using System.Text.Json.Serialization;

namespace BlueBirdDX.Social.Twitter;

public class MediaV2SetMetadataRequest
{
    public class MediaV2Metadata
    {
        public class MediaV2MetadataAltText
        {
            [JsonPropertyName("text")]
            public string Text
            {
                get;
                set;
            }
        }

        [JsonPropertyName("alt_text")]
        public MediaV2MetadataAltText AltText
        {
            get;
            set;
        }
    }

    [JsonPropertyName("id")]
    public string MediaId
    {
        get;
        set;
    }

    [JsonPropertyName("metadata")]
    public MediaV2Metadata Metadata
    {
        get;
        set;
    }
}