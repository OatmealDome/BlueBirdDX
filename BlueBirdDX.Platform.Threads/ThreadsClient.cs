using System.Text;
using System.Text.Json;
using System.Web;
using DotNext;
using OatmealDome.Unravel.Authentication;
using OatmealDome.Unravel.Framework.Request;
using OatmealDome.Unravel.Framework.Response;
using OatmealDome.Unravel.Publishing;
using OatmealDome.Unravel.User;

namespace OatmealDome.Unravel;

public class ThreadsClient
{
    private const string ApiBaseUrl = "https://graph.threads.net";
    private const string UserOAuthAuthorizeBaseUrl = "https://threads.net/oauth/authorize";
    
    private static readonly HttpClient SharedClient = new HttpClient();
    
    private readonly HttpClient _httpClient;

    private readonly ulong _clientId;
    private readonly string _clientSecret;

    public ThreadsCredentials? Credentials
    {
        get;
        set;
    }

    static ThreadsClient()
    {
        Version version = typeof(ThreadsClient).Assembly.GetName().Version!;

        SharedClient.DefaultRequestHeaders.Add("User-Agent",
            $"Unravel/{version.Major}.{version.Minor}.{version.Revision}");
    }

    public ThreadsClient(HttpClient httpClient, ulong clientId, string clientSecret)
    {
        _httpClient = httpClient;

        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public ThreadsClient(ulong clientId, string clientSecret) : this(SharedClient, clientId, clientSecret)
    {
        //
    }
    
    //
    // Internals
    //

    private async Task<HttpResponseMessage> SendRequest(ThreadsRequest request)
    {
        StringBuilder urlBuilder = new StringBuilder();

        urlBuilder.Append(ApiBaseUrl);

        if (request.Endpoint[0] != '/')
        {
            urlBuilder.Append('/');
        }

        urlBuilder.Append(request.Endpoint);

        FormUrlEncodedContent? urlContent = request.CreateQueryParameters();

        if (urlContent != null)
        {
            urlBuilder.Append('?');
            urlBuilder.Append(await urlContent.ReadAsStringAsync());
        }
        
        if (request.AuthenticationType == ThreadsRequestAuthenticationType.Authenticated)
        {
            if (urlContent == null)
            {
                urlBuilder.Append('?');
            }
            else
            {
                urlBuilder.Append('&');
            }
            
            urlBuilder.Append("access_token=");
            urlBuilder.Append(HttpUtility.UrlEncode(Credentials!.AccessToken));
        }

        string url = urlBuilder.ToString();

        HttpRequestMessage requestMessage = new HttpRequestMessage(request.Method, url)
        {
            Content = request.CreateHttpContent()
        };
        
        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage);
        
        if (!responseMessage.IsSuccessStatusCode)
        {
            throw new ThreadsException(await responseMessage.Content.ReadAsStringAsync());
        }

        return responseMessage;
    }

    private async Task<T> SendRequestWithJsonResponse<T>(ThreadsRequest request) where T : ThreadsJsonResponse
    {
        using HttpResponseMessage message = await SendRequest(request);

        string json = await message.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<T>(json)!;
    }

    private void VerifyCredentials()
    {
        if (Credentials == null)
        {
            throw new ThreadsException("Must authenticate to use this API endpoint");
        }

        if (Credentials.Expiry < DateTime.UtcNow)
        {
            throw new ThreadsException("Credentials have expired");
        }
    }
    
    //
    // Authentication
    //

    public string Auth_GetUserOAuthAuthorizationUrl(string redirectUri, ThreadsPermission permissions,
        string? state = null)
    {
        if ((permissions & ThreadsPermission.Basic) == 0)
        {
            throw new ThreadsException("Must request at least Basic permission");
        }

        List<string> scopeStrings = new List<string>();
        
        scopeStrings.Add("threads_basic");

        if ((permissions & ThreadsPermission.ContentPublish) != 0)
        {
            scopeStrings.Add("threads_content_publish");
        }

        if ((permissions & ThreadsPermission.ReadReplies) != 0)
        {
            scopeStrings.Add("threads_read_replies");
        }

        if ((permissions & ThreadsPermission.ManageReplies) != 0)
        {
            scopeStrings.Add("threads_manage_replies");
        }

        if ((permissions & ThreadsPermission.ManageInsights) != 0)
        {
            scopeStrings.Add("threads_manage_insights");
        }
        
        Dictionary<string, string> parameters = new Dictionary<string, string>();
        
        parameters.Add("client_id", _clientId.ToString());
        parameters.Add("redirect_uri", redirectUri);
        parameters.Add("response_type", "code");
        parameters.Add("scope", string.Join(',', scopeStrings));

        if (state != null)
        {
            parameters.Add("state", state);
        }

        FormUrlEncodedContent urlContent = new FormUrlEncodedContent(parameters);
        
        return $"{UserOAuthAuthorizeBaseUrl}?{urlContent.ReadAsStringAsync().Result}";
    }

    public async Task<ThreadsCredentials> Auth_GetShortLivedAccessToken(string code, string redirectUri)
    {
        ShortLivedAccessTokenRequest request = new ShortLivedAccessTokenRequest()
        {
            ClientId = _clientId,
            ClientSecret = _clientSecret,
            Code = code,
            GrantType = "authorization_code",
            RedirectUri = redirectUri
        };

        ShortLivedAccessTokenResponse response =
            await SendRequestWithJsonResponse<ShortLivedAccessTokenResponse>(request);

        Credentials = new ThreadsCredentials()
        {
            CredentialType = ThreadsCredentialType.ShortLived,
            AccessToken = response.AccessToken,
            Expiry = DateTime.UtcNow.AddHours(1), // https://developers.facebook.com/docs/threads/get-started
            UserId = response.UserId.ToString()
        };

        return Credentials;
    }

    public async Task<ThreadsCredentials> Auth_GetLongLivedAccessToken()
    {
        VerifyCredentials();

        if (Credentials!.CredentialType != ThreadsCredentialType.ShortLived)
        {
            throw new ThreadsException("Must have a short-lived access token");
        }

        LongLivedAccessTokenRequest request = new LongLivedAccessTokenRequest()
        {
            ClientSecret = _clientSecret
        };

        LongLivedAccessTokenResponse
            response = await SendRequestWithJsonResponse<LongLivedAccessTokenResponse>(request);

        Credentials.CredentialType = ThreadsCredentialType.LongLived;
        Credentials.AccessToken = response.AccessToken;
        Credentials.Expiry = DateTime.UtcNow.AddSeconds(response.Expiry);

        return Credentials;
    }

    public async Task<ThreadsCredentials> Auth_RefreshLongLivedAccessToken()
    {
        VerifyCredentials();

        if (Credentials!.CredentialType != ThreadsCredentialType.LongLived)
        {
            throw new ThreadsException("Must request long-lived access token first");
        }
        
        RefreshLongLivedAccessTokenRequest request = new RefreshLongLivedAccessTokenRequest();

        RefreshLongLivedAccessTokenResponse
            response = await SendRequestWithJsonResponse<RefreshLongLivedAccessTokenResponse>(request);

        Credentials.AccessToken = response.AccessToken;
        Credentials.Expiry = DateTime.UtcNow.AddSeconds(response.ExpiresIn);
        
        return Credentials;
    }
    
    //
    // Publishing
    //

    private async Task<string> Publishing_CreateMediaContainer(CreateMediaContainerRequest request)
    {
        VerifyCredentials();
        
        request.UserId = Credentials!.UserId;

        CreateMediaContainerResponse
            response = await SendRequestWithJsonResponse<CreateMediaContainerResponse>(request);

        return response.MediaContainerId;
    }

    public async Task<string> Publishing_CreateTextMediaContainer(string text, string? replyToId = null,
        string? quotedPostId = null)
    {
        return await Publishing_CreateMediaContainer(new CreateMediaContainerRequest()
        {
            MediaType = "TEXT",
            Text = text,
            ReplyToId = replyToId ?? Optional<string>.None,
            QuotedPostId = quotedPostId ?? Optional<string>.None
        });
    }

    public async Task<string> Publishing_CreateImageMediaContainer(string imageUrl, string? text = null,
        string? replyToId = null, bool isCarouselItem = false, string? altText = null, string? quotedPostId = null)
    {
        if (isCarouselItem)
        {
            if (text != null || replyToId != null || quotedPostId != null)
            {
                throw new ThreadsException("Can't set text, replyToId, or quotedPostId when creating a Carousel item");
            }
        }
        
        return await Publishing_CreateMediaContainer(new CreateMediaContainerRequest()
        {
            MediaType = "IMAGE",
            ImageUrl = imageUrl,
            AltText = altText ?? Optional<string>.None,
            Text = text ?? Optional<string>.None,
            ReplyToId = replyToId ?? Optional<string>.None,
            QuotedPostId = quotedPostId ?? Optional<string>.None,
            IsCarouselItem = isCarouselItem
        });
    }

    public async Task<string> Publishing_CreateVideoMediaContainer(string videoUrl, string? text = null,
        string? replyToId = null, bool isCarouselItem = false, string? altText = null, string? quotedPostId = null)
    {
        if (isCarouselItem)
        {
            if (text != null || replyToId != null || quotedPostId != null)
            {
                throw new ThreadsException("Can't set text, replyToId, or quotedPostId when creating a Carousel item");
            }
        }
        
        return await Publishing_CreateMediaContainer(new CreateMediaContainerRequest()
        {
            MediaType = "VIDEO",
            VideoUrl = videoUrl,
            AltText = altText ?? Optional<string>.None,
            Text = text ?? Optional<string>.None,
            ReplyToId = replyToId ?? Optional<string>.None,
            QuotedPostId = quotedPostId ?? Optional<string>.None,
            IsCarouselItem = isCarouselItem
        });
    }

    public async Task<string> Publishing_CreateCarouselMediaContainer(List<string> childrenIds, string? text = null,
        string? replyToId = null, string? quotedPostId = null)
    {
        if (childrenIds.Count == 0)
        {
            throw new ThreadsException("Must have at least one child in Carousel");
        }

        if (childrenIds.Count > 10)
        {
            throw new ThreadsException("Maximum number of children in Carousel is 10");
        }
        
        VerifyCredentials();
        
        return await Publishing_CreateMediaContainer(new CreateMediaContainerRequest()
        {
            MediaType = "CAROUSEL",
            Children = childrenIds,
            Text = text ?? Optional<string>.None,
            ReplyToId = replyToId ?? Optional<string>.None,
            QuotedPostId = quotedPostId ?? Optional<string>.None
        });
    }

    public async Task<string> Publishing_PublishMediaContainer(string mediaContainerId)
    {
        VerifyCredentials();
        
        PublishMediaContainerRequest request = new PublishMediaContainerRequest()
        {
            UserId = Credentials!.UserId,
            MediaContainerId = mediaContainerId
        };

        PublishMediaContainerResponse response =
            await SendRequestWithJsonResponse<PublishMediaContainerResponse>(request);

        return response.MediaId;
    }

    public async Task<MediaContainerState> Publishing_GetMediaContainerState(string mediaContainerId)
    {
        VerifyCredentials();

        GetMediaContainerStatusRequest request = new GetMediaContainerStatusRequest()
        {
            MediaContainerId = mediaContainerId
        };

        GetMediaContainerStatusResponse response =
            await SendRequestWithJsonResponse<GetMediaContainerStatusResponse>(request);

        MediaContainerStatus status;

        switch (response.Status)
        {
            case "EXPIRED":
                status = MediaContainerStatus.Expired;
                break;
            case "ERROR":
                status = MediaContainerStatus.Error;
                break;
            case "FINISHED":
                status = MediaContainerStatus.Finished;
                break;
            case "IN_PROGRESS":
                status = MediaContainerStatus.InProgress;
                break;
            case "PUBLISHED":
                status = MediaContainerStatus.Published;
                break;
            default:
                throw new ThreadsException($"Unsupported media container status \"{response.Status}\"");
        }

        return new MediaContainerState()
        {
            Status = status,
            ErrorMessage = response.ErrorMessage
        };
    }
    
    //
    // User
    //
    
    public async Task<ThreadsUserProfile> User_GetProfile(string userId)
    {
        VerifyCredentials();
        
        GetUserProfileRequest request = new GetUserProfileRequest()
        {
            UserId = userId
        };

        GetUserProfileResponse response = await SendRequestWithJsonResponse<GetUserProfileResponse>(request);

        return new ThreadsUserProfile(response);
    }
    
    public async Task<ThreadsUserProfile> User_GetOwnProfile()
    {
        return await User_GetProfile("me");
    }
}