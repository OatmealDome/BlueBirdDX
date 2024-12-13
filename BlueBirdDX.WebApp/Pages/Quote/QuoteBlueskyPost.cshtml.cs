using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlueBirdDX.WebApp.Pages.Quote;

public class QuoteBlueskyPostModel : PageModel
{
    public string Uri
    {
        get;
        set;
    }

    public string Cid
    {
        get;
        set;
    }
    
    public IActionResult OnGet(string uri, string cid)
    {
        Uri = uri;
        Cid = cid;
        
        return Page();
    }
}