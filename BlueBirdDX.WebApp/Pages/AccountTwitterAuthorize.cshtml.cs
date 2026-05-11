using BlueBirdDX.Common.Account;
using BlueBirdDX.WebApp.Models;
using BlueBirdDX.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.WebApp.Pages;

public class AccountTwitterAuthorize : PageModel
{
    private readonly TwitterAuthorizationService _twitterAuthorizationService;

    public readonly IMongoCollection<AccountGroup> AccountGroupCollection;

    public AccountTwitterAuthorize(SlabMongoService mongoService,
        TwitterAuthorizationService twitterAuthorizationService)
    {
        _twitterAuthorizationService = twitterAuthorizationService;

        AccountGroupCollection = mongoService.GetCollection<AccountGroup>("accounts");
    }

    public void OnGet()
    {
        //
    }

    public async Task<IActionResult> OnPostAsync(string groupId)
    {
        if (!ObjectId.TryParse(groupId, out ObjectId groupIdObj))
        {
            return BadRequest("Invalid group id");
        }

        AccountGroup? group = AccountGroupCollection.AsQueryable().FirstOrDefault(g => g._id == groupIdObj);

        if (group == null)
        {
            return BadRequest("Invalid group ID");
        }

        string url = await _twitterAuthorizationService.CreateAuthorizationAttemptUrl(groupId);
        
        return Redirect(url);
    }
}
