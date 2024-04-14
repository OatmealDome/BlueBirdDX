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

    public PostThreadItemApi()
    {
        Text = "";
    }
    
    public PostThreadItemApi(PostThreadItem realItem)
    {
        Text = realItem.Text;
    }
}