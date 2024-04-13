using System.Text.Json.Serialization;

namespace BlueBirdDX.Social.Twitter;

public class TweetV2Request
{
    [JsonPropertyName("text")]
    public string Text
    {
        get;
        set;
    }

    [JsonPropertyName("media")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TweetV2RequestMedia? Media
    {
        get;
        set;
    }
}
