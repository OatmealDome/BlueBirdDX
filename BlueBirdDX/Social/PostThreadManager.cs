using System.Text;
using System.Text.RegularExpressions;
using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Common.Storage;
using BlueBirdDX.Config;
using BlueBirdDX.Config.Storage;
using BlueBirdDX.Database;
using BlueBirdDX.Social.Twitter;
using BlueBirdDX.Util;
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
using OatmealDome.Airship.Bluesky.Feed.Facets;
using Serilog;
using Serilog.Core;

namespace BlueBirdDX.Social;

public class PostThreadManager
{
    private const string BlueskyUrlRegex =
        "(?<root>[(http(s)?):\\/\\/(www\\.)?a-zA-Z0-9@:%._\\+~#=-]{2,256}\\.[a-z]{2,6}\\b([-a-zA-Z0-9@:%_\\+.~#?&//=]*))";
    
    private static PostThreadManager? _instance;
    public static PostThreadManager Instance => _instance!;

    private static readonly ILogger LogContext =
        Log.ForContext(Constants.SourceContextPropertyName, "PostThreadManager");
    
    private readonly IMongoCollection<AccountGroup> _accountGroupCollection;
    private readonly IMongoCollection<PostThread> _postThreadCollection;

    private readonly RemoteStorage _remoteStorage;
    
    private readonly Regex _urlRegex;

    private PostThreadManager()
    {
        _accountGroupCollection = DatabaseManager.Instance.GetCollection<AccountGroup>("accounts");
        _postThreadCollection = DatabaseManager.Instance.GetCollection<PostThread>("threads");

        RemoteStorageConfig storageConfig = BbConfig.Instance.RemoteStorage;
        
        _remoteStorage = new RemoteStorage(storageConfig.ServiceUrl, storageConfig.Bucket, storageConfig.AccessKey,
            storageConfig.AccessKeySecret);

        _urlRegex = new Regex(BlueskyUrlRegex, RegexOptions.Compiled | RegexOptions.ExplicitCapture);
    }
    
    public static void Initialize()
    {
        _instance = new PostThreadManager();
    }

