using System.Text;
using System.Text.RegularExpressions;
using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Common.Storage;
using BlueBirdDX.Common.Util;
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
using OatmealDome.Unravel;
using OatmealDome.Unravel.Authentication;
using Serilog;
using Serilog.Core;

namespace BlueBirdDX.Social;

public class PostThreadManager
{
    private const string BlueskyUrlRegex =
        "(?<root>[(http(s)?):\\/\\/(www\\.)?a-zA-Z0-9@:%._\\+~#=-]{2,256}\\.[a-z]{2,6}\\b([-a-zA-Z0-9@:%_\\+.~#?&//=]*))";
    private const string BlueskyHashtagRegex =
        "(^|\\s)([#＃][\\w\\u05be\\u05f3\\u05f4]*[\\p{L}_]+[\\w\\u05be\\u05f3\\u05f4]*)";
    
    private static PostThreadManager? _instance;
    public static PostThreadManager Instance => _instance!;

    private static readonly ILogger LogContext =
        Log.ForContext(Constants.SourceContextPropertyName, "PostThreadManager");
    
    private readonly IMongoCollection<PostThread> _postThreadCollection;

    private readonly RemoteStorage _remoteStorage;
    
    private readonly Regex _urlRegex;
    private readonly Regex _hashtagRegex;

