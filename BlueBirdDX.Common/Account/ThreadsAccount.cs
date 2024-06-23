namespace BlueBirdDX.Common.Account;

public class ThreadsAccount
{
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