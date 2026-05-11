using System.Text.Json.Serialization;
using OatmealDome.Unravel.Framework.Response;

namespace OatmealDome.Unravel.Publishing;

internal class CreateMediaContainerResponse : ThreadsJsonResponse
{
    [JsonPropertyName("id")]
    public string MediaContainerId
    {
        get;
        set;
    }
}