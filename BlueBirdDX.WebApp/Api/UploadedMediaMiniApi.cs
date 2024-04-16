using System.Text.Json.Serialization;
using BlueBirdDX.Common.Media;

namespace BlueBirdDX.WebApp.Api;

// Exposes an even smaller subset of UploadedMedia for API use.
public class UploadedMediaMiniApi
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

    public UploadedMediaMiniApi(UploadedMedia realMedia)
    {
        Id = realMedia._id.ToString();
        Name = realMedia.Name;
    }
}