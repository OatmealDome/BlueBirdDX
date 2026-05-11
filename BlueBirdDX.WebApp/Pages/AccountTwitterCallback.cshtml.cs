using BlueBirdDX.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBirdDX.WebApp.Pages;

public class AccountTwitterCallback : PageModel
{
    private readonly TwitterAuthorizationService _twitterAuthorizationService;

    public string AuthorizationResult
    {
        get;
        private set;
    } = string.Empty;

    public AccountTwitterCallback(TwitterAuthorizationService twitterAuthorizationService)
    {
        _twitterAuthorizationService = twitterAuthorizationService;
    }

    public async Task<IActionResult> OnGet(string state, string code, string? error = null)
    {
        if (error == null)
        {
            try
            {
                await _twitterAuthorizationService.AuthorizeByCallback(state, code);

                AuthorizationResult = "OK";
            }
            catch (Exception e)
            {
                AuthorizationResult = e.ToString();
            }
        }
        else
        {
            AuthorizationResult = $"Twitter returned error \"{error}\"";
        }

        return Page();
    }
}
