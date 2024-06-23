using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BlueBirdDX.WebApp.Api;

[ApiController]
[Produces("application/json")]
public class PostThreadApiController : ControllerBase
{
    private readonly DatabaseService _database;

    public PostThreadApiController(DatabaseService database)
    {
        _database = database;
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

        PostThread? postThread = _database.PostThreadCollection.AsQueryable().FirstOrDefault(p => p._id == threadIdObj);

        if (postThread == null)
        {
            return Problem("Invalid thread ID", statusCode: 404);
        }

        return Ok(new PostThreadApi(postThread));
    }

    private bool IsIncomingThreadSane(PostThreadApi inState, out string? error)
    {
        if (!ObjectId.TryParse(inState.TargetGroup, out ObjectId groupId))
        {
            error = "Invalid account group ID";
            return false;
        }

        AccountGroup? group = _database.AccountGroupCollection.AsQueryable().FirstOrDefault(g => g._id == groupId);

        if (group == null)
        {
            error = "Invalid account group ID";
            return false;
        }

        if (inState.PostToTwitter && group.Twitter == null)
        {
            error = "Twitter account does not exist in this group";
            return false;
        }
        
        if (inState.PostToBluesky && group.Bluesky == null)
        {
            error = "Bluesky account does not exist in this group";
            return false;
        }
        
        if (inState.PostToMastodon && group.Mastodon == null)
        {
            error = "Mastodon account does not exist in this group";
            return false;
        }
        
        if (inState.PostToThreads && group.Threads == null)
        {
            error = "Threads account does not exist in this group";
            return false;
        }
        
        if (inState.ParentThread != null)
        {
            if (!ObjectId.TryParse(inState.ParentThread, out ObjectId parentId))
            {
                error = "Invalid parent thread ID";
                return false;
            }

            PostThread? parentThread =
                _database.PostThreadCollection.AsQueryable().FirstOrDefault(t => t._id == parentId);

            if (parentThread == null)
            {
                error = "Invalid parent thread ID";
                return false;
            }

            if (parentThread.State != PostThreadState.Sent)
            {
                error = "Parent thread not in Sent state";
                return false;
            }

            PostThreadItem lastParentItem = parentThread.Items.Last();

            if (inState.PostToTwitter)
            {
                if (!parentThread.PostToTwitter)
                {
                    error = "Parent thread not posted to Twitter";
                    return false;
                }

                if (lastParentItem.TwitterId == null)
                {
                    error = "Final item in parent thread has no tweet ID";
                    return false;
                }
            }
            
            if (inState.PostToBluesky)
            {
                if (!parentThread.PostToBluesky)
                {
                    error = "Parent thread not posted to Bluesky";
                    return false;
                }
                
                if (lastParentItem.BlueskyRootRef == null || lastParentItem.BlueskyThisRef == null)
                {
                    error = "Final item in parent thread has no Bluesky post reference(s)";
                    return false;
                }
            }
            
            if (inState.PostToMastodon)
            {
                if (!parentThread.PostToMastodon)
                {
                    error = "Parent thread not posted to Mastodon";
                    return false;
                }
                
                if (lastParentItem.MastodonId == null)
                {
                    error = "Final item in parent thread has no Mastodon status ID";
                    return false;
                }
            }
            
            if (inState.PostToThreads)
            {
                if (!parentThread.PostToThreads)
                {
                    error = "Parent thread not posted to Threads";
                    return false;
                }
                
                if (lastParentItem.ThreadsId == null)
                {
                    error = "Final item in parent thread has no Threads media container ID";
                    return false;
                }
            }
        }
        
        if (inState.ScheduledTime.Kind != DateTimeKind.Utc)
        {
            error = "Scheduled time is not in UTC";
            return false;
        }

        if (inState.State == PostThreadState.Sent || inState.State == PostThreadState.Error)
        {
            error = "Invalid thread state";
            return false;
        }
        
        if (inState.Name.Length == 0)
        {
            error = "Name is empty";
            return false;
        }

        if (inState.State == PostThreadState.Enqueued && DateTime.UtcNow > inState.ScheduledTime)
        {
            TimeSpan span = DateTime.UtcNow - inState.ScheduledTime;

            if (span.TotalMinutes > 4.0d)
            {
                error = "Scheduled time is too far in the past";
                return false;   
            }
        }

        if (inState.Items.Count == 0)
        {
            error = "Thread has no items";
            return false;
        }

        foreach (PostThreadItemApi item in inState.Items)
        {
            if (item.QuotedPost != null)
            {
                if (!Uri.TryCreate(item.QuotedPost, UriKind.Absolute, out Uri? uri))
                {
                    error = "Invalid quoted post URL";
                    return false;
                }

                if (uri.Host != "twitter.com" && uri.Host != "x.com")
                {
                    error = "Quoted post URL is not a Twitter URL";
                    return false;
                }

                if (!uri.PathAndQuery.Contains("status"))
                {
                    error = "Quoted post URL is not a Tweet URL";
                    return false;
                }
            }
            
            int attachmentCount = item.AttachedMedia.Count + (item.QuotedPost != null ? 1 : 0);

            if (attachmentCount > 4)
            {
                error = "Thread has too many attachments";
                return false;
            }
            
            foreach (string mediaId in item.AttachedMedia)
            {
                if (!ObjectId.TryParse(mediaId, out ObjectId mediaIdObj))
                {
                    error = "Invalid media ID";
                    return false;
                }

                UploadedMedia? media = _database.UploadedMediaCollection.AsQueryable()
                    .FirstOrDefault(m => m._id == mediaIdObj);

                if (media == null)
                {
                    error = "Invalid media ID";
                    return false;
                }
            }
        }

        error = null;

        return true;
    }
    
    [HttpPost]
    [Route("/api/v1/thread")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult PostPostThread(PostThreadApi postThreadApi)
    {
        if (!IsIncomingThreadSane(postThreadApi, out string? error))
        {
            return Problem(error, statusCode: 400);
        }
        
        PostThread postThread = new PostThread();
        postThreadApi.TransferToNormal(postThread);
        
        _database.PostThreadCollection.InsertOne(postThread);
        
        return Ok();
    }
    
    [HttpPut]
    [Route("/api/v1/thread/{threadId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult PutPostThread(string threadId, PostThreadApi postThreadApi)
    {
        if (!ObjectId.TryParse(threadId, out ObjectId threadIdObj))
        {
            return Problem("Invalid thread ID", statusCode: 404);
        }

        PostThread? postThread = _database.PostThreadCollection.AsQueryable().FirstOrDefault(p => p._id == threadIdObj);

        if (postThread == null)
        {
            return Problem("Invalid thread ID", statusCode: 404);
        }
        
        if (postThread.State == PostThreadState.Sent || postThread.State == PostThreadState.Error)
        {
            return Problem("Thread is already in Sent or Error state", statusCode: 400);
        }
        
        if (!IsIncomingThreadSane(postThreadApi, out string? error))
        {
            return Problem(error, statusCode: 400);
        }
        
        postThreadApi.TransferToNormal(postThread);
        
        _database.PostThreadCollection.ReplaceOne(Builders<PostThread>.Filter.Eq(p => p._id, postThread._id),
            postThread);

        return Ok();
    }
}
