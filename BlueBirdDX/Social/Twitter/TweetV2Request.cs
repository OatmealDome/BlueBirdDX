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
    
    [JsonPropertyName("reply")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TweetV2RequestReply? Reply
    {
        get;
        set;
    }
    
    [JsonPropertyName("quote_tweet_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? QuotedTweetId
    {
        get;
        set;
    }
}
