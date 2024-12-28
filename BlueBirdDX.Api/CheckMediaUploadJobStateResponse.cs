using System.Text.Json.Serialization;

namespace BlueBirdDX.Api;

public class CheckMediaUploadJobStateResponse
{
    [JsonPropertyName("state")]
    public int State
    {
        get;
        set;
    }
    
    [JsonPropertyName("media_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaId
    {
        get;
        set;
    }

    [JsonPropertyName("error_detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorDetail
    {
        get;
        set;
    }

    public bool IsProcessingFinished()
    {
        return State == 3 || State == 4;
    }

    public bool IsSuccess()
    {
        return State == 3;
    }

    public bool IsFailure()
    {
        return State == 4;
    }
}