using System.Text.Json.Serialization;

namespace BlueBirdDX.Social.Twitter;

public class TweetV2RequestReply
{
    [JsonPropertyName("in_reply_to_tweet_id")]
    public string InReplyToTweetId
    {
        get;
        set;
    }
}