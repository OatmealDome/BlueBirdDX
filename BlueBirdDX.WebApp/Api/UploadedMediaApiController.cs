using Amazon.S3;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Storage;
using BlueBirdDX.Api;
using BlueBirdDX.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using NATS.Net;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace BlueBirdDX.WebApp.Api;

[ApiController]
[Produces("application/json")]
public class UploadedMediaApiController : ControllerBase
{
    private readonly DatabaseService _database;
    private readonly RemoteStorage _remoteStorage;
    private readonly NatsClient _natsClient;

    public UploadedMediaApiController(DatabaseService database, RemoteStorageService remoteStorage,
        NotificationService notification)
    {
        _database = database;
        _remoteStorage = remoteStorage.SharedInstance;
        _natsClient = notification.Client;
    }
    
    [HttpGet]
    [Route("/api/v1/media")]
    [ProducesResponseType(typeof(List<UploadedMediaApi>), StatusCodes.Status200OK)]
    public IActionResult GetMedia()
    {
        IEnumerable<UploadedMedia> media = _database.UploadedMediaCollection.AsQueryable();
        
        return Ok(media.Select(m => UploadedMediaApiExtensions.CreateApiFromCommon(m)));
    }

    private MediaUploadJob CreateMediaUploadJob(string name, string mimeType, string altText)
    {
        MediaUploadJob uploadJob = new MediaUploadJob()
        {
            SchemaVersion = UploadedMedia.LatestSchemaVersion,
            Name = name,
            MimeType = mimeType,
            AltText = altText,
            CreationTime = DateTime.UtcNow,
            State = MediaUploadJobState.Uploading,
            MediaId = null
        };

        _database.MediaUploadJobCollection.InsertOne(uploadJob);

        return uploadJob;
    }
    
    private MediaUploadJob? FindMediaUploadJobById(ObjectId jobId)
    {
        return _database.MediaUploadJobCollection.AsQueryable().FirstOrDefault(m => m._id == jobId);
    }
    
    private MediaUploadJob? FindMediaUploadJobById(string jobId)
    {
        if (!ObjectId.TryParse(jobId, out ObjectId jobIdObj))
        {
            return null;
        }

        return FindMediaUploadJobById(jobIdObj);
    }
    
    // Kept for backwards compatibility.
    [HttpPost]
    [Route("/api/v1/media")]
    [ProducesResponseType(typeof(UploadedMediaApi), StatusCodes.Status200OK)]
    public async Task<IActionResult> PostMediaVersionOne([FromForm] string name, IFormFile file, [FromForm] string? altText = null)
    {
        if (name == "")
        {
            return Problem("Name cannot be empty", statusCode: 400);
        }
        
        using MemoryStream memoryStream = new MemoryStream();
        
        file.CopyTo(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);
        
        IImageFormat format;

        try
        {
            format = Image.DetectFormat(memoryStream);
        }
        catch (Exception)
        {
            return Problem("Unsupported file type. If you are uploading video, use the new upload job endpoints.",
                statusCode: 415);
        }

        MediaUploadJob uploadJob = CreateMediaUploadJob(name, format.DefaultMimeType, altText ?? "");

        _remoteStorage.TransferFile("unprocessed_media/" + uploadJob._id.ToString(), memoryStream.ToArray());
        
        uploadJob.State = MediaUploadJobState.Ready;

        _database.MediaUploadJobCollection.ReplaceOne(Builders<MediaUploadJob>.Filter.Eq(j => j._id, uploadJob._id),
            uploadJob);
        
        await _natsClient.PublishAsync("media.jobs.ready", uploadJob._id.ToString());
        
        // Waiting isn't great, but I'm not sure how else to implement this.
        while (uploadJob.State != MediaUploadJobState.Success && uploadJob.State != MediaUploadJobState.Failed)
        {
            Thread.Sleep(1000);

            uploadJob = FindMediaUploadJobById(uploadJob._id)!;
        }

        if (uploadJob.State == MediaUploadJobState.Failed)
        {
            return Problem($"Failed to process job due to error \"{uploadJob.State}\"", statusCode: 500);
        }

        UploadedMedia media = _database.UploadedMediaCollection.AsQueryable()
            .FirstOrDefault(m => m._id == uploadJob.MediaId)!;

        return Ok(UploadedMediaApiExtensions.CreateApiFromCommon(media));
    }

    [HttpPost]
    [Route("/api/v2/media/job")]
    [ProducesResponseType(typeof(CreateMediaUploadJobResponse), StatusCodes.Status200OK)]
    public IActionResult PostMediaUploadJob([FromForm] string name, [FromForm] string mimeType,
        [FromForm] string? altText = null)
    {
        MediaUploadJob uploadJob = CreateMediaUploadJob(name, mimeType, altText ?? "");

        string url =
            _remoteStorage.GetPreSignedUrlForFile("unprocessed_media/" + uploadJob._id.ToString(), HttpVerb.PUT, 60);

        return Ok(new CreateMediaUploadJobResponse()
        {
            Id = uploadJob._id.ToString(),
            TargetUrl = url
        });
    }
    
    [HttpPut]
    [Route("/api/v2/media/job/{jobId}/state")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutMediaUploadJobState(string jobId, [FromBody] ChangeMediaUploadJobStateRequest request)
    {
        MediaUploadJob? job = FindMediaUploadJobById(jobId);

        if (job == null)
        {
            return Problem("Invalid media upload job ID", statusCode: 404);
        }

        if (job.State != MediaUploadJobState.Uploading)
        {
            return BadRequest("Upload job is not in Uploading state");
        }

        if (request.State != (int)MediaUploadJobState.Uploading && request.State != (int)MediaUploadJobState.Ready)
        {
            return BadRequest("Can only set state to Uploading or Ready");
        }

        job.State = (MediaUploadJobState)request.State;

        await _database.MediaUploadJobCollection.ReplaceOneAsync(
            Builders<MediaUploadJob>.Filter.Eq(j => j._id, job._id), job);

        await _natsClient.PublishAsync("media.jobs.ready", job._id.ToString());
        
        return Ok();
    }

    [HttpGet]
    [Route("/api/v2/media/job/{jobId}/state")]
    [ProducesResponseType(typeof(CheckMediaUploadJobStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetMediaUploadJob(string jobId)
    {
        MediaUploadJob? job = FindMediaUploadJobById(jobId);

        if (job == null)
        {
            return Problem("Invalid media upload job ID", statusCode: 404);
        }

        return Ok(new CheckMediaUploadJobStateResponse()
        {
            State = (int)job.State,
            MediaId = job.MediaId?.ToString() ?? null,
            ErrorDetail = job.ErrorDetail
        });
    }

    private UploadedMedia? FindMediaById(string mediaId)
    {
        if (!ObjectId.TryParse(mediaId, out ObjectId mediaIdObj))
        {
            return null;
        }

        UploadedMedia? media = _database.UploadedMediaCollection.AsQueryable().FirstOrDefault(m => m._id == mediaIdObj);

        if (media == null)
        {
            return null;
        }

        return media;
    }

    [HttpGet]
    [Route("/api/v1/media/{mediaId}")]
    [ProducesResponseType(typeof(UploadedMediaApi), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetMedia(string mediaId)
    {
        UploadedMedia? media = FindMediaById(mediaId);

        if (media == null)
        {
            return Problem("Invalid media ID", statusCode: 404);
        }
        
        return Ok(UploadedMediaApiExtensions.CreateApiFromCommon(media));
    }
    
    [HttpPut]
    [Route("/api/v1/media/{mediaId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult PutMedia(string mediaId, [FromBody] UploadedMediaApi apiMedia)
    {
        UploadedMedia? realMedia = FindMediaById(mediaId);

        if (realMedia == null)
        {
            return Problem("Invalid media ID", statusCode: 404);
        }
        
        // Sanity checks

        if (apiMedia.Name == "")
        {
            return Problem("Name cannot be empty", statusCode: 400);
        }

        apiMedia.TransferApiToCommon(realMedia);

        _database.UploadedMediaCollection.ReplaceOne(Builders<UploadedMedia>.Filter.Eq(m => m._id, realMedia._id),
            realMedia);

        return Ok();
    }
}