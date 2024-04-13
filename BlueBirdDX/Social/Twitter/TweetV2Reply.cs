using System.Text.Json.Serialization;

namespace BlueBirdDX.Social.Twitter;

public class TweetV2Reply
{
    public class TweetV2ReplyInner
    {
        [JsonPropertyName("id")]
        public string Id
        {
            get;
            set;
        }

        [JsonPropertyName("text")]
        public string Text
        {
            get;
            set;
        }
    }

    [JsonPropertyName("data")]
    public TweetV2ReplyInner InnerData
    {
        get;
        set;
    }
}