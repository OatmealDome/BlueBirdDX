namespace OatmealDome.Unravel.Framework.Request;

internal abstract class ThreadsRequest
{
    public abstract string Endpoint
    {
        get;
    }

    public abstract HttpMethod Method
    {
        get;
    }

    public abstract ThreadsRequestAuthenticationType AuthenticationType
    {
        get;
    }

    public abstract HttpContent? CreateHttpContent();

    public abstract FormUrlEncodedContent? CreateQueryParameters();
}