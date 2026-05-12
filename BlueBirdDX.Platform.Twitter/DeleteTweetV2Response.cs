using System.Text.Json.Serialization;

namespace BlueBirdDX.Platform.Twitter;

public class DeleteTweetV2Response
{
    public class DeleteTweetV2ResponseInner
    {
        [JsonPropertyName("deleted")]
        public bool Deleted
        {
            get;
            set;
        }
    }

    [JsonPropertyName("data")]
    public DeleteTweetV2ResponseInner InnerData
    {
        get;
        set;
    } = null!;
}
