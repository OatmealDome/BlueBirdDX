namespace BlueBirdDX.WebApp.Models;

public class TwitterAuthorizationSettings
{
    public string ClientId
    {
        get;
        set;
    } = string.Empty;

    public string ClientSecret
    {
        get;
        set;
    } = string.Empty;

    public string RedirectUrl
    {
        get;
        set;
    } = string.Empty;
}
