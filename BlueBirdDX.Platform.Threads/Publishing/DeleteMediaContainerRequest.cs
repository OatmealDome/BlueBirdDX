using OatmealDome.Unravel.Framework.Request;

namespace OatmealDome.Unravel.Publishing;

internal class DeleteMediaContainerRequest : ThreadsQueryRequest
{
    public override HttpMethod Method => HttpMethod.Delete;
    
    public override string Endpoint => $"/v1.0/{MediaContainerId}";

    public override ThreadsRequestAuthenticationType AuthenticationType =>
        ThreadsRequestAuthenticationType.Authenticated;
    
    public string MediaContainerId
    {
        get;
        set;
    }
}
