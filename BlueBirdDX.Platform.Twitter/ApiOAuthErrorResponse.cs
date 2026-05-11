using System.Text.Json.Serialization;

namespace BlueBirdDX.Platform.Twitter;

public class ApiOAuthErrorResponse
{
    [JsonPropertyName("error")]
    public string Error
    {
        get;
        set;
    } = string.Empty;
    
    [JsonPropertyName("error_description")]
    public string Description
    {
        get;
        set;
    } = string.Empty;
}
