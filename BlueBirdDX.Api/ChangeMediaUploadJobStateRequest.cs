using System.Text.Json.Serialization;

namespace BlueBirdDX.Api;

public class ChangeMediaUploadJobStateRequest
{
    [JsonPropertyName("state")]
    public int State
    {
        get;
        set;
    }
}