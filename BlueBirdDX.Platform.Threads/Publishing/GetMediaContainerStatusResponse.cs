using System.Text.Json.Serialization;
using OatmealDome.Unravel.Framework.Response;

namespace OatmealDome.Unravel.Publishing;

internal class GetMediaContainerStatusResponse : ThreadsJsonResponse
{
    [JsonPropertyName("status")]
    public string Status
    {
        get;
        set;
    }
    
    [JsonPropertyName("id")]
    public string MediaContainerId
    {
        get;
        set;
    }
    
    [JsonPropertyName("error_message")]
    public string? ErrorMessage
    {
        get;
        set;
    }
}