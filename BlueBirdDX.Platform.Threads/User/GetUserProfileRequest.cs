using OatmealDome.Unravel.Framework.Request;

namespace OatmealDome.Unravel.User;

internal class GetUserProfileRequest : ThreadsQueryRequest
{
    public override string Endpoint => $"/v1.0/{UserId}";
    
    public override HttpMethod Method => HttpMethod.Get;

    public override ThreadsRequestAuthenticationType AuthenticationType =>
        ThreadsRequestAuthenticationType.Authenticated;

    public string UserId
    {
        get;
        set;
    }

    // Hardcode this. I don't see any downside to just fetching everything.
    [ThreadsUrlEncodedParameterName("fields")]
    public string Fields
    {
        get;
        private set;
    } = "id,username,threads_profile_picture_url, threads_biography";
}