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

    public async Task<IMedia> UploadImage(byte[] image)
    {
        return await _internalClient.Upload.UploadTweetImageAsync(image);
    }
    
    public async Task Tweet(string text, string[]? mediaIds)
    {
        TweetV2RequestMedia? tweetRequestMedia;

        if (mediaIds != null)
        {
            tweetRequestMedia = new TweetV2RequestMedia()
            {
                MediaIds = mediaIds
            };
        }
        else
        {
            tweetRequestMedia = null;
        }

        TweetV2Request tweetRequest = new TweetV2Request()
        {
            Text = text,
            Media = tweetRequestMedia
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