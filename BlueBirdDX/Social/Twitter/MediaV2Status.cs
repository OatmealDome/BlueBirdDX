using System.Text.Json.Serialization;

namespace BlueBirdDX.Social.Twitter;

public class MediaV2Status
{
    [JsonPropertyName("check_after_secs")]
    public int CheckAfter
    {
        get;
        set;
    }

    [JsonPropertyName("progress_percent")]
    public int Progress
    {
        get;
        set;
    }

    [JsonPropertyName("state")]
    public string State
    {
        get;
        set;
    } = string.Empty;
}