namespace BlueBirdDX.WebApp.Models;

public class TwitterAuthorizationState
{
    public required string Id
    {
        get;
        set;
    }

    public required string Verifier
    {
        get;
        set;
    }

    public required string Challenge
    {
        get;
        set;
    }

    public required string GroupId
    {
        get;
        set;
    }
}
