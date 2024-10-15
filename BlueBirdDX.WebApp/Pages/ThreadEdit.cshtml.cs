using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Api;
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

    public IActionResult OnGet(string threadId, [FromQuery] string? baseThreadId = null)
    {
        MediaNameCache = new Dictionary<string, string>();

        PostThreadApi? LoadThreadById(string id)
        {
            if (!ObjectId.TryParse(id, out ObjectId objectId))
            {
                return null;
            }

            PostThread? realThread =
                _database.PostThreadCollection.AsQueryable().SingleOrDefault(p => p._id == objectId);
 
            if (realThread == null)
            {
                return null;
            }
            
            MediaNameCache = new Dictionary<string, string>();

            List<ObjectId> mediaIds = realThread.Items.SelectMany(i => i.AttachedMedia).Distinct().ToList();
            IEnumerable<UploadedMedia> allMedia =
                _database.UploadedMediaCollection.AsQueryable().Where(m => mediaIds.Contains(m._id));

            foreach (UploadedMedia media in allMedia)
            {
                MediaNameCache[media._id.ToString()] = media.Name;
            }

            return PostThreadApiExtensions.CreateApiFromCommon(realThread);
        }
        
        if (threadId == "new")
        {
            if (baseThreadId != null)
            {
                PostThreadApi? loadedThread = LoadThreadById(baseThreadId);

                if (loadedThread == null)
                {
                    return NotFound();
                }

                loadedThread.Name += " (Copy)";
                loadedThread.State = 0;
                loadedThread.ScheduledTime = DateTime.UnixEpoch;

                ApiThread = loadedThread;
            }
            else
            {
                ApiThread = new PostThreadApi();
            }
        }
        else
        {
            PostThreadApi? loadedThread = LoadThreadById(threadId);

            if (loadedThread == null)
            {
                return NotFound();
            }

            ApiThread = loadedThread;
        }

        ApiThreadId = threadId;

        return Page();
    }
}