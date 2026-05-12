using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Api;
using BlueBirdDX.Common.Social;
using BlueBirdDX.Common.Util;
using BlueBirdDX.Common.Util.TextWrapper;
using BlueBirdDX.Grpc;
using BlueBirdDX.WebApp.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;
using GrpcStatusCode = Grpc.Core.StatusCode;

namespace BlueBirdDX.WebApp.Api;

[ApiController]
[Produces("application/json")]
public class PostThreadApiController : ControllerBase
{
    private const string TwitterUrlPathRegex = "^/[A-Za-z0-9_]+/status/[0-9]+";
    
    private readonly IMongoCollection<PostThread> _postThreadCollection;
    private readonly IMongoCollection<AccountGroup> _accountGroupCollection;
    private readonly IMongoCollection<UploadedMedia> _uploadedMediaCollection;
    private readonly TextWrapperClient  _textWrapperClient;
    private readonly PostThreadManagerRemoteService.PostThreadManagerRemoteServiceClient _postThreadManagerClient;

    public PostThreadApiController(SlabMongoService mongoService, TextWrapperService textWrapperService,
        PostThreadManagerRemoteService.PostThreadManagerRemoteServiceClient postThreadManagerClient)
    {
        _postThreadCollection = mongoService.GetCollection<PostThread>("threads");
        _accountGroupCollection = mongoService.GetCollection<AccountGroup>("accounts");
        _uploadedMediaCollection = mongoService.GetCollection<UploadedMedia>("media");
        _textWrapperClient =  textWrapperService.Client;
        _postThreadManagerClient = postThreadManagerClient;
    }

