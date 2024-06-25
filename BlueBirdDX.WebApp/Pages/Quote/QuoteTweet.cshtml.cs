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
        // Remove the query parameters (usually just analytics stuff) and always use twitter.com as the domain
        QuotedUrl = url.Replace("x.com", "twitter.com");

        int queryParametersIdx = QuotedUrl.IndexOf('?');
        
        if (queryParametersIdx != -1)
        {
            QuotedUrl = QuotedUrl.Substring(0, queryParametersIdx);
        }

        return Page();
    }
}