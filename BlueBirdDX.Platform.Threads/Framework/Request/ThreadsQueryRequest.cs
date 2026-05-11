namespace OatmealDome.Unravel.Framework.Request;

internal abstract class ThreadsQueryRequest : ThreadsUrlEncodedContentRequest
{
    public override HttpContent? CreateHttpContent()
    {
        return null;
    }

    public override FormUrlEncodedContent? CreateQueryParameters()
    {
        return CreateFormUrlEncodedContent();
    }
}