using System.Text.Json.Serialization;

namespace BlueBirdDX.Social.Twitter;

public class MediaV2StatusResponse
{
    public class MediaV2StatusResponseInner
    {
        [JsonPropertyName("processing_info")]
        public MediaV2Status Status
        {
            get;
            set;
        }
    }

    [JsonPropertyName("data")]
    public MediaV2StatusResponseInner InnerData
    {
        get;
        set;
    }
}