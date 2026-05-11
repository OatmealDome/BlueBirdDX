namespace OatmealDome.Unravel.Authentication;

public class ThreadsCredentials
{
    public ThreadsCredentialType CredentialType
    {
        get;
        set;
    }

    public string AccessToken
    {
        get;
        set;
    }

    public DateTime Expiry
    {
        get;
        set;
    }

    public string UserId
    {
        get;
        set;
    }
}