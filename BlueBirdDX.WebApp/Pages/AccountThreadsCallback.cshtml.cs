using BlueBirdDX.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBirdDX.WebApp.Pages;

public class AccountThreadsCallback : PageModel
{
    private readonly ThreadsAuthorizationService _threadsAuthorizationService;

    public string AuthorizationResult
    {
        get;
        private set;
    } = string.Empty;

    public AccountThreadsCallback(ThreadsAuthorizationService threadsAuthorizationService)
    {
        _threadsAuthorizationService = threadsAuthorizationService;
    }

    public async Task<IActionResult> OnGet(string state, string code, string? error = null,
        [FromQuery(Name = "error_reason")] string? errorReason = null,
        [FromQuery(Name = "error_description")] string? errorDescription = null)
    {
        if (error == null)
        {
            try
            {
                await _threadsAuthorizationService.AuthorizeByCallback(state, code);

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
