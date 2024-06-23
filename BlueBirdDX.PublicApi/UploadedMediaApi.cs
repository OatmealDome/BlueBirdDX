using System.Text.Json.Serialization;

namespace BlueBirdDX.PublicApi;

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

    [JsonPropertyName("alt_text")]
    public string AltText
    {
        get;
        set;
    }

    public UploadedMediaApi()
    {
        Id = "";
        Name = "";
        AltText = "";
    }
}