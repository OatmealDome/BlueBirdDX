using System.Text;
using System.Text.Json;
using BlueBirdDX.Common.Account;
using Tweetinvi;
using Tweetinvi.Models;
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

    public async Task<string> UploadImage(byte[] image)
    {
        IMedia media = await _internalClient.Upload.UploadTweetImageAsync(image);

        return media.Id.ToString()!;
    }
    
    public async Task Tweet(string text, string? replyToTweetId = null, string[]? mediaIds = null)
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
            Reply = tweetRequestReply
        };

        await _internalClient.Execute.AdvanceRequestAsync(request =>
        {
            string json = JsonSerializer.Serialize(tweetRequest);

            request.Query.Url = "https://api.twitter.com/2/tweets";
            request.Query.HttpMethod = HttpMethod.POST;
            request.Query.HttpContent = new StringContent(json, Encoding.UTF8, "application/json");
        });
    }
}