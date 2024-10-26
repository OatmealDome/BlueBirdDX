using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Storage;
using BlueBirdDX.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BlueBirdDX.WebApp.Pages;

public class MediaPreviewModel : PageModel
{
    public readonly DatabaseService Database;
    public readonly RemoteStorage Storage;

    public MediaPreviewModel(DatabaseService databaseService, RemoteStorageService remoteStorageService)
    {
        Database = databaseService;
        Storage = remoteStorageService.SharedInstance;
    }
    
    public IActionResult OnGet(string mediaId)
    {
        if (!ObjectId.TryParse(mediaId, out ObjectId objectId))
        {
            return NotFound();
        }

        UploadedMedia? realMedia =
            Database.UploadedMediaCollection.AsQueryable().SingleOrDefault(m => m._id == objectId);

        if (realMedia == null)
        {
            return NotFound();
        }
        
        string url = Storage.GetPreSignedUrlForFile("media/" + mediaId, 15);

        return Redirect(url);
    }
}