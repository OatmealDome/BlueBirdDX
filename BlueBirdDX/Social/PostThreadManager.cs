using System.Text;
using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Common.Storage;
using BlueBirdDX.Config;
using BlueBirdDX.Config.Storage;
using BlueBirdDX.Database;
using BlueBirdDX.Social.Twitter;
using MongoDB.Driver;
using Serilog;
using Serilog.Core;

namespace BlueBirdDX.Social;

public class PostThreadManager
{
    private static PostThreadManager? _instance;
    public static PostThreadManager Instance => _instance!;

    private static readonly ILogger LogContext =
        Log.ForContext(Constants.SourceContextPropertyName, "PostThreadManager");
    
    private readonly IMongoCollection<AccountGroup> _accountGroupCollection;
    private readonly IMongoCollection<PostThread> _postThreadCollection;

    private readonly RemoteStorage _remoteStorage;

    private PostThreadManager()
    {
        _accountGroupCollection = DatabaseManager.Instance.GetCollection<AccountGroup>("accounts");
        _postThreadCollection = DatabaseManager.Instance.GetCollection<PostThread>("threads");

        RemoteStorageConfig storageConfig = BbConfig.Instance.RemoteStorage;
        
        _remoteStorage = new RemoteStorage(storageConfig.ServiceUrl, storageConfig.Bucket, storageConfig.AccessKey,
            storageConfig.AccessKeySecret);
    }
    
    public static void Initialize()
    {
        _instance = new PostThreadManager();
    }

    public async Task ProcessPostThreads()
    {
        DateTime referenceNow = DateTime.UtcNow;
        
        IEnumerable<PostThread> enqueuedPostThreads =
            _postThreadCollection.AsQueryable().Where(p => p.State == PostThreadState.Enqueued).ToList();

        foreach (PostThread postThread in enqueuedPostThreads)
        {
            if (referenceNow > postThread.ScheduledTime)
            {
                TimeSpan span = referenceNow - postThread.ScheduledTime;
                
                // We should only post threads that are within a 5 minute window of its scheduled time.
                if (span.TotalMinutes < 5.0d)
                {
                    await Post(postThread);
                }
                else
                {
                    LogContext.Error(
                        "Skipping thread {id} because its scheduled time is outside of the margin of error",
                        postThread._id.ToString());
                    
                    postThread.State = PostThreadState.Error;
                    postThread.ErrorMessage =
                        "Scheduled time is in past but outside of margin of error when processed by PostThreadManager";

                    await _postThreadCollection.ReplaceOneAsync(
                        Builders<PostThread>.Filter.Eq(p => p._id, postThread._id), postThread);
                }
            }
        }
    }

    public async Task Post(PostThread postThread)
    {
        bool failed = false;
        StringBuilder errorBuilder = new StringBuilder();

        void AppendError(string error)
        {
            errorBuilder.AppendLine("=======================================");
            errorBuilder.AppendLine(error);
        }

        AccountGroup group =
            _accountGroupCollection.AsQueryable().FirstOrDefault(a => a._id == postThread.TargetGroup)!;

        if (group.Twitter != null)
        {
            try
            {
                await PostToTwitter(postThread, group.Twitter);
            }
            catch (Exception e)
            {
                LogContext.Error(e, "Failed to post thread {id} to Twitter", postThread._id.ToString());
                
                failed = true;
                AppendError(e.ToString());
            }
        }

        PostThreadState outState = PostThreadState.Sent;
        
        if (failed)
        {
            outState = PostThreadState.Error;

            await _postThreadCollection.UpdateOneAsync(p => p._id == postThread._id,
                Builders<PostThread>.Update.Set(p => p.ErrorMessage, errorBuilder.ToString()));
        }

        await _postThreadCollection.UpdateOneAsync(p => p._id == postThread._id,
            Builders<PostThread>.Update.Set(p => p.State, outState));
    }
    
    private async Task PostToTwitter(PostThread postThread, TwitterAccount account)
    {
        BbTwitterClient client = new BbTwitterClient(account);

        string? previousId = null;

        foreach (PostThreadItem item in postThread.Items)
        {
            previousId = await client.Tweet(item.Text, replyToTweetId: previousId);
        }
    }
}