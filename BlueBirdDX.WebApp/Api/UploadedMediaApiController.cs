using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Storage;
using BlueBirdDX.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace BlueBirdDX.WebApp.Api;

[ApiController]
[Produces("application/json")]
public class UploadedMediaApiController : ControllerBase
{
    private readonly DatabaseService _database;
    private readonly RemoteStorage _remoteStorage;

    public UploadedMediaApiController(DatabaseService database, RemoteStorageService remoteStorage)
    {
        _database = database;
        _remoteStorage = remoteStorage.SharedInstance;
    }

    [HttpGet]
    [Route("/api/v1/media")]
    [ProducesResponseType(typeof(List<UploadedMediaMiniApi>), StatusCodes.Status200OK)]
    public IActionResult GetMedia()
    {
        IEnumerable<UploadedMedia> media = _database.UploadedMediaCollection.AsQueryable();
        
        return Ok(media.Select(m => new UploadedMediaMiniApi(m)));
    }
    
    [HttpPost]
    [Route("/api/v1/media")]
    [ProducesResponseType(typeof(UploadedMediaMiniApi), StatusCodes.Status200OK)]
    public IActionResult PostMedia([FromForm] string name, IFormFile file, [FromForm] string? altText = null)
    {
        if (name == "")
        {
            return Problem("Name cannot be empty", statusCode: 400);
        }
        
        using MemoryStream memoryStream = new MemoryStream();
        
        file.CopyTo(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);
        
        // TODO expand format detection to video
        
        IImageFormat format;

        try
        {
            format = Image.DetectFormat(memoryStream);
        }
        catch (Exception)
        {
            return Problem("Unsupported file type", statusCode: 415);
        }
        
        UploadedMedia media = new UploadedMedia()
        {
            SchemaVersion = UploadedMedia.LatestSchemaVersion,
            Name = name,
            AltText = altText,
            MimeType = format.DefaultMimeType,
            CreationTime = DateTime.UtcNow
        };
        
        _database.UploadedMediaCollection.InsertOne(media);

        try
        {
            _remoteStorage.TransferFile(media._id.ToString(), memoryStream, format.DefaultMimeType);
        }
        catch (Exception)
        {
            _database.UploadedMediaCollection.DeleteOne(Builders<UploadedMedia>.Filter.Eq(m => m._id, media._id));

            return Problem("An error occurred while transferring the media data to remote storage", statusCode: 500);
        }
        
        return Ok(new UploadedMediaMiniApi(media));
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
        
        return Ok(new UploadedMediaApi(media));
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
        
        apiMedia.TransferToNormal(realMedia);

        _database.UploadedMediaCollection.ReplaceOne(Builders<UploadedMedia>.Filter.Eq(m => m._id, realMedia._id),
            realMedia);

        return Ok();
    }
    
    [HttpGet]
    [Route("/api/v1/media/{mediaId}/url")]
    [ProducesResponseType(typeof(UploadedMediaUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetMediaUrl(string mediaId)
    {
        UploadedMedia? media = FindMediaById(mediaId);

        if (media == null)
        {
            return Problem("Invalid media ID", statusCode: 404);
        }

        string url;

        try
        {
            url = _remoteStorage.GetPreSignedUrlForFile(mediaId, 15);
        }
        catch (Exception)
        {
            return Problem("Failed to generate pre-signed URL", statusCode: 500);
        }
        
        return Ok(new UploadedMediaUrlResponse()
        {
            Url = url
        });
    }
}