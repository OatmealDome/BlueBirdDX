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

    public UploadedMediaApi(UploadedMedia realMedia)
    {
        Id = realMedia._id.ToString();
        Name = realMedia.Name;
        AltText = realMedia.AltText;
    }

    public void TransferToNormal(UploadedMedia realMedia)
    {
        realMedia.Name = Name;
        realMedia.AltText = AltText;
    }
}