    [HttpGet]
    [Route("/api/v1/thread/{threadId}")]
    [ProducesResponseType(typeof(PostThreadApi), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetPostThread(string threadId)
    {
        if (!ObjectId.TryParse(threadId, out ObjectId threadIdObj))
        {
            return Problem("Invalid thread ID", statusCode: 404);
        }

        PostThread? postThread = _postThreadCollection.AsQueryable().FirstOrDefault(p => p._id == threadIdObj);

        if (postThread == null)
        {
            return Problem("Invalid thread ID", statusCode: 404);
        }

        return Ok(PostThreadApiExtensions.CreateApiFromCommon(postThread));
    }

    private async Task<string?> ValidateIncomingThreadAndGetError(PostThreadApi inState)
    {
        if (!ObjectId.TryParse(inState.TargetGroup, out ObjectId groupId))
        {
            return "Invalid account group ID";
        }

        AccountGroup? group = _accountGroupCollection.AsQueryable().FirstOrDefault(g => g._id == groupId);

        if (group == null)
        {
            return "Invalid account group ID";
        }

        if (inState.PostToTwitter && group.Twitter == null)
        {
            return "Twitter account does not exist in this group";
        }

        if (inState.PostToBluesky && group.Bluesky == null)
        {
            return "Bluesky account does not exist in this group";
        }

        if (inState.PostToMastodon && group.Mastodon == null)
        {
            return "Mastodon account does not exist in this group";
        }

        if (inState.PostToThreads && group.Threads == null)
        {
            return "Threads account does not exist in this group";
        }

        if (inState.ParentThread != null)
        {
            if (!ObjectId.TryParse(inState.ParentThread, out ObjectId parentId))
            {
                return "Invalid parent thread ID";
            }

            PostThread? parentThread = _postThreadCollection.AsQueryable().FirstOrDefault(t => t._id == parentId);

            if (parentThread == null)
            {
                return "Invalid parent thread ID";
            }

            if (parentThread.State != PostThreadState.Sent)
            {
                return "Parent thread not in Sent state";
            }

            PostThreadItem lastParentItem = parentThread.Items.Last();

            if (inState.PostToTwitter)
            {
                if (!parentThread.PostToTwitter)
                {
                    return "Parent thread not posted to Twitter";
                }

                if (lastParentItem.TwitterId == null)
                {
                    return "Final item in parent thread has no tweet ID";
                }
            }

            if (inState.PostToBluesky)
            {
                if (!parentThread.PostToBluesky)
                {
                    return "Parent thread not posted to Bluesky";
                }

                if (lastParentItem.BlueskyRootRef == null || lastParentItem.BlueskyThisRef == null)
                {
                    return "Final item in parent thread has no Bluesky post reference(s)";
                }
            }

            if (inState.PostToMastodon)
            {
                if (!parentThread.PostToMastodon)
                {
                    return "Parent thread not posted to Mastodon";
                }

                if (lastParentItem.MastodonId == null)
                {
                    return "Final item in parent thread has no Mastodon status ID";
                }
            }

            if (inState.PostToThreads)
            {
                if (!parentThread.PostToThreads)
                {
                    return "Parent thread not posted to Threads";
                }

                if (lastParentItem.ThreadsId == null)
                {
                    return "Final item in parent thread has no Threads media container ID";
                }
            }
        }

        if (inState.ScheduledTime.Kind != DateTimeKind.Utc)
        {
            return "Scheduled time is not in UTC";
        }

        if (inState.State == (int)PostThreadState.Sent ||
            inState.State == (int)PostThreadState.Error ||
            inState.State == (int)PostThreadState.Deleted)
        {
            return "Invalid thread state";
        }

        if (inState.Name.Length == 0)
        {
            return "Name is empty";
        }

        if (inState.State == (int)PostThreadState.Enqueued && DateTime.UtcNow > inState.ScheduledTime)
        {
            TimeSpan span = DateTime.UtcNow - inState.ScheduledTime;

            if (span.TotalMinutes > 4.0d)
            {
                return "Scheduled time is too far in the past";
            }
        }

        if (inState.Items.Count == 0)
        {
            return "Thread has no items";
        }

        foreach (PostThreadItemApi item in inState.Items)
        {
            if (item.QuotedPost != null)
            {
                if (!Uri.TryCreate(item.QuotedPost, UriKind.Absolute, out Uri? uri))
                {
                    return "Invalid quoted post URL";
                }

                if (uri.Host != "twitter.com" && uri.Host != "x.com" && uri.Host != "bsky.app")
                {
                    return "Quoted post URL is not a Twitter or Bluesky URL";
                }

                // TODO url format enforcement regex

                QuotedPost quotedPost = await QuotedPost.BuildInitialFromUrl(item.QuotedPost, _postThreadCollection);

                if (inState.PostToTwitter)
                {
                    int length = await _textWrapperClient.CountCharacters(item.Text);

                    if (quotedPost.GetPrimaryPlatform() != SocialPlatform.Twitter)
                    {
                        // We need 28 characters for the link to the external platform.
                        if (length > 252)
                        {
                            return "Cannot exceed 252 characters when quoting a post from another platform on Twitter";
                        }
                    }
                    else
                    {
                        // Twitter API anti-spam restrictions forbids us from quoting directly. We can still quote
                        // by including the URL directly in the body, but this takes up characters.
                        if (length > 256)
                        {
                            return "Cannot exceed 256 characters when quoting a post on Twitter";
                        }
                    }
                }
            }

            int attachmentCount = item.AttachedMedia.Count + (item.QuotedPost != null ? 1 : 0);

            if (attachmentCount > 4)
            {
                return "Thread has too many attachments";
            }

            foreach (string mediaId in item.AttachedMedia)
            {
                if (!ObjectId.TryParse(mediaId, out ObjectId mediaIdObj))
                {
                    return "Invalid media ID";
                }

                UploadedMedia? media = _uploadedMediaCollection.AsQueryable().FirstOrDefault(m => m._id == mediaIdObj);

                if (media == null)
                {
                    return "Invalid media ID";
                }
            }
        }

        return null;
    }
    
    [HttpPost]
    [Route("/api/v1/thread")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PostPostThread(PostThreadApi postThreadApi)
    {
        string? error = await ValidateIncomingThreadAndGetError(postThreadApi);
        if (error != null)
        {
            return Problem(error, statusCode: 400);
        }

        PostThread postThread = new PostThread();
        postThreadApi.TransferApiToCommon(postThread);
        
        _postThreadCollection.InsertOne(postThread);
        
        return Ok();
    }
    
    [HttpPut]
    [Route("/api/v1/thread/{threadId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutPostThread(string threadId, PostThreadApi postThreadApi)
    {
        if (!ObjectId.TryParse(threadId, out ObjectId threadIdObj))
        {
            return Problem("Invalid thread ID", statusCode: 404);
        }

        PostThread? postThread = _postThreadCollection.AsQueryable().FirstOrDefault(p => p._id == threadIdObj);

        if (postThread == null)
        {
            return Problem("Invalid thread ID", statusCode: 404);
        }
        
        if (postThread.State == PostThreadState.Sent ||
            postThread.State == PostThreadState.Error ||
            postThread.State == PostThreadState.Deleted)
        {
            return Problem("Thread is already in a finalized state", statusCode: 400);
        }
        
        string? error = await ValidateIncomingThreadAndGetError(postThreadApi);
        if (error != null)
        {
            return Problem(error, statusCode: 400);
        }

        postThreadApi.TransferApiToCommon(postThread);

        _postThreadCollection.ReplaceOne(Builders<PostThread>.Filter.Eq(p => p._id, postThread._id), postThread);

        return Ok();
    }

    [HttpPost]
    [Route("/api/v1/thread/{threadId}/delete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeletePostThreadFromSocialPlatforms(string threadId)
    {
        try
        {
            await _postThreadManagerClient.DeletePostThreadFromSocialPlatformsAsync(
                new DeletePostThreadFromSocialPlatformsRequest
                {
                    ThreadId = threadId
                });
        }
        catch (RpcException e) when (e.StatusCode == GrpcStatusCode.InvalidArgument)
        {
            return Problem(e.Status.Detail, statusCode: 400);
        }
        catch (RpcException e) when (e.StatusCode == GrpcStatusCode.NotFound)
        {
            return Problem(e.Status.Detail, statusCode: 404);
        }
        catch (RpcException e)
        {
            return Problem(e.Status.Detail, statusCode: 500);
        }

        return Ok();
    }
}
