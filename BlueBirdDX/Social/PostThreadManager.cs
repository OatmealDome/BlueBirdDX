using System.Text;
using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Database;
using MongoDB.Driver;

namespace BlueBirdDX.Social;

public class PostThreadManager
{
    private static PostThreadManager? _instance;
    public static PostThreadManager Instance => _instance!;
    
    private readonly IMongoCollection<AccountGroup> _accountGroupCollection;
    private readonly IMongoCollection<PostThread> _postThreadCollection;

    private PostThreadManager()
    {
        _accountGroupCollection = DatabaseManager.Instance.GetCollection<AccountGroup>("accounts");
        _postThreadCollection = DatabaseManager.Instance.GetCollection<PostThread>("threads");
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
            if (postThread.ScheduledTime > referenceNow)
            {
                await Post(postThread);
            }
        }
    }

    public async Task Post(PostThread postThread)
    {
        bool failed = false;
        StringBuilder errorBuilder = new StringBuilder();

        AccountGroup group =
            _accountGroupCollection.AsQueryable().FirstOrDefault(a => a._id == postThread.TargetGroup)!;
        
        // TODO

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
}