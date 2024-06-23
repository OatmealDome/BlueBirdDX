namespace BlueBirdDX.Common.Account;

public class ThreadsAccount
{
    public ulong ClientId
    {
        get;
        set;
    }

    public string ClientSecret
    {
        get;
        set;
    }
    
    public string AccessToken
    {
        get;
        set;
    } = "";

    public string UserId
    {
        get;
        set;
    } = "";

    public DateTime Expiry
    {
        get;
        set;
    }

    public ThreadsAccount()
    {
        //
    }
}