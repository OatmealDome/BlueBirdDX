using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBirdDX.WebApp.Pages.Quote;

public class QuoteTweetModel : PageModel
{
    public string QuotedUrl
    {
        get;
        set;
    }
    
    public IActionResult OnGet(string url)
    {
        QuotedUrl = url;

        return Page();
    }
}