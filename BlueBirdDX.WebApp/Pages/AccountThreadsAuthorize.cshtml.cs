using BlueBirdDX.Common.Account;
using BlueBirdDX.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.WebApp.Pages;

public class AccountThreadsAuthorize : PageModel
{
    private readonly SocialAppAuthorizationService _authorizationService;

    public readonly IMongoCollection<AccountGroup> AccountGroupCollection;

    public AccountThreadsAuthorize(SlabMongoService mongoService,
        SocialAppAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;

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
            return BadRequest("Invalid group ID");
        }

        AccountGroup? group = AccountGroupCollection.AsQueryable().FirstOrDefault(g => g._id == groupIdObj);

        if (group == null)
        {
            return BadRequest("Invalid group ID");
        }

        string url = await _authorizationService.CreateThreadsAuthorizationAttemptUrl(groupId);
        
        return Redirect(url);
    }
}