    private async Task UpdateThreadState(PostThread thread, PostThreadState state, string? errorMessage = null)
    {
        thread.State = state;

        if (state == PostThreadState.Error)
        {
            thread.ErrorMessage = errorMessage;
        }

        await _postThreadCollection.ReplaceOneAsync(
            Builders<PostThread>.Filter.Eq(p => p._id, thread._id), thread);
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
                    try
                    {
                        await ProcessPostThread(postThread);
                    }
                    catch (Exception e)
                    {
                        LogContext.Error(e, "An exception occurred during processing of thread {id}", postThread._id.ToString());

                        await UpdateThreadState(postThread, PostThreadState.Error,
                            "An exception occurred during processing.\n\n" + e.ToString());
                    }
                }
                else
                {
                    LogContext.Error(
                        "Skipping thread {id} because its scheduled time is outside of the margin of error",
                        postThread._id.ToString());

                    await UpdateThreadState(postThread, PostThreadState.Error,
                        "Scheduled time is in past but outside of margin of error when processed by PostThreadManager");
                }
            }
        }
    }

    private async Task ProcessPostThread(PostThread postThread)
    {
        LogContext.Information("Processing thread {id}", postThread._id.ToString());

        LogContext.Information("Creating quoted post screenshots for {id}", postThread._id.ToString());
        
        AttachmentCache attachmentCache = new AttachmentCache();
        
        foreach (PostThreadItem item in postThread.Items)
        {
            if (item.QuotedPost != null)
            {
                await attachmentCache.AddQuotedPostToCache(item.QuotedPost);
            }
        }

        LogContext.Information("Downloading media for thread {id}", postThread._id.ToString());
        
        foreach (ObjectId mediaId in postThread.Items.SelectMany(i => i.AttachedMedia))
        {
            await attachmentCache.AddMediaToCache(mediaId);
        }

        LogContext.Information("Beginning post process for thread {id}", postThread._id.ToString());
        
        await Post(postThread, attachmentCache);
        
        LogContext.Information("Processed thread {id}", postThread._id.ToString());
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

        if (postThread.PostToTwitter && group.Twitter != null)
        {
            LogContext.Information("Posting thread {id} to Twitter", postThread._id.ToString());
            
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
        
        if (postThread.PostToBluesky && group.Bluesky != null)
        {
            LogContext.Information("Posting thread {id} to Bluesky", postThread._id.ToString());
            
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
        
        if (postThread.PostToMastodon && group.Mastodon != null)
        {
            LogContext.Information("Posting thread {id} to Mastodon", postThread._id.ToString());
            
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
        }

        await UpdateThreadState(postThread, outState, errorBuilder.ToString());
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
                    string altText = attachmentCache.GetMediaAltText(mediaId);

                    string uploadedMediaId =
                        await client.UploadImage(attachmentCache.GetMediaData(mediaId, SocialPlatform.Twitter),
                            altText.Length > 0 ? altText : null);
                    
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
            
            List<PostFacet> facets = new List<PostFacet>();

            StringBuilder builder = new StringBuilder();
            
            foreach (string s in _urlRegex.Split(item.Text))
            {
                if (!_urlRegex.IsMatch(s))
                {
                    builder.Append(s);
            
                    continue;
                }

                string urlString;

                if (!s.StartsWith("http"))
                {
                    urlString = "https://" + s;
                }
                else
                {
                    urlString = s;
                }
                
                int byteStart = Encoding.UTF8.GetByteCount(builder.ToString(), 0, builder.Length);
                
                Uri uri = new Uri(urlString);
        
                string replacement = $"🔗\u00a0{uri.Host}";
                builder.Append(replacement);

                facets.Add(new PostFacet()
                {
                    Index = new FacetRange()
                    {
                        ByteStart = byteStart,
                        ByteEnd = byteStart + Encoding.UTF8.GetByteCount(replacement)
                    },
                    Features = new List<GenericFeature>()
                    {
                        new LinkFeature()
                        {
                            Uri = urlString
                        }
                    }
                });
            }

            post.Text = builder.ToString();

            if (item.QuotedPost != null)
            {
                byte[] quotedPostData = attachmentCache.GetQuotedPostData(item.QuotedPost);

                GenericBlob blob = await client.Repo_CreateBlob(quotedPostData, "image/png");
                
                images.Add(new EmbeddedImage()
                {
                    Image = blob,
                    AltText = "A screenshot of a tweet on Twitter."
                });
                
                string textWithSpacingIfNecessary;

                if (item.Text == "")
                {
                    textWithSpacingIfNecessary = "";
                }
                else
                {
                    textWithSpacingIfNecessary = item.Text + "\n\n";
                }
                
                string textWithSpacingAndLink = textWithSpacingIfNecessary + "🐦 original post";

                int linkStartIdx = Encoding.UTF8.GetByteCount(textWithSpacingIfNecessary);
                int linkEndIdx = Encoding.UTF8.GetByteCount(textWithSpacingAndLink);

                post.Text = textWithSpacingAndLink;

                facets.Add(new PostFacet()
                {
                    Index = new FacetRange()
                    {
                        ByteStart = linkStartIdx,
                        ByteEnd = linkEndIdx
                    },
                    Features = new List<GenericFeature>()
                    {
                        new LinkFeature()
                        {
                            Uri = item.QuotedPost
                        }
                    }
                });
            }
            
            foreach (ObjectId mediaId in item.AttachedMedia)
            {
                GenericBlob blob = await client.Repo_CreateBlob(
                    attachmentCache.GetMediaData(mediaId, SocialPlatform.Bluesky),
                    attachmentCache.GetMediaMimeType(mediaId, SocialPlatform.Bluesky));
                
                images.Add(new EmbeddedImage()
                {
                    Image = blob,
                    AltText = attachmentCache.GetMediaAltText(mediaId)
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
            
            if (facets.Count > 0)
            {
                post.Facets = facets;
            }

            previousPost = await client.Post_Create(post);

            if (rootPost == null)
            {
                rootPost = previousPost;
            }
        }

        await client.Server_DeleteSession();
    }
    
    private async Task PostToMastodon(PostThread postThread, MastodonAccount account, AttachmentCache attachmentCache)
    {
        MastodonClient client = new MastodonClient(account.InstanceUrl, account.AccessToken);

        Status? previousStatus = null;

        foreach (PostThreadItem item in postThread.Items)
        {
            string text = item.Text;
            
            List<Attachment> attachments = new List<Attachment>();
            
            if (item.QuotedPost != null)
            {
                byte[] quotedPostData = attachmentCache.GetQuotedPostData(item.QuotedPost);
                
                using MemoryStream quotedPostStream = new MemoryStream(quotedPostData);

                attachments.Add(await client.UploadMedia(quotedPostStream,
                    description: "A screenshot of a Tweet on Twitter."));

                if (text != "")
                {
                    text += "\n\n";
                }

                text += "🐦 " + item.QuotedPost;
            }

            foreach (ObjectId mediaId in item.AttachedMedia)
            {
                using MemoryStream mediaStream =
                    new MemoryStream(attachmentCache.GetMediaData(mediaId, SocialPlatform.Mastodon));

                attachments.Add(await client.UploadMedia(mediaStream,
                    description: attachmentCache.GetMediaAltText(mediaId)));
            }

            Status status = await client.PublishStatus(text, replyStatusId: previousStatus?.Id,
                mediaIds: attachments.Count > 0 ? attachments.Select(a => a.Id) : null, visibility: Visibility.Public);

            previousStatus = status;
        }
    }
}