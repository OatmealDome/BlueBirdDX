using System.Text.Json.Serialization;

namespace BlueBirdDX.Api;

public class CreateMediaUploadJobResponse
{
    [JsonPropertyName("id")]
    public string Id
    {
        get;
        set;
    }

    [JsonPropertyName("target_url")]
    public string TargetUrl
    {
        get;
        set;
    }
}