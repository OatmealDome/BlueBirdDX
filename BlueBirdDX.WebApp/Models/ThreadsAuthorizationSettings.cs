namespace BlueBirdDX.WebApp.Models;

public class ThreadsAuthorizationSettings
{
    public ulong? AppId
    {
        get;
        set;
    }

    public string? AppSecret
    {
        get;
        set;
    }
    
    public string? RedirectUrl
    {
        get;
        set;
    }
}
