using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Post;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.WebApp.Pages;

public class IndexModel : PageModel
{
    public readonly IMongoCollection<AccountGroup> AccountGroupCollection;
    public readonly IMongoCollection<PostThread> PostThreadCollection;

    public IndexModel(SlabMongoService mongoService)
    {
        AccountGroupCollection = mongoService.GetCollection<AccountGroup>("accounts");
        PostThreadCollection = mongoService.GetCollection<PostThread>("threads");
    }

    public void OnGet()
    {
        //
    }
}
