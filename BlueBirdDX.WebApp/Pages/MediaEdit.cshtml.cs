using BlueBirdDX.Common.Media;
using BlueBirdDX.Api;
using BlueBirdDX.WebApp.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.WebApp.Pages;

public class MediaEditModel : PageModel
{
    private readonly IMongoCollection<UploadedMedia> _uploadedMediaCollection;

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

    public MediaEditModel(SlabMongoService mongoService)
    {
        _uploadedMediaCollection = mongoService.GetCollection<UploadedMedia>("media");
    }
    
    public IActionResult OnGet(string mediaId)
    {
        MediaId = mediaId;

        if (!ObjectId.TryParse(mediaId, out ObjectId objectId))
        {
            return NotFound();
        }

        UploadedMedia? realMedia = _uploadedMediaCollection.AsQueryable().SingleOrDefault(m => m._id == objectId);

        if (realMedia == null)
        {
            return NotFound();
        }

        ApiMedia = UploadedMediaApiExtensions.CreateApiFromCommon(realMedia);

        return Page();
    }
}
