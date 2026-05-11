using Amazon.S3;
using BlueBirdDX.Common.Media;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;
using OatmealDome.Slab.S3;

namespace BlueBirdDX.WebApp.Pages;

public class MediaPreviewModel : PageModel
{
    private readonly IMongoCollection<UploadedMedia> _uploadedMediaCollection;
    private readonly SlabS3Service _s3Service;

    public MediaPreviewModel(SlabMongoService mongoService, SlabS3Service s3Service)
    {
        _uploadedMediaCollection = mongoService.GetCollection<UploadedMedia>("media");
        _s3Service = s3Service;
    }
    
    public async Task<IActionResult> OnGet(string mediaId)
    {
        if (!ObjectId.TryParse(mediaId, out ObjectId objectId))
        {
            return NotFound();
        }

        UploadedMedia? realMedia = _uploadedMediaCollection.AsQueryable().SingleOrDefault(m => m._id == objectId);

        if (realMedia == null)
        {
            return NotFound();
        }
        
        string url = await _s3Service.GetPreSignedUrlForFile("media/" + mediaId, HttpVerb.GET, 15);

        return Redirect(url);
    }
}
