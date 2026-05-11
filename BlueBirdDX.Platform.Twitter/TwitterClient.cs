namespace BlueBirdDX.Platform.Twitter;

public class TwitterClient
{
    public TwitterClient(string consumerKey, string consumerSecret, string accessToken, string accessTokenSecret)
    {
        //
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
