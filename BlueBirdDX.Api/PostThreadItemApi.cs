using System.Text.Json.Serialization;

namespace BlueBirdDX.Api;

public class PostThreadItemApi
{
    [JsonPropertyName("text")]
    public string Text
    {
        get;
        set;
    }

    [JsonPropertyName("attached_media")]
    public List<string> AttachedMedia
    {
        get;
        set;
    }

    [JsonPropertyName("quoted_post")]
    public string? QuotedPost
    {
        get;
        set;
    }

    public PostThreadItemApi()
    {
        Text = "";
        AttachedMedia = new List<string>();
        QuotedPost = null;
    }
}