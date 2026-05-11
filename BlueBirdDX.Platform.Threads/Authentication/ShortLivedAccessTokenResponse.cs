using System.Text.Json.Serialization;
using OatmealDome.Unravel.Framework.Response;

namespace OatmealDome.Unravel.Authentication;

internal class ShortLivedAccessTokenResponse : ThreadsJsonResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken
    {
        get;
        set;
    }

    [JsonPropertyName("user_id")]
    public ulong UserId
    {
        get;
        set;
    }
}