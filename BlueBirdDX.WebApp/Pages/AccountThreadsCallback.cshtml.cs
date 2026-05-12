using BlueBirdDX.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBirdDX.WebApp.Pages;

public class AccountThreadsCallback : PageModel
{
    private readonly SocialAppAuthorizationService _authorizationService;

    public string AuthorizationResult
    {
        get;
        private set;
    } = string.Empty;

    public AccountThreadsCallback(SocialAppAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task<IActionResult> OnGet(string state, string code, string? error = null,
        [FromQuery(Name = "error_reason")] string? errorReason = null,
        [FromQuery(Name = "error_description")] string? errorDescription = null)
    {
        if (error == null)
        {
            try
            {
                await _authorizationService.AuthorizeThreadsByCallback(state, code);

                AuthorizationResult = "OK";
            }
            catch (Exception e)
            {
                AuthorizationResult = e.ToString();
            }
        }
        else
        {
            AuthorizationResult =
                $"Threads returned error \"{error}\", reason \"{errorReason}\", description \"{errorDescription}\"";
        }

        return Page();
    }
}
