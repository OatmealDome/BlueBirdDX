using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.PublicApi;
using BlueBirdDX.WebApp.Api;
using BlueBirdDX.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BlueBirdDX.WebApp.Pages;

public class MediaEditModel : PageModel
{
    private readonly DatabaseService _database;

    public string MediaId
    {
        get;
        set;
    }
    
    public UploadedMediaApi ApiMedia
    {
        get;
        set;
    }

    public MediaEditModel(DatabaseService databaseService)
    {
        _database = databaseService;
    }
    
    public IActionResult OnGet(string mediaId)
    {
        MediaId = mediaId;

        if (!ObjectId.TryParse(mediaId, out ObjectId objectId))
        {
            return NotFound();
        }

        UploadedMedia? realMedia =
            _database.UploadedMediaCollection.AsQueryable().SingleOrDefault(m => m._id == objectId);

        if (realMedia == null)
        {
            return NotFound();
        }

        ApiMedia = UploadedMediaApiExtensions.CreateApiFromCommon(realMedia);

        return Page();
    }
}