using System.Text.Json.Serialization;

namespace BlueBirdDX.Common.Util.TextWrapper;

public class ExtractedChunk
{
    [JsonPropertyName("type")]
    public ExtractedChunkType ChunkType
    {
        get;
        set;
    }

    [JsonPropertyName("value")]
    public string Value
    {
        get;
        set;
    } = string.Empty;
}
