using System.Text.Json.Serialization;

namespace BlueBirdDX.Common.Util.TextWrapper;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExtractedChunkType
{
    Text,
    Url,
    Hashtag,
    Mention,
    Cashtag,
    Unknown,
}
