using System.Text.Json.Serialization;

namespace BlueBirdDX.WebApp.Api;

public class UploadedMediaUrlResponse
{
    [JsonPropertyName("url")]
    public string Url
    {
        get;
        set;
    }
}