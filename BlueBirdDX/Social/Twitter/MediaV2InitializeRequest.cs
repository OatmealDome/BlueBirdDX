using System.Text.Json.Serialization;

namespace BlueBirdDX.Social.Twitter;

public class MediaV2InitializeRequest
{
    [JsonPropertyName("media_category")]
    public string Category
    {
        get;
        set;
    }

    [JsonPropertyName("media_type")]
    public string MimeType
    {
        get;
        set;
    }
    
    [JsonPropertyName("total_bytes")]
    public int FileSize
    {
        get;
        set;
    }
}