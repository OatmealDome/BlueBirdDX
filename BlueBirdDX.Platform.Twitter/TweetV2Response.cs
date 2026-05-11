using System.Text.Json.Serialization;

namespace BlueBirdDX.Platform.Twitter;

public class TweetV2Response
{
    public class TweetV2ResponseInner
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
    public TweetV2ResponseInner InnerData
    {
        get;
        set;
    }
}
