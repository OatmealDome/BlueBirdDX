using System.Text.Json.Serialization;

namespace BlueBirdDX.Platform.Twitter;

public class MediaV2UploadSingleShotResponse
{
    public class MediaV2UploadSingleShotResponseInner
    {
        [JsonPropertyName("id")]
        public string Id
        {
            get;
            set;
        }
    }

    [JsonPropertyName("data")]
    public MediaV2UploadSingleShotResponseInner InnerData
    {
        get;
        set;
    }
}