    private PostThreadManager()
    {
        _postThreadCollection = DatabaseManager.Instance.GetCollection<PostThread>("threads");

        RemoteStorageConfig storageConfig = BbConfig.Instance.RemoteStorage;
        
        _remoteStorage = new RemoteStorage(storageConfig.ServiceUrl, storageConfig.Bucket, storageConfig.AccessKey,
            storageConfig.AccessKeySecret);

        _urlRegex = new Regex(BlueskyUrlRegex, RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        _hashtagRegex = new Regex(BlueskyHashtagRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
                        LogContext.Error(e, "An exception occurred during processing of thread {id}",
                            postThread._id.ToString());

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
                await attachmentCache.AddQuotedPostToCache(item.QuotedPostSanitized!);
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

        AccountGroup group = AccountGroupManager.Instance.GetAccountGroup(postThread.TargetGroup);

        PostThread? parentThread = null;

        if (postThread.ParentThread != null)
        {
            parentThread = _postThreadCollection.AsQueryable().FirstOrDefault(t => t._id == postThread.ParentThread)!;
        }

        if (postThread.PostToTwitter && group.Twitter != null)
        {
            LogContext.Information("Posting thread {id} to Twitter", postThread._id.ToString());
            
            try
            {
                await PostToTwitter(postThread, parentThread, group.Twitter, attachmentCache);
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
                await PostToBluesky(postThread, parentThread, group.Bluesky, attachmentCache);
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
                await PostToMastodon(postThread, parentThread, group.Mastodon, attachmentCache);
            }
            catch (Exception e)
            {
                LogContext.Error(e, "Failed to post thread {id} to Mastodon", postThread._id.ToString());
                
                failed = true;
                AppendError(e.ToString());
            }
        }
        
        if (postThread.PostToThreads && group.Threads != null)
        {
            LogContext.Information("Posting thread {id} to Threads", postThread._id.ToString());
            
            try
            {
                await PostToThreads(postThread, parentThread, group.Threads, attachmentCache);
            }
            catch (Exception e)
            {
                LogContext.Error(e, "Failed to post thread {id} to Threads", postThread._id.ToString());
                
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

    private async Task PostToTwitter(PostThread postThread, PostThread? parentThread, TwitterAccount account,
        AttachmentCache attachmentCache)
    {
        BbTwitterClient client = new BbTwitterClient(account);

        string? previousId = parentThread?.Items.Last().TwitterId;

        foreach (PostThreadItem item in postThread.Items)
        {
            string? quotedTweetId = null;
            
            if (item.QuotedPost != null)
            {
                string[] splitUrl = item.QuotedPostSanitized!.Split('/');

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

            item.TwitterId = previousId;
        }
    }

    private async Task PostToBluesky(PostThread postThread, PostThread? parentThread, BlueskyAccount account,
        AttachmentCache attachmentCache)
    {
        BlueskyClient client = new BlueskyClient();
        await client.Server_CreateSession(account.Identifier, account.Password);

        PostThreadItem? lastParentItem = parentThread?.Items.Last();

        StrongRef? BbCommonRefToAirshipRef(BlueskyRef? commonRef)
        {
            return commonRef != null ? new StrongRef(commonRef.Uri, commonRef.Cid) : null;
        }
        
        StrongRef? rootPost = BbCommonRefToAirshipRef(lastParentItem?.BlueskyRootRef);
        StrongRef? previousPost = BbCommonRefToAirshipRef(lastParentItem?.BlueskyThisRef);
        
        foreach (PostThreadItem item in postThread.Items)
        {
            string text = item.Text.TrimEnd();
            
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
            
            foreach (string s in _urlRegex.Split(text))
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

            builder = new StringBuilder();
            
            foreach (string s in _hashtagRegex.Split(post.Text))
            {
                if (!_hashtagRegex.IsMatch(s))
                {
                    builder.Append(s);
                    
                    continue;
                }
                
                int byteStart = Encoding.UTF8.GetByteCount(builder.ToString(), 0, builder.Length);
                
                facets.Add(new PostFacet()
                {
                    Index = new FacetRange()
                    {
                        ByteStart = byteStart,
                        ByteEnd = byteStart + Encoding.UTF8.GetByteCount(s)
                    },
                    Features = new List<GenericFeature>()
                    {
                        new TagFeature()
                        {
                            Tag = s.Substring(1)
                        }
                    }
                });
                
                builder.Append(s);
            }

            if (item.QuotedPost != null)
            {
                byte[] quotedPostData = attachmentCache.GetQuotedPostData(item.QuotedPostSanitized!);

                GenericBlob blob = await client.Repo_CreateBlob(quotedPostData, "image/png");
                
                images.Add(new EmbeddedImage()
                {
                    Image = blob,
                    AltText = "A screenshot of a tweet on Twitter."
                });
                
                string textWithSpacingIfNecessary;

                if (post.Text == "")
                {
                    textWithSpacingIfNecessary = "";
                }
                else
                {
                    textWithSpacingIfNecessary = post.Text + "\n\n";
                }
                
                string textWithSpacingAndLink = textWithSpacingIfNecessary + "🐦\u00a0original post";

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
                            Uri = item.QuotedPostSanitized!
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

            BlueskyRef AirshipRefToBbCommonRef(StrongRef strongRef)
            {
                return new BlueskyRef()
                {
                    Cid = strongRef.Cid,
                    Uri = strongRef.Uri
                };
            }

            item.BlueskyRootRef = AirshipRefToBbCommonRef(rootPost);
            item.BlueskyThisRef = AirshipRefToBbCommonRef(previousPost);
        }

        await client.Server_DeleteSession();
    }

    private async Task PostToMastodon(PostThread postThread, PostThread? parentThread, MastodonAccount account,
        AttachmentCache attachmentCache)
    {
        MastodonClient client = new MastodonClient(account.InstanceUrl, account.AccessToken);

        string? previousId = parentThread?.Items.Last().MastodonId;

        foreach (PostThreadItem item in postThread.Items)
        {
            string text = item.Text;
            
            List<Attachment> attachments = new List<Attachment>();
            
            if (item.QuotedPost != null)
            {
                byte[] quotedPostData = attachmentCache.GetQuotedPostData(item.QuotedPostSanitized!);
                
                using MemoryStream quotedPostStream = new MemoryStream(quotedPostData);

                attachments.Add(await client.UploadMedia(quotedPostStream,
                    description: "A screenshot of a Tweet on Twitter."));

                if (text != "")
                {
                    text += "\n\n";
                }

                text += "🐦\u00a0" + item.QuotedPostSanitized!;
            }

            foreach (ObjectId mediaId in item.AttachedMedia)
            {
                using MemoryStream mediaStream =
                    new MemoryStream(attachmentCache.GetMediaData(mediaId, SocialPlatform.Mastodon));

                attachments.Add(await client.UploadMedia(mediaStream,
                    description: attachmentCache.GetMediaAltText(mediaId)));
            }

            Status status = await client.PublishStatus(text, replyStatusId: previousId,
                mediaIds: attachments.Count > 0 ? attachments.Select(a => a.Id) : null, visibility: Visibility.Public);

            previousId = status.Id;

            item.MastodonId = previousId;
        }
    }

    private async Task PostToThreads(PostThread postThread, PostThread? parentThread, ThreadsAccount account,
        AttachmentCache attachmentCache)
    {
        ThreadsClient client = new ThreadsClient(account.ClientId, account.ClientSecret)
        {
            Credentials = new ThreadsCredentials()
            {
                CredentialType = ThreadsCredentialType.LongLived,
                AccessToken = account.AccessToken,
                Expiry = account.Expiry,
                UserId = account.UserId
            }
        };

        string? previousId = parentThread?.Items.Last().ThreadsId;

        foreach (PostThreadItem item in postThread.Items)
        {
            string text = item.Text;
            
            List<string> mediaUrls = new List<string>();
            
            if (item.QuotedPost != null)
            {
                mediaUrls.Add(attachmentCache.GetQuotedPostPreSignedUrl(item.QuotedPostSanitized!));
                
                if (text != "")
                {
                    text += "\n\n";
                }

                text += "🐦\u00a0" + item.QuotedPostSanitized;
            }

            foreach (ObjectId attachmentId in item.AttachedMedia)
            {
                mediaUrls.Add(attachmentCache.GetMediaPreSignedUrl(attachmentId));
            }

            string containerId;
            
            if (mediaUrls.Count > 1)
            {
                List<string> subIds = new List<string>();

                foreach (string url in mediaUrls)
                {
                    subIds.Add(await client.Publishing_CreateImageMediaContainer(url, isCarouselItem: true));
                }

                containerId = await client.Publishing_CreateCarouselMediaContainer(subIds, text, previousId);
            }
            else if (mediaUrls.Count == 1)
            {
                containerId = await client.Publishing_CreateImageMediaContainer(mediaUrls[0], text, previousId);
            }
            else
            {
                containerId = await client.Publishing_CreateTextMediaContainer(text, previousId);
            }
            
            if (mediaUrls.Count > 0)
            {
                // Facebook says to "wait on average 30 seconds" before publishing a media container to give the server
                // time to process the upload.
                // TODO: After waiting, we should probably check the media container status before proceeding.
                // https://developers.facebook.com/docs/threads/posts
                await Task.Delay(30 * 1000);
            }

            previousId = await client.Publishing_PublishMediaContainer(containerId);

            item.ThreadsId = previousId;
        }
    }
}