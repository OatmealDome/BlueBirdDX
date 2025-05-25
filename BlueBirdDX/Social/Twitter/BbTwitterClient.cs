using System.Text;
using System.Text.Json;
using BlueBirdDX.Common.Account;
using Tweetinvi;
using Tweetinvi.Core.Web;
using Tweetinvi.Models;
using Tweetinvi.Parameters;
using HttpMethod = Tweetinvi.Models.HttpMethod;

namespace BlueBirdDX.Social.Twitter;

public class BbTwitterClient
{
    // Optimally, we wouldn't be using Tweetinvi, but the code works and I don't want to change it.
    private readonly TwitterClient _internalClient;

    public BbTwitterClient(TwitterAccount account)
    {
        _internalClient = new TwitterClient(account.ConsumerKey, account.ConsumerSecret, account.AccessToken,
            account.AccessTokenSecret);
    }

    private async Task<string> UploadMedia_Initialize(string category, string mimeType, int fileSize)
    {
        MediaV2InitializeRequest initializeRequest = new MediaV2InitializeRequest()
        {
            Category = category,
            MimeType = mimeType,
            FileSize = fileSize
        };
        
        ITwitterResult result = await _internalClient.Execute.AdvanceRequestAsync(request =>
        {
            string json = JsonSerializer.Serialize(initializeRequest);

            request.Query.Url = "https://api.twitter.com/2/media/upload/initialize";
            request.Query.HttpMethod = HttpMethod.POST;
            request.Query.HttpContent = new StringContent(json, Encoding.UTF8, "application/json");
        });

        MediaV2InitializeResponse initializeResponse =
            JsonSerializer.Deserialize<MediaV2InitializeResponse>(result.Response.Content)!;

        return initializeResponse.InnerData.Id;
    }

    private async Task UploadMedia_Append(string mediaId, int segment, byte[] data)
    {
        await _internalClient.Execute.AdvanceRequestAsync(request =>
        {
            MultipartFormDataContent content = new MultipartFormDataContent();
            content.Add(new StringContent(segment.ToString()), "segment_index");
            content.Add(new ByteArrayContent(data), "media", "data.bin");
            
            request.Query.Url = $"https://api.twitter.com/2/media/upload/{mediaId}/append";
            request.Query.HttpMethod = HttpMethod.POST;
            request.Query.HttpContent = content;
        });
    }
    
    private async Task UploadMedia_Finalize(string mediaId)
    {
        await _internalClient.Execute.AdvanceRequestAsync(request =>
        {
            request.Query.Url = $"https://api.twitter.com/2/media/upload/{mediaId}/finalize";
            request.Query.HttpMethod = HttpMethod.POST;
        });
    }

    private async Task<MediaV2Status> UploadMedia_GetStatus(string mediaId)
    {
        ITwitterResult result = await _internalClient.Execute.AdvanceRequestAsync(request =>
        {
            request.Query.Url = $"https://api.twitter.com/2/media/upload?media_id={mediaId}&command=STATUS";
            request.Query.HttpMethod = HttpMethod.GET;
        });

        MediaV2StatusResponse statusResponse =
            JsonSerializer.Deserialize<MediaV2StatusResponse>(result.Response.Content)!;

        return statusResponse.InnerData.Status;
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
        
        await _internalClient.Execute.AdvanceRequestAsync(request =>
        {
            string json = JsonSerializer.Serialize(metadataRequest);

            request.Query.Url = "https://api.twitter.com/2/media/metadata";
            request.Query.HttpMethod = HttpMethod.POST;
            request.Query.HttpContent = new StringContent(json, Encoding.UTF8, "application/json");
        });
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

        ITwitterResult result = await _internalClient.Execute.AdvanceRequestAsync(request =>
        {
            string json = JsonSerializer.Serialize(tweetRequest);

            request.Query.Url = "https://api.twitter.com/2/tweets";
            request.Query.HttpMethod = HttpMethod.POST;
            request.Query.HttpContent = new StringContent(json, Encoding.UTF8, "application/json");
        });

        TweetV2Response tweetResponse = JsonSerializer.Deserialize<TweetV2Response>(result.Response.Content)!;

        return tweetResponse.InnerData.Id;
    }
}