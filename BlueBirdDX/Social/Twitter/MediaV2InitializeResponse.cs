using System.Text.Json.Serialization;

namespace BlueBirdDX.Social.Twitter;

public class MediaV2InitializeResponse
{
    public class MediaV2InitializeResponseInner
    {
        [JsonPropertyName("id")]
        public string Id
        {
            get;
            set;
        }
    }

    [JsonPropertyName("data")]
    public MediaV2InitializeResponseInner InnerData
    {
        get;
        set;
    }
}