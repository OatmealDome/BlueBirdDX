using System.Text.Json.Serialization;
using OatmealDome.Unravel.Framework.Response;

namespace OatmealDome.Unravel.Authentication;

internal class RefreshLongLivedAccessTokenResponse : ThreadsJsonResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken
    {
        get;
        set;
    }

    [JsonPropertyName("token_type")]
    public string TokenType
    {
        get;
        set;
    }

    [JsonPropertyName("expires_in")]
    public long ExpiresIn
    {
        get;
        set;
    }
}