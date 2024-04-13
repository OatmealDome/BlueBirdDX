using System.Text.Json.Serialization;

namespace BlueBirdDX.Social.Twitter;

public class TweetV2RequestMedia
{
    [JsonPropertyName("media_ids")]
    public string[] MediaIds
    {
        get;
        set;
    }
}