using System.Text.Json.Serialization;

namespace BlueBirdDX.Platform.Twitter;

internal sealed class OAuth2TokenResponse
{
    [JsonPropertyName("token_type")]
    public string TokenType
    {
        get;
        set;
    } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn
    {
        get;
        set;
    }

    [JsonPropertyName("access_token")]
    public string AccessToken
    {
        get;
        set;
    } = string.Empty;
    
    [JsonPropertyName("scope")]
    public string Scope
    {
        get;
        set;
    } = string.Empty;
    
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken
    {
        get;
        set;
    }
}
