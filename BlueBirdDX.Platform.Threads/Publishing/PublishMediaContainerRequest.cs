using OatmealDome.Unravel.Framework.Request;

namespace OatmealDome.Unravel.Publishing;

internal class PublishMediaContainerRequest : ThreadsQueryRequest
{
    public override HttpMethod Method => HttpMethod.Post;
    
    public override string Endpoint => $"/v1.0/{UserId}/threads_publish";

    public override ThreadsRequestAuthenticationType AuthenticationType =>
        ThreadsRequestAuthenticationType.Authenticated;

    public string UserId
    {
        get;
        set;
    }

    [ThreadsUrlEncodedParameterName("creation_id")]
    public string MediaContainerId
    {
        get;
        set;
    }
}