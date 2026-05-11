using System.Text.Json.Serialization;
using OatmealDome.Unravel.Framework.Response;

namespace OatmealDome.Unravel.Publishing;

internal class PublishMediaContainerResponse : ThreadsJsonResponse
{
    [JsonPropertyName("id")]
    public string MediaId
    {
        get;
        set;
    }
}