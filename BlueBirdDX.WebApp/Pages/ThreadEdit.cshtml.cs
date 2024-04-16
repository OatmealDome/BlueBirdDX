using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.WebApp.Api;
using BlueBirdDX.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BlueBirdDX.WebApp.Pages;

public class ThreadEditModel : PageModel
{
    private readonly DatabaseService _database;

    public string ApiThreadId
    {
        get;
        set;
    }
    
    public PostThreadApi ApiThread
    {
        get;
        set;
    }

    public Dictionary<string, string> MediaNameCache
    {
        get;
        set;
    }

    public ThreadEditModel(DatabaseService databaseService)
    {
        _database = databaseService;
    }

    public IActionResult OnGet(string threadId)
    {
        MediaNameCache = new Dictionary<string, string>();
        
        if (threadId == "new")
        {
            ApiThread = new PostThreadApi();
        }
        else
        {
            if (!ObjectId.TryParse(threadId, out ObjectId objectId))
            {
                return NotFound();
            }

            PostThread? realThread =
                _database.PostThreadCollection.AsQueryable().SingleOrDefault(p => p._id == objectId);

            if (realThread == null)
            {
                return NotFound();
            }
            
            MediaNameCache = new Dictionary<string, string>();

            List<ObjectId> mediaIds = realThread.Items.SelectMany(i => i.AttachedMedia).Distinct().ToList();
            IEnumerable<UploadedMedia> allMedia =
                _database.UploadedMediaCollection.AsQueryable().Where(m => mediaIds.Contains(m._id));

            foreach (UploadedMedia media in allMedia)
            {
                MediaNameCache[media._id.ToString()] = media.Name;
            }

            ApiThread = new PostThreadApi(realThread);
        }

        ApiThreadId = threadId;

        return Page();
    }
}