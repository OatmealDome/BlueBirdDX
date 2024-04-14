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
    
    public PostThreadApi ApiThread
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

            ApiThread = new PostThreadApi(realThread);
        }

        return Page();
    }
}