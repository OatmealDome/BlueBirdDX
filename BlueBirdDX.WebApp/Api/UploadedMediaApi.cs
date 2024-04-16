using System.Text.Json.Serialization;
using BlueBirdDX.Common.Media;

namespace BlueBirdDX.WebApp.Api;

// Exposes a subset of UploadedMedia for API use.
public class UploadedMediaApi
{
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
        Name = "";
        AltText = "";
    }

    public UploadedMediaApi(UploadedMedia realMedia)
    {
        Name = realMedia.Name;
        AltText = realMedia.AltText;
    }

    public void TransferToNormal(UploadedMedia realMedia)
    {
        realMedia.Name = Name;
        realMedia.AltText = AltText;
    }
}