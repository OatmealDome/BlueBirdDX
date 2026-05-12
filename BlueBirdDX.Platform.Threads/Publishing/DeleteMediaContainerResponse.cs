using System.Text.Json.Serialization;
using OatmealDome.Unravel.Framework.Response;

namespace OatmealDome.Unravel.Publishing;

internal class DeleteMediaContainerResponse : ThreadsJsonResponse
{
    [JsonPropertyName("success")]
    public bool Success
    {
        get;
        set;
    }

    [JsonPropertyName("deleted_id")]
    public string Id
    {
        get;
        set;
    }
}

