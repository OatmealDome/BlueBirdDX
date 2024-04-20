using System.Text;
using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Common.Storage;
using BlueBirdDX.Config;
using BlueBirdDX.Config.Storage;
using BlueBirdDX.Database;
using BlueBirdDX.Social.Twitter;
using Mastonet;
using Mastonet.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Airship.ATProtocol.Lexicon.Types;
using OatmealDome.Airship.ATProtocol.Lexicon.Types.Blob;
using OatmealDome.Airship.Bluesky;
using OatmealDome.Airship.Bluesky.Embed;
using OatmealDome.Airship.Bluesky.Embed.Image;
using OatmealDome.Airship.Bluesky.Feed;
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
                
                // We should only process threads that are within a 5 minute window of its scheduled time.
                if (span.TotalMinutes < 5.0d)
                {
                    await ProcessPostThread(postThread);
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

    private async Task ProcessPostThread(PostThread postThread)
    {
        AttachmentCache attachmentCache = new AttachmentCache();
        
        foreach (PostThreadItem item in postThread.Items)
        {
            if (item.QuotedPost != null)
            {
                await attachmentCache.AddQuotedPostToCache(item.QuotedPost);
            }
        }
        
        foreach (ObjectId mediaId in postThread.Items.SelectMany(i => i.AttachedMedia))
        {
            await attachmentCache.AddMediaToCache(mediaId);
        }
        
        await Post(postThread, attachmentCache);
    }

    private async Task Post(PostThread postThread, AttachmentCache attachmentCache)
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
                await PostToTwitter(postThread, group.Twitter, attachmentCache);
            }
            catch (Exception e)
            {
                LogContext.Error(e, "Failed to post thread {id} to Twitter", postThread._id.ToString());
                
                failed = true;
                AppendError(e.ToString());
            }
        }
        
        if (group.Bluesky != null)
        {
            try
            {
                await PostToBluesky(postThread, group.Bluesky, attachmentCache);
            }
            catch (Exception e)
            {
                LogContext.Error(e, "Failed to post thread {id} to Bluesky", postThread._id.ToString());
                
                failed = true;
                AppendError(e.ToString());
            }
        }
        
        if (group.Mastodon != null)
        {
            try
            {
                await PostToMastodon(postThread, group.Mastodon, attachmentCache);
            }
            catch (Exception e)
            {
                LogContext.Error(e, "Failed to post thread {id} to Mastodon", postThread._id.ToString());
                
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

    private async Task PostToTwitter(PostThread postThread, TwitterAccount account, AttachmentCache attachmentCache)
    {
        BbTwitterClient client = new BbTwitterClient(account);

        string? previousId = null;

        foreach (PostThreadItem item in postThread.Items)
        {
            string? quotedTweetId = null;
            
            if (item.QuotedPost != null)
            {
                string[] splitUrl = item.QuotedPost.Split('/');

                quotedTweetId = splitUrl[^1];
            }
            
            string[]? twitterMediaIds = null;

            if (item.AttachedMedia.Count > 0)
            {
                List<string> uploadedMediaIds = new List<string>();

                foreach (ObjectId mediaId in item.AttachedMedia)
                {
                    UploadedMedia media = attachmentCache.GetMediaDocument(mediaId);

                    string uploadedMediaId =
                        await client.UploadImage(attachmentCache.GetMediaData(mediaId),
                            media.AltText.Length > 0 ? media.AltText : null);
                    
                    uploadedMediaIds.Add(uploadedMediaId);
                }

                twitterMediaIds = uploadedMediaIds.ToArray();
            }

            previousId = await client.Tweet(item.Text, quotedTweetId: quotedTweetId, replyToTweetId: previousId,
                mediaIds: twitterMediaIds);
        }
    }

    private async Task PostToBluesky(PostThread postThread, BlueskyAccount account, AttachmentCache attachmentCache)
    {
        BlueskyClient client = new BlueskyClient();
        await client.Server_CreateSession(account.Identifier, account.Password);
        
        StrongRef? rootPost = null;
        StrongRef? previousPost = null;
        
        foreach (PostThreadItem item in postThread.Items)
        {
            Post post = new Post()
            {
                Text = item.Text,
                CreatedAt = DateTime.UtcNow
            };
            
            if (previousPost != null)
            {
                post.Reply = new PostReply()
                {
                    Root = rootPost!,
                    Parent = previousPost
                };
            }

            GenericEmbed? embed = null;
            
            List<EmbeddedImage> images = new List<EmbeddedImage>();

            if (item.QuotedPost != null)
            {
                byte[] quotedPostData = attachmentCache.GetQuotedPostData(item.QuotedPost);

                GenericBlob blob = await client.Repo_CreateBlob(quotedPostData, "image/png");
                
                images.Add(new EmbeddedImage()
                {
                    Image = blob,
                    AltText = "A screenshot of a tweet on Twitter."
                });
            }
            
            foreach (ObjectId mediaId in item.AttachedMedia)
            {
                UploadedMedia media = attachmentCache.GetMediaDocument(mediaId);
                byte[] mediaData = attachmentCache.GetMediaData(mediaId);

                GenericBlob blob = await client.Repo_CreateBlob(mediaData, media.MimeType);
                
                images.Add(new EmbeddedImage()
                {
                    Image = blob,
                    AltText = media.AltText
                });
            }

            if (images.Count > 0)
            {
                embed = new ImagesEmbed()
                {
                    Images = images
                };
            }

            if (embed != null)
            {
                post.Embed = embed;
            }

            previousPost = await client.Post_Create(post);

            if (rootPost == null)
            {
                rootPost = previousPost;
            }
        }
    }
    
    private async Task PostToMastodon(PostThread postThread, MastodonAccount account, AttachmentCache attachmentCache)
    {
        MastodonClient client = new MastodonClient(account.InstanceUrl, account.AccessToken);

        Status? previousStatus = null;

        foreach (PostThreadItem item in postThread.Items)
        {
            List<Attachment> attachments = new List<Attachment>();
            
            if (item.QuotedPost != null)
            {
                byte[] quotedPostData = attachmentCache.GetQuotedPostData(item.QuotedPost);
                
                using MemoryStream quotedPostStream = new MemoryStream(quotedPostData);

                attachments.Add(await client.UploadMedia(quotedPostStream,
                    description: "A screenshot of a Tweet on Twitter."));
            }

            foreach (ObjectId mediaId in item.AttachedMedia)
            {
                UploadedMedia media = attachmentCache.GetMediaDocument(mediaId);
                byte[] mediaData = attachmentCache.GetMediaData(mediaId);

                using MemoryStream mediaStream = new MemoryStream(mediaData);
                
                attachments.Add(await client.UploadMedia(mediaStream, description: media.AltText));
            }

            Status status = await client.PublishStatus(item.Text, replyStatusId: previousStatus?.Id,
                mediaIds: attachments.Count > 0 ? attachments.Select(a => a.Id) : null);

            previousStatus = status;
        }
    }
}