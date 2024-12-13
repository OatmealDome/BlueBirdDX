using System.Text;
using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Common.Util;
using BlueBirdDX.Config;
using BlueBirdDX.Database;
using BlueBirdDX.Social.Twitter;
using BlueBirdDX.Util;
using BlueBirdDX.Util.TextWrapper;
using Mastonet;
using Mastonet.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Airship.ATProtocol.Lexicon.Types;
using OatmealDome.Airship.ATProtocol.Lexicon.Types.Blob;
using OatmealDome.Airship.Bluesky;
using OatmealDome.Airship.Bluesky.Embed;
using OatmealDome.Airship.Bluesky.Embed.Image;
using OatmealDome.Airship.Bluesky.Embed.Record;
using OatmealDome.Airship.Bluesky.Feed;
using OatmealDome.Airship.Bluesky.Feed.Facets;
using OatmealDome.Unravel;
using OatmealDome.Unravel.Authentication;
using OatmealDome.Unravel.Publishing;
using Polly;
using Polly.Retry;
using Serilog;
using Serilog.Core;

namespace BlueBirdDX.Social;

public class PostThreadManager
{
    private const int ThreadsWaitForReadyTimeout = 60;
    private const int ThreadsWaitForReadyRetryDelay = 5;
    
    private static PostThreadManager? _instance;
    public static PostThreadManager Instance => _instance!;

    private static readonly ILogger LogContext =
        Log.ForContext(Constants.SourceContextPropertyName, "PostThreadManager");
    
    private readonly IMongoCollection<PostThread> _postThreadCollection;
    
    private readonly ResiliencePipeline _retryResiliencePipeline;
    private readonly ResiliencePipeline _threadsTimeoutResiliencePipeline;

    private readonly TextWrapperClient _textWrapperClient;

    private PostThreadManager()
    {
        _postThreadCollection = DatabaseManager.Instance.GetCollection<PostThread>("threads");
        
        RetryStrategyOptions retryOptions = new RetryStrategyOptions
        {
            BackoffType = DelayBackoffType.Linear,
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(5)
        };

        _retryResiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(retryOptions)
            .Build();

        _threadsTimeoutResiliencePipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(ThreadsWaitForReadyTimeout))
            .Build();

        _textWrapperClient = new TextWrapperClient(BbConfig.Instance.TextWrapper.ServerUrl);
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
                QuotedPost quotedPost = attachmentCache.GetQuotedPost(item.QuotedPost);

                quotedTweetId = quotedPost.TwitterId;
            }
            
            string[]? twitterMediaIds = null;

            if (item.AttachedMedia.Count > 0)
            {
                List<string> uploadedMediaIds = new List<string>();

                foreach (ObjectId mediaId in item.AttachedMedia)
                {
                    string altText = attachmentCache.GetMediaAltText(mediaId);

                    await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                    {
                        string uploadedMediaId =
                            await client.UploadImage(attachmentCache.GetMediaData(mediaId, SocialPlatform.Twitter),
                                altText.Length > 0 ? altText : null);

                        uploadedMediaIds.Add(uploadedMediaId);
                    });
                }

                twitterMediaIds = uploadedMediaIds.ToArray();
            }

            await _retryResiliencePipeline.ExecuteAsync(async (token) =>
            {
                previousId = await client.Tweet(item.Text, quotedTweetId: quotedTweetId, replyToTweetId: previousId,
                    mediaIds: twitterMediaIds);
            });

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

            List<ExtractedChunk> foundUrls = await _textWrapperClient.ExtractUrls(text);
            
            int i = 0;
            foreach (ExtractedChunk urlChunk in foundUrls)
            {
                builder.Append(text.Substring(i, urlChunk.Start - i));
        
                int byteStart = Encoding.UTF8.GetByteCount(builder.ToString(), 0, builder.Length);
        
                string fixedUrl = urlChunk.Data;

                if (!fixedUrl.StartsWith("http"))
                {
                    fixedUrl = "https://" + fixedUrl;
                }
        
                Uri uri = new Uri(fixedUrl);
        
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
                            Uri = fixedUrl
                        }
                    }
                });

                i = urlChunk.End;
            }
            
            builder.Append(text, i, text.Length - i);

            post.Text = builder.ToString();

            List<ExtractedChunk> foundHashtags = await _textWrapperClient.ExtractHashtags(post.Text);
            
            foreach (ExtractedChunk foundHashtag in foundHashtags)
            {
                facets.Add(new PostFacet()
                {
                    Index = new FacetRange()
                    {
                        ByteStart = Encoding.UTF8.GetByteCount(post.Text.Substring(0, foundHashtag.Start)),
                        ByteEnd = Encoding.UTF8.GetByteCount(post.Text.Substring(0, foundHashtag.End))
                    },
                    Features = new List<GenericFeature>()
                    {
                        new TagFeature()
                        {
                            Tag = foundHashtag.Data
                        }
                    }
                });
            }

            StrongRef? quotedRef = null;

            if (item.QuotedPost != null)
            {
                QuotedPost quotedPost = attachmentCache.GetQuotedPost(item.QuotedPost);
                
                if (quotedPost.BlueskyRef != null)
                {
                    quotedRef = quotedPost.BlueskyRef;
                }
                else
                {
                    byte[] quotedPostData = quotedPost.ImageData;
                
                    await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                    {
                        GenericBlob blob = await client.Repo_CreateBlob(quotedPostData, "image/png");
                
                        images.Add(new EmbeddedImage()
                        {
                            Image = blob,
                            AltText = "A screenshot of a tweet on Twitter."
                        });
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
                                Uri = quotedPost.SanitizedUrl
                            }
                        }
                    });
                }
            }
            
            foreach (ObjectId mediaId in item.AttachedMedia)
            {
                await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                {
                    GenericBlob blob = await client.Repo_CreateBlob(
                        attachmentCache.GetMediaData(mediaId, SocialPlatform.Bluesky),
                        attachmentCache.GetMediaMimeType(mediaId, SocialPlatform.Bluesky));
                
                    images.Add(new EmbeddedImage()
                    {
                        Image = blob,
                        AltText = attachmentCache.GetMediaAltText(mediaId)
                    }); 
                });
            }

            if (images.Count > 0 && quotedRef != null)
            {
                embed = new RecordWithMediaEmbed()
                {
                    RecordEmbed = new RecordEmbed()
                    {
                        Record = quotedRef
                    },
                    MediaEmbed = new ImagesEmbed()
                    {
                        Images = images
                    }
                };
            }
            else if (images.Count > 0)
            {
                embed = new ImagesEmbed()
                {
                    Images = images
                };
            }
            else if (quotedRef != null)
            {
                embed = new RecordEmbed()
                {
                    Record = quotedRef
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

            await _retryResiliencePipeline.ExecuteAsync(async (token) =>
            {
                previousPost = await client.Post_Create(post);
            });

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
                QuotedPost quotedPost = attachmentCache.GetQuotedPost(item.QuotedPost);

                byte[] quotedPostData = quotedPost.ImageData;
                
                using MemoryStream quotedPostStream = new MemoryStream(quotedPostData);

                await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                {
                    attachments.Add(await client.UploadMedia(quotedPostStream,
                        description: "A screenshot of a Tweet on Twitter.")); 
                });
                
                if (text != "")
                {
                    text += "\n\n";
                }

                if (quotedPost.MastodonId != null)
                {
                    Account mastodonAccount = await client.GetCurrentUser();
                    
                    text += "🐘\u00a0" + mastodonAccount.ProfileUrl + "/" + quotedPost.MastodonId;
                }
                else
                {
                    text += "🐦\u00a0" + quotedPost.SanitizedUrl;
                }
            }

            foreach (ObjectId mediaId in item.AttachedMedia)
            {
                using MemoryStream mediaStream =
                    new MemoryStream(attachmentCache.GetMediaData(mediaId, SocialPlatform.Mastodon));

                attachments.Add(await client.UploadMedia(mediaStream,
                    description: attachmentCache.GetMediaAltText(mediaId)));
            }

            await _retryResiliencePipeline.ExecuteAsync(async (token) =>
            {
                Status status = await client.PublishStatus(text, replyStatusId: previousId,
                    mediaIds: attachments.Count > 0 ? attachments.Select(a => a.Id) : null, visibility: Visibility.Public);
                
                previousId = status.Id;
            });

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

        async Task<bool> CheckMediaContainerReady(string containerId)
        {
            MediaContainerState state = await client.Publishing_GetMediaContainerState(containerId);

            if (state.Status == MediaContainerStatus.Error)
            {
                throw new Exception(
                    $"Media container {containerId} processing returned an error: \"{state.ErrorMessage}\"");
            }
            
            if (state.Status == MediaContainerStatus.Expired ||
                state.Status == MediaContainerStatus.Published)
            {
                throw new Exception($"Media container {containerId} is in unexpected status \"{state.Status}\"");
            }

            return state.Status == MediaContainerStatus.Finished;
        }

        string? previousId = parentThread?.Items.Last().ThreadsId;

        foreach (PostThreadItem item in postThread.Items)
        {
            string text = item.Text;
            
            List<(string mediaUrl, string altText)> attachments = new List<(string mediaUrl, string altText)>();

            string? quotedPostId = null;
            
            if (item.QuotedPost != null)
            {
                QuotedPost quotedPost = attachmentCache.GetQuotedPost(item.QuotedPost);

                if (quotedPost.ThreadsId != null)
                {
                    quotedPostId = quotedPost.ThreadsId;
                }
                else
                {
                    attachments.Add((quotedPost.ImageUrl, "A screenshot of a tweet on Twitter."));
                    
                    if (text != "")
                    {
                        text += "\n\n";
                    }

                    text += "🐦\u00a0" + quotedPost.SanitizedUrl;
                }
            }

            foreach (ObjectId attachmentId in item.AttachedMedia)
            {
                attachments.Add((attachmentCache.GetMediaPreSignedUrl(attachmentId),
                    attachmentCache.GetMediaAltText(attachmentId)));
            }

            string containerId = null!;
            
            if (attachments.Count > 1)
            {
                List<string> subIds = new List<string>();

                foreach ((string mediaUrl, string altText) in attachments)
                {
                    await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                    {
                        subIds.Add(await client.Publishing_CreateImageMediaContainer(mediaUrl, isCarouselItem: true,
                            altText: altText));
                    });
                }

                await _threadsTimeoutResiliencePipeline.ExecuteAsync(async (token) =>
                {
                    Dictionary<string, bool> subStates = new Dictionary<string, bool>();

                    while (!token.IsCancellationRequested)
                    {
                        foreach (string subId in subIds)
                        {
                            if (subStates.TryGetValue(subId, out bool ready))
                            {
                                if (ready)
                                {
                                    continue;
                                }
                            }
                            
                            subStates[subId] = await CheckMediaContainerReady(subId);
                        }

                        if (subStates.All(p => p.Value))
                        {
                            break;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(ThreadsWaitForReadyRetryDelay), token);
                    }
                });

                await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                {
                    containerId =
                        await client.Publishing_CreateCarouselMediaContainer(subIds, text, previousId,
                            quotedPostId: quotedPostId);
                });
            }
            else if (attachments.Count == 1)
            {
                (string mediaUrl, string altText) attachment = attachments[0];
                
                await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                {
                    containerId = await client.Publishing_CreateImageMediaContainer(attachment.mediaUrl, text,
                        previousId, quotedPostId: quotedPostId, altText: attachment.altText);
                });
            }
            else
            {
                await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                {
                    containerId =
                        await client.Publishing_CreateTextMediaContainer(text, previousId, quotedPostId: quotedPostId);
                });
            }
            
            await _threadsTimeoutResiliencePipeline.ExecuteAsync(async (token) =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (await CheckMediaContainerReady(containerId))
                    {
                        break;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(ThreadsWaitForReadyRetryDelay), token);
                }
            });

            await _retryResiliencePipeline.ExecuteAsync(async (token) =>
            {
                previousId = await client.Publishing_PublishMediaContainer(containerId);
            });

            item.ThreadsId = previousId;
            
            await _threadsTimeoutResiliencePipeline.ExecuteAsync(async (token) =>
            {
                while (!token.IsCancellationRequested)
                {
                    MediaContainerState state = await client.Publishing_GetMediaContainerState(containerId);
            
                    if (state.Status == MediaContainerStatus.Error ||
                        state.Status == MediaContainerStatus.Expired)
                    {
                        throw new Exception(
                            $"Media container {containerId} is in unexpected status during publishing \"{state.Status}\"");
                    }

                    if (state.Status == MediaContainerStatus.Published)
                    {
                        break;
                    }
                    
                    await Task.Delay(TimeSpan.FromSeconds(ThreadsWaitForReadyRetryDelay), token);
                }
            });
        }
    }
}
