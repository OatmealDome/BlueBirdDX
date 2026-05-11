using System.Text.Json.Serialization;
using OatmealDome.Unravel.Framework.Response;

namespace OatmealDome.Unravel.User;

internal class GetUserProfileResponse : ThreadsJsonResponse
{
    [JsonPropertyName("id")]
    public string UserId
    {
        get;
        set;
    }
    
    [JsonPropertyName("username")]
    public string Username
    {
        get;
        set;
    }
    
    [JsonPropertyName("threads_profile_picture_url")]
    public string ProfilePictureUrl
    {
        get;
        set;
    }
    
    [JsonPropertyName("threads_biography")]
    public string Biography
    {
        get;
        set;
    }
}