using System.Text.Json.Serialization;
using BlueBirdDX.Common.Post;

namespace BlueBirdDX.WebApp.Api;

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

    public PostThreadItemApi()
    {
        Text = "";
        AttachedMedia = new List<string>();
    }
    
    public PostThreadItemApi(PostThreadItem realItem)
    {
        Text = realItem.Text;
        AttachedMedia = realItem.AttachedMedia.Select(m => m.ToString()).ToList();
    }
}