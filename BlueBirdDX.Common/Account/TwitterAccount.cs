namespace BlueBirdDX.Common.Account;

public class TwitterAccount
{
    public bool Premium
    {
        get;
        set;
    } = false;

    public string RefreshToken
    {
        get;
        set;
    } = string.Empty;
}
