using DotNext;
using OatmealDome.Unravel.Framework.Request;

namespace OatmealDome.Unravel.Publishing;

internal class CreateMediaContainerRequest : ThreadsQueryRequest
{
    public override HttpMethod Method => HttpMethod.Post;
    
    public override string Endpoint => $"/v1.0/{UserId}/threads";

    public override ThreadsRequestAuthenticationType AuthenticationType =>
        ThreadsRequestAuthenticationType.Authenticated;

    public string UserId
    {
        get;
        set;
    }

    [ThreadsUrlEncodedParameterName("media_type")]
    public string MediaType
    {
        get;
        set;
    }

    [ThreadsUrlEncodedParameterName("text")]
    public Optional<string> Text
    {
        get;
        set;
    }

    [ThreadsUrlEncodedParameterName("image_url")]
    public Optional<string> ImageUrl
    {
        get;
        set;
    }

    [ThreadsUrlEncodedParameterName("video_url")]
    public Optional<string> VideoUrl
    {
        get;
        set;
    }

    [ThreadsUrlEncodedParameterName("alt_text")]
    public Optional<string> AltText
    {
        get;
        set;
    }

    [ThreadsUrlEncodedParameterName("is_carousel_item")]
    public Optional<bool> IsCarouselItem
    {
        get;
        set;
    }

    public Optional<List<string>> Children
    {
        get;
        set;
    }

    // Bit of a hack...
    [ThreadsUrlEncodedParameterName("children")]
    public Optional<string> ChildrenCommaSeparated =>
        Children.IsUndefined ? Optional<string>.None : string.Join(',', Children.Value);

    [ThreadsUrlEncodedParameterName("reply_to_id")]
    public Optional<string> ReplyToId
    {
        get;
        set;
    }

    [ThreadsUrlEncodedParameterName("reply_control")]
    public Optional<string> ReplyControl
    {
        get;
        set;
    }

    [ThreadsUrlEncodedParameterName("quote_post_id")]
    public Optional<string> QuotedPostId
    {
        get;
        set;
    }
}