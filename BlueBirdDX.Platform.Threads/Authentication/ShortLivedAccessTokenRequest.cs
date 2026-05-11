using OatmealDome.Unravel.Framework.Request;

namespace OatmealDome.Unravel.Authentication;

internal class ShortLivedAccessTokenRequest : ThreadsFormRequest
{
    public override string Endpoint => "/oauth/access_token";

    public override ThreadsRequestAuthenticationType AuthenticationType => ThreadsRequestAuthenticationType.Unauthenticated;

    [ThreadsUrlEncodedParameterName("client_id")]
    public ulong ClientId
    {
        get;
        set;
    }

    [ThreadsUrlEncodedParameterName("client_secret")]
    public string ClientSecret
    {
        get;
        set;
    }

    [ThreadsUrlEncodedParameterName("code")]
    public string Code
    {
        get;
        set;
    }

    [ThreadsUrlEncodedParameterName("grant_type")]
    public string GrantType
    {
        get;
        set;
    }
    
    [ThreadsUrlEncodedParameterName("redirect_uri")]
    public string RedirectUri
    {
        get;
        set;
    }
}