using Amazon.S3;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Api;
using BlueBirdDX.Grpc;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;
using OatmealDome.Slab.S3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace BlueBirdDX.WebApp.Api;

[ApiController]
[Produces("application/json")]
public class UploadedMediaApiController : ControllerBase
{
    private readonly IMongoCollection<UploadedMedia> _uploadedMediaCollection;
    private readonly IMongoCollection<MediaUploadJob> _mediaUploadJobCollection;
    private readonly SlabS3Service _s3Service;
    private readonly MediaUploadJobManagerRemoteService.MediaUploadJobManagerRemoteServiceClient _mediaUploadJobsClient;

    public UploadedMediaApiController(SlabMongoService mongoService, SlabS3Service s3Service,
        MediaUploadJobManagerRemoteService.MediaUploadJobManagerRemoteServiceClient mediaUploadJobsClient)
    {
        _uploadedMediaCollection = mongoService.GetCollection<UploadedMedia>("media");
        _mediaUploadJobCollection = mongoService.GetCollection<MediaUploadJob>("media_jobs");
        _s3Service = s3Service;
        _mediaUploadJobsClient = mediaUploadJobsClient;
    }
    
    [HttpGet]
    [Route("/api/v1/media")]
    [ProducesResponseType(typeof(List<UploadedMediaApi>), StatusCodes.Status200OK)]
    public IActionResult GetMedia()
    {
        IEnumerable<UploadedMedia> media = _uploadedMediaCollection.AsQueryable();
        
        return Ok(media.Select(m => UploadedMediaApiExtensions.CreateApiFromCommon(m)).Reverse());
    }

    private MediaUploadJob CreateMediaUploadJob(string name, string mimeType, string altText)
    {
        MediaUploadJob uploadJob = new MediaUploadJob()
        {
            SchemaVersion = MediaUploadJob.LatestSchemaVersion,
            Name = name,
            MimeType = mimeType,
            AltText = altText,
            CreationTime = DateTime.UtcNow,
            State = MediaUploadJobState.Uploading,
            MediaId = null
        };

        _mediaUploadJobCollection.InsertOne(uploadJob);

        return uploadJob;
    }
    
    private MediaUploadJob? FindMediaUploadJobById(ObjectId jobId)
    {
        return _mediaUploadJobCollection.AsQueryable().FirstOrDefault(m => m._id == jobId);
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

        await _s3Service.TransferFile("unprocessed_media/" + uploadJob._id.ToString(), memoryStream.ToArray());
        
        uploadJob.State = MediaUploadJobState.Ready;

        _mediaUploadJobCollection.ReplaceOne(Builders<MediaUploadJob>.Filter.Eq(j => j._id, uploadJob._id),
            uploadJob);
        
        await ProcessReadyMediaUploadJob(uploadJob._id);
        
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

        UploadedMedia media = _uploadedMediaCollection.AsQueryable().FirstOrDefault(m => m._id == uploadJob.MediaId)!;

        return Ok(UploadedMediaApiExtensions.CreateApiFromCommon(media));
    }

    [HttpPost]
    [Route("/api/v2/media/job")]
    [ProducesResponseType(typeof(CreateMediaUploadJobResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PostMediaUploadJob([FromForm] string name, [FromForm] string mimeType,
        [FromForm] string? altText = null)
    {
        MediaUploadJob uploadJob = CreateMediaUploadJob(name, mimeType, altText ?? "");

        string url =
            await _s3Service.GetPreSignedUrlForFile("unprocessed_media/" + uploadJob._id.ToString(), HttpVerb.PUT, 60);

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

        await _mediaUploadJobCollection.ReplaceOneAsync(Builders<MediaUploadJob>.Filter.Eq(j => j._id, job._id), job);

        if (job.State == MediaUploadJobState.Ready)
        {
            await ProcessReadyMediaUploadJob(job._id);
        }
        
        return Ok();
    }

    private async Task ProcessReadyMediaUploadJob(ObjectId jobId)
    {
        await _mediaUploadJobsClient.ProcessReadyMediaUploadJobAsync(new ProcessReadyMediaUploadJobRequest
        {
            JobId = jobId.ToString()
        });
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

        UploadedMedia? media = _uploadedMediaCollection.AsQueryable().FirstOrDefault(m => m._id == mediaIdObj);

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

        _uploadedMediaCollection.ReplaceOne(Builders<UploadedMedia>.Filter.Eq(m => m._id, realMedia._id), realMedia);

        return Ok();
    }
}
