namespace OatmealDome.Unravel.User;

public class ThreadsUserProfile
{
    public string UserId
    {
        get;
        set;
    }
    
    public string Username
    {
        get;
        set;
    }
    
    public string ProfilePictureUrl
    {
        get;
        set;
    }
    
    public string Biography
    {
        get;
        set;
    }

    public ThreadsUserProfile()
    {
        UserId = "";
        Username = "";
        ProfilePictureUrl = "";
        Biography = "";
    }
    
    internal ThreadsUserProfile(GetUserProfileResponse response)
    {
        UserId = response.UserId;
        Username = response.Username;
        ProfilePictureUrl = response.ProfilePictureUrl;
        Biography = response.Biography;
    }
}