using System.Text.Json.Serialization;
using BlueBirdDX.Common.Media;

namespace BlueBirdDX.WebApp.Api;

// Exposes a subset of UploadedMedia for API use.
public class UploadedMediaApi
{
    [JsonPropertyName("id")]
    public string Id
    {
        get;
        set;
    }
    
    [JsonPropertyName("name")]
    public string Name
    {
        get;
        set;
    }

    public UploadedMediaApi(UploadedMedia realMedia)
    {
        Id = realMedia._id.ToString();
        Name = realMedia.Name;
    }
}