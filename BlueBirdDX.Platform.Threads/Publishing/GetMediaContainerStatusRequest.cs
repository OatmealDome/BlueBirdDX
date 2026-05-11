using OatmealDome.Unravel.Framework.Request;

namespace OatmealDome.Unravel.Publishing;

internal class GetMediaContainerStatusRequest : ThreadsQueryRequest
{
    public override HttpMethod Method => HttpMethod.Get;
    
    public override string Endpoint => $"/v1.0/{MediaContainerId}";

    public override ThreadsRequestAuthenticationType AuthenticationType =>
        ThreadsRequestAuthenticationType.Authenticated;
    
    public string MediaContainerId
    {
        get;
        set;
    }
    
    // Hardcoded as the documentation doesn't mention any other valid values as of 10/14/2024.
    [ThreadsUrlEncodedParameterName("fields")]
    public string Fields
    {
        get;
        private set;
    } = "status,error_message";
}