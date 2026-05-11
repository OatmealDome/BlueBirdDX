using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace BlueBirdDX.Platform.Twitter;

public class TwitterClient
{
    private const string ApiBaseUrl = "https://api.x.com/2";

    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly HttpClient _httpClient;

    public string? RefreshToken
    {
        get;
        private set;
    }
    
    public string? AccessToken
    {
        get;
        private set;
    }

    public DateTime? AccessTokenExpiry
    {
        get;
        private set;
    }

    public TwitterClient(string clientId, string clientSecret)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"BlueBirdDX/1.0.0");
    }

    private async Task<HttpResponseMessage> SendRequestToOAuth2Endpoint(string endpoint,
        Dictionary<string, string> contentDict)
    {
        using HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}{endpoint}");
        requestMessage.Content = new FormUrlEncodedContent(contentDict);

        string authHeaderContent = $"{_clientId}:{_clientSecret}";
        string authHeaderContentEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(authHeaderContent));
        
        requestMessage.Headers.Add("Authorization", $"Basic {authHeaderContentEncoded}");

        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage);

        if (!responseMessage.IsSuccessStatusCode)
        {
            Uri requestUri = requestMessage.RequestUri!;
            HttpStatusCode httpStatus = responseMessage.StatusCode;
            string json = await responseMessage.Content.ReadAsStringAsync();
            ApiOAuthErrorResponse? errorResponse = JsonSerializer.Deserialize<ApiOAuthErrorResponse>(json);
            
            responseMessage.Dispose();

            if (errorResponse == null)
            {
                throw new TwitterException($"OAuth request to {requestUri} returned {httpStatus}, no error content available");
            }

            throw new TwitterException(
                $"OAuth request to {requestUri} returned {httpStatus} with error {errorResponse.Error} and description {errorResponse.Description}");
        }

        return responseMessage;
    }

    private async Task<HttpResponseMessage> SendRequestToNormalEndpoint(HttpMethod method, string endpoint,
        HttpContent content)
    {
        if (AccessToken == null)
        {
            throw new TwitterException("Attempting to use authenticated API endpoint when unauthenticated");
        }

        if (AccessTokenExpiry == null || AccessTokenExpiry.Value < DateTime.UtcNow)
        {
            throw new TwitterException("Access token is expired");
        }
        
        using HttpRequestMessage requestMessage = new HttpRequestMessage(method, $"{ApiBaseUrl}{endpoint}");
        requestMessage.Content = content;
        
        requestMessage.Headers.Add("Authorization", $"Bearer {AccessToken}");
        
        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage);

        if (!responseMessage.IsSuccessStatusCode)
        {
            Uri requestUri = requestMessage.RequestUri!;
            HttpStatusCode httpStatus = responseMessage.StatusCode;
            string json = await responseMessage.Content.ReadAsStringAsync();
            ApiErrorResponse? errorResponse = JsonSerializer.Deserialize<ApiErrorResponse>(json);
            
            responseMessage.Dispose();

            if (errorResponse == null)
            {
                throw new TwitterException($"{requestUri} returned {httpStatus}, no error content available");
            }

            throw new TwitterException(
                $"{requestUri} returned {httpStatus} with error {errorResponse.Title} and detail {errorResponse.Detail}");
        }

        return responseMessage;
    }

    private async Task Login(Dictionary<string, string> urlParameters)
    {
        using HttpResponseMessage responseMessage = await SendRequestToOAuth2Endpoint("/oauth2/token", urlParameters);

        OAuth2TokenResponse tokenResponse = (await responseMessage.Content.ReadFromJsonAsync<OAuth2TokenResponse>())!;
        AccessToken = tokenResponse.AccessToken;
        AccessTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
        RefreshToken = tokenResponse.RefreshToken;
    }

    public Task Login(string refreshToken)
    {
        Dictionary<string, string> urlParameters = new Dictionary<string, string>()
        {
            { "refresh_token", refreshToken },
            { "grant_type", "refresh_token" },
        };

        return Login(urlParameters);
    }

    public Task LoginWithCodeAndVerifier(string code, string verifier, string redirectUrl)
    {
        Dictionary<string, string> urlParameters = new Dictionary<string, string>()
        {
            { "code", code },
            { "grant_type", "authorization_code" },
            { "redirect_uri", redirectUrl },
            { "code_verifier", verifier },
        };
        
        return Login(urlParameters);
    }

    private async Task<string> UploadMedia_Initialize(string category, string mimeType, int fileSize)
    {
        MediaV2InitializeRequest initializeRequest = new MediaV2InitializeRequest()
        {
            Category = category,
            MimeType = mimeType,
            FileSize = fileSize
        };
        
        throw new NotImplementedException();
    }

    private async Task UploadMedia_Append(string mediaId, int segment, byte[] data)
    {
        throw new NotImplementedException();
    }
    
    private async Task UploadMedia_Finalize(string mediaId)
    {
        throw new NotImplementedException();
    }

    private async Task<MediaV2Status> UploadMedia_GetStatus(string mediaId)
    {
        throw new NotImplementedException();
    }

    private async Task UploadMedia_SetMetadata(string mediaId, string altText)
    {
        MediaV2SetMetadataRequest metadataRequest = new MediaV2SetMetadataRequest()
        {
            MediaId = mediaId,
            Metadata = new MediaV2SetMetadataRequest.MediaV2Metadata()
            {
                AltText = new MediaV2SetMetadataRequest.MediaV2Metadata.MediaV2MetadataAltText()
                {
                    Text = altText
                }
            }
        };
        
        throw new NotImplementedException();
    }

    private async Task<string> UploadMedia(string category, string mimeType, byte[] data, string? altText = null)
    {
        string mediaId = await UploadMedia_Initialize(category, mimeType, data.Length);
        
        using Stream memoryStream = new MemoryStream(data);

        const int bufSize = 1048576 * 4; // 4 MiB (the maximum chunk size is 5 MiB, but we're leaving a margin)
        byte[] buf = new byte[bufSize];
        
        int readLength = 0;
        
        int segment = 0;
        
        while ((readLength = memoryStream.Read(buf)) > 0)
        {
            byte[] segmentData;

            if (readLength == bufSize)
            {
                segmentData = buf;
            }
            else
            {
                segmentData = new byte[readLength];
                Array.Copy(buf, segmentData, readLength);
            }
            
            await UploadMedia_Append(mediaId, segment, segmentData);
            
            segment++;
        }

        await UploadMedia_Finalize(mediaId);

        if (category.Contains("video"))
        {
            string state;

            do
            {
                MediaV2Status status = await UploadMedia_GetStatus(mediaId);

                if (status.State == "failed")
                {
                    throw new Exception("Media upload failed");
                }
            
                state = status.State;

                await Task.Delay(TimeSpan.FromSeconds(status.CheckAfter));
            } while (state != "succeeded");
        }
        
        if (!string.IsNullOrWhiteSpace(altText))
        {
            await UploadMedia_SetMetadata(mediaId, altText);
        }

        return mediaId;
    }

    public async Task<string> UploadImage(byte[] image, string mimeType, string? altText = null)
    {
        return await UploadMedia("tweet_image", mimeType, image, altText);
    }
    
    public async Task<string> UploadVideo(byte[] image, string mimeType, string? altText = null)
    {
        return await UploadMedia("amplify_video", mimeType, image, altText);
    }
    
    public async Task<string> Tweet(string text, string? quotedTweetId = null, string? replyToTweetId = null, string[]? mediaIds = null)
    {
        TweetV2RequestMedia? tweetRequestMedia = null;

        if (mediaIds != null)
        {
            tweetRequestMedia = new TweetV2RequestMedia()
            {
                MediaIds = mediaIds
            };
        }

        TweetV2RequestReply? tweetRequestReply = null;

        if (replyToTweetId != null)
        {
            tweetRequestReply = new TweetV2RequestReply()
            {
                InReplyToTweetId = replyToTweetId
            };
        }

        TweetV2Request tweetRequest = new TweetV2Request()
        {
            Text = text,
            Media = tweetRequestMedia,
            Reply = tweetRequestReply,
            QuotedTweetId = quotedTweetId
        };
        
        throw new NotImplementedException();
    }
}
