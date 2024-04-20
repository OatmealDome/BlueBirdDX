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
            int attachmentCount = item.AttachedMedia.Count + (item.QuotedPost != null ? 0 : 1);

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
