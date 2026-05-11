using OatmealDome.Unravel.Framework.Request;

namespace OatmealDome.Unravel.Authentication;

internal class LongLivedAccessTokenRequest : ThreadsQueryRequest
{
    public override HttpMethod Method => HttpMethod.Get;
    
    public override string Endpoint => "/access_token";

    public override ThreadsRequestAuthenticationType AuthenticationType =>
        ThreadsRequestAuthenticationType.Authenticated;

    [ThreadsUrlEncodedParameterName("grant_type")]
    public string GrantType
    {
        get;
        private set;
    } = "th_exchange_token";

    [ThreadsUrlEncodedParameterName("client_secret")]
    public string ClientSecret
    {
        get;
        set;
    }
}