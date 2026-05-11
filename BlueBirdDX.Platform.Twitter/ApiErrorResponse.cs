using System.Text.Json.Serialization;

namespace BlueBirdDX.Platform.Twitter;

public class ApiErrorResponse
{
    [JsonPropertyName("title")]
    public string Title
    {
        get;
        set;
    } = string.Empty;
    
    [JsonPropertyName("detail")]
    public string Detail
    {
        get;
        set;
    } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type
    {
        get;
        set;
    } = string.Empty;
}
