using System.Reflection;
using System.Text;
using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Common.Social;
using BlueBirdDX.Common.Util;
using BlueBirdDX.Common.Util.TextWrapper;
using BlueBirdDX.Database;
using BlueBirdDX.Platform.Twitter;
using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.Embed;
using idunno.Bluesky.RichText;
using idunno.Bluesky.Video;
using Mastonet;
using Mastonet.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;
using OatmealDome.Slab.S3;
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

    private const string DifferentPlatformQuoteImageAltText = "A screenshot of a post on a different platform.";

    private readonly ILogger<PostThreadManager> _logger;
    private readonly PostThreadManagerConfiguration _settings;
    private readonly SocialAppConfiguration _socialSettings;
    private readonly SlabMongoService _mongoService;
    private readonly SlabS3Service _s3Service;
    private readonly IMongoCollection<PostThread> _postThreadCollection;
    private readonly AccountGroupManager _accountGroupManager;
    private readonly TextWrapperClient _textWrapperClient;
    
    private readonly ResiliencePipeline _retryResiliencePipeline;
    private readonly ResiliencePipeline _threadsTimeoutResiliencePipeline;

    public PostThreadManager(ILogger<PostThreadManager> logger, IOptions<PostThreadManagerConfiguration> settings,
        IOptions<SocialAppConfiguration> socialSettings, SlabMongoService mongoService, SlabS3Service s3Service,
        AccountGroupManager accountGroupManager)
    {
        _logger = logger;
        _settings = settings.Value;
        _socialSettings = socialSettings.Value;
        _mongoService = mongoService;
        _s3Service = s3Service;
        _postThreadCollection = mongoService.GetCollection<PostThread>("threads");
        _accountGroupManager = accountGroupManager;
        _textWrapperClient = new TextWrapperClient(_settings.TextWrapperServer);
        
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
                        _logger.LogError(e, "An exception occurred during processing of thread {id}",
                            postThread._id.ToString());

                        await UpdateThreadState(postThread, PostThreadState.Error,
                            "An exception occurred during processing.\n\n" + e.ToString());
                    }
                }
                else
                {
                    _logger.LogError(
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
        _logger.LogInformation("Processing thread {id}", postThread._id.ToString());

        _logger.LogInformation("Fetching quoted posts for {id}", postThread._id.ToString());

        AttachmentCache attachmentCache =
            new AttachmentCache(_s3Service, _mongoService, _settings.SeleniumNodeUrl, _settings.WebAppUrl);
        
        foreach (PostThreadItem item in postThread.Items)
        {
            if (item.QuotedPost != null)
            {
                await attachmentCache.AddQuotedPostToCache(item.QuotedPost);
            }
        }

        _logger.LogInformation("Downloading media for thread {id}", postThread._id.ToString());
        
        foreach (ObjectId mediaId in postThread.Items.SelectMany(i => i.AttachedMedia))
        {
            await attachmentCache.AddMediaToCache(mediaId);
        }

        _logger.LogInformation("Beginning post process for thread {id}", postThread._id.ToString());
        
        await Post(postThread, attachmentCache);
        
        _logger.LogInformation("Processed thread {id}", postThread._id.ToString());
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

        AccountGroup group = _accountGroupManager.GetAccountGroup(postThread.TargetGroup);

        PostThread? parentThread = null;

        if (postThread.ParentThread != null)
        {
            parentThread = _postThreadCollection.AsQueryable().FirstOrDefault(t => t._id == postThread.ParentThread)!;
        }

        if (postThread.PostToTwitter && group.Twitter != null &&
            !string.IsNullOrEmpty(_socialSettings.TwitterClientId) &&
            !string.IsNullOrEmpty(_socialSettings.TwitterClientSecret))
        {
            _logger.LogInformation("Posting thread {id} to Twitter", postThread._id.ToString());
            
            try
            {
                await PostToTwitter(postThread, parentThread, group, attachmentCache);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to post thread {id} to Twitter", postThread._id.ToString());
                
                failed = true;
                AppendError(e.ToString());
            }
        }
        
        if (postThread.PostToBluesky && group.Bluesky != null)
        {
            _logger.LogInformation("Posting thread {id} to Bluesky", postThread._id.ToString());
            
            try
            {
                await PostToBluesky(postThread, parentThread, group.Bluesky, attachmentCache);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to post thread {id} to Bluesky", postThread._id.ToString());
                
                failed = true;
                AppendError(e.ToString());
            }
        }
        
        if (postThread.PostToMastodon && group.Mastodon != null)
        {
            _logger.LogInformation("Posting thread {id} to Mastodon", postThread._id.ToString());
            
            try
            {
                await PostToMastodon(postThread, parentThread, group.Mastodon, attachmentCache);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to post thread {id} to Mastodon", postThread._id.ToString());
                
                failed = true;
                AppendError(e.ToString());
            }
        }
        
        if (postThread.PostToThreads && group.Threads != null)
        {
            _logger.LogInformation("Posting thread {id} to Threads", postThread._id.ToString());
            
            try
            {
                await PostToThreads(postThread, parentThread, group.Threads, attachmentCache);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to post thread {id} to Threads", postThread._id.ToString());
                
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

    private async Task PostToTwitter(PostThread postThread, PostThread? parentThread, AccountGroup group,
        AttachmentCache attachmentCache)
    {
        TwitterClient client =
            new TwitterClient(_socialSettings.TwitterClientId!, _socialSettings.TwitterClientSecret!);
        await client.Login(group.Twitter!.RefreshToken);

        try
        {
            string? previousId = parentThread?.Items.Last().TwitterId;

            foreach (PostThreadItem item in postThread.Items)
            {
                string text = item.Text;

                List<string> uploadedMediaIds = new List<string>();

                string? quotedTweetId = null;

                if (item.QuotedPost != null)
                {
                    QuotedPost quotedPost = attachmentCache.GetQuotedPost(item.QuotedPost);

                    if (quotedPost.TwitterId != null)
                    { 
                        // This is currently blocked by Twitter anti-spam restrictions.
                        // quotedTweetId = quotedPost.TwitterId;
                        
                        // We can bypass the restrictions by inserting the URL into the tweet directly, though this
                        // uses up part of our character limit.
                        text += $" https://x.com/_/status/{quotedPost.TwitterId}";
                    }
                    else
                    {
                        string quoteMediaId = await client.UploadImage(quotedPost.ImageData!, "image/png",
                            DifferentPlatformQuoteImageAltText);

                        uploadedMediaIds.Add(quoteMediaId);

                        if (text != "")
                        {
                            text += "\n\n";
                        }

                        text += quotedPost.GetPrimaryPlatform().ToEmoji() + "\u00a0" +
                                quotedPost.GetPostUrlOnPrimaryPlatform();
                    }
                }

                string[]? twitterMediaIds = null;

                if (item.AttachedMedia.Count > 0)
                {
                    foreach (ObjectId mediaId in item.AttachedMedia)
                    {
                        string altText = attachmentCache.GetMediaAltText(mediaId);

                        await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                        {
                            string mediaMimeType = attachmentCache.GetMediaMimeType(mediaId, SocialPlatform.Twitter);
                            byte[] mediaData = attachmentCache.GetMediaData(mediaId, SocialPlatform.Twitter);
                            string? mediaAltText = altText.Length > 0 ? altText : null;

                            string uploadedMediaId;

                            if (mediaMimeType.StartsWith("image/"))
                            {
                                uploadedMediaId = await client.UploadImage(mediaData, mediaMimeType, mediaAltText);
                            }
                            else
                            {
                                uploadedMediaId = await client.UploadVideo(mediaData, mediaMimeType, mediaAltText);
                            }

                            uploadedMediaIds.Add(uploadedMediaId);
                        });
                    }
                }

                twitterMediaIds = uploadedMediaIds.Count > 0 ? uploadedMediaIds.ToArray() : null;

                await _retryResiliencePipeline.ExecuteAsync(async (token) =>
                {
                    previousId = await client.Tweet(text, quotedTweetId: quotedTweetId, replyToTweetId: previousId,
                        mediaIds: twitterMediaIds);
                });

                item.TwitterId = previousId;
            }
        }
        finally
        {
            try
            {
                await client.Logout();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred while logging out of Twitter");
            }
            
            await _accountGroupManager.UpdateTwitterRefreshTokenForGroup(group, client.RefreshToken!);
        }
    }

    private async Task PostToBluesky(PostThread postThread, PostThread? parentThread, BlueskyAccount account,
        AttachmentCache attachmentCache)
    {
        BlueskyAgent agent = new BlueskyAgent();
        await agent.Login(account.Identifier, account.Password);

        try
        {
            PostThreadItem? lastParentItem = parentThread?.Items.Last();

            StrongReference? BbCommonRefToStrongReference(BlueskyRef? commonRef)
            {
                return commonRef != null ? new StrongReference(commonRef.Uri, commonRef.Cid) : null;
            }
            
            StrongReference? rootPost = BbCommonRefToStrongReference(lastParentItem?.BlueskyRootRef);
            StrongReference? previousPost = BbCommonRefToStrongReference(lastParentItem?.BlueskyThisRef);
            
            foreach (PostThreadItem item in postThread.Items)
            {
                string text = item.Text.TrimEnd();

                PostBuilder postBuilder = new PostBuilder();
                
                List<ExtractedChunk> chunks = await _textWrapperClient.Tokenize(text);
                foreach (ExtractedChunk chunk in chunks)
                {
                    if (chunk.ChunkType == ExtractedChunkType.Url)
                    {
                        string fixedUrl = chunk.Value;

                        if (!fixedUrl.StartsWith("http"))
                        {
                            fixedUrl = "https://" + fixedUrl;
                        }

                        Uri uri = new Uri(fixedUrl);
            
                        string replacement = $"🔗\u00a0{uri.Host}";
                        postBuilder.Append(new Link(uri, replacement));
                    }
                    else if (chunk.ChunkType == ExtractedChunkType.Hashtag)
                    {
                        postBuilder.Append(new HashTag(chunk.Value.Substring(1)));
                    }
                    else
                    {
                        postBuilder.Append(chunk.Value);
                    }
                }

                if (item.QuotedPost != null)
                {
                    QuotedPost quotedPost = attachmentCache.GetQuotedPost(item.QuotedPost);
                    
                    if (quotedPost.BlueskyRef != null)
                    {
                        postBuilder.QuotePost = BbCommonRefToStrongReference(quotedPost.BlueskyRef)!;
                    }
                    else
                    {
                        byte[] quotedPostData = quotedPost.ImageData!;
                    
                        await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                        {
                            AtProtoHttpResult<EmbeddedImage> atResult = await agent.UploadImage(quotedPostData, "image/png",
                                DifferentPlatformQuoteImageAltText, null);
                            atResult.EnsureSucceeded();
                            
                            postBuilder.Add(atResult.Result);
                        });
                    
                        if (!string.IsNullOrEmpty(postBuilder.Text))
                        {
                            postBuilder.Append('\n', 2);
                        }

                        postBuilder.Append(new Link(quotedPost.GetPostUrlOnPrimaryPlatform(),
                            quotedPost.GetPrimaryPlatform().ToEmoji() + "\u00a0original post"));
                    }
                }

                foreach (ObjectId mediaId in item.AttachedMedia)
                {
                    byte[] data = attachmentCache.GetMediaData(mediaId, SocialPlatform.Bluesky);
                    string mimeType = attachmentCache.GetMediaMimeType(mediaId, SocialPlatform.Bluesky);
                    string altText = attachmentCache.GetMediaAltText(mediaId);
                    (int width, int height) aspectRatioTuple = attachmentCache.GetMediaAspectRatio(mediaId);
                    AspectRatio aspectRatio = new AspectRatio(aspectRatioTuple.width, aspectRatioTuple.height);

                    if (mimeType.StartsWith("image/"))
                    {
                        if (postBuilder.HasVideo)
                        {
                            throw new Exception("Can't have images at the same time as videos in one post on Bluesky");
                        }

                        await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                        {
                            AtProtoHttpResult<EmbeddedImage> atResult =
                                await agent.UploadImage(data, mimeType, altText, aspectRatio);
                            atResult.EnsureSucceeded();

                            postBuilder.Add(atResult.Result);
                        });
                    }
                    else
                    {
                        if (postBuilder.HasVideo)
                        {
                            throw new Exception("Can't have more than one video in a post on Bluesky");
                        }

                        if (postBuilder.HasImages)
                        {
                            throw new Exception("Can't have images at the same time as videos in one post on Bluesky");
                        }

                        await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                        {
                            // Based on https://bluesky.idunno.dev/docs/video.html

                            AtProtoHttpResult<JobStatus> atResult = await agent.UploadVideo("video.mp4", data);
                            atResult.EnsureSucceeded();

                            while (atResult.Succeeded && (atResult.Result.State == JobState.Created ||
                                                          atResult.Result.State == JobState.InProgress))
                            {
                                await Task.Delay(1000);
                                atResult = await agent.GetVideoJobStatus(atResult.Result.JobId);
                                atResult.EnsureSucceeded();
                            }

                            if (!atResult.Succeeded || atResult.Result.Blob is null ||
                                atResult.Result.State != JobState.Completed)
                            {
                                throw new Exception(
                                    $"Video upload failed with error {atResult.AtErrorDetail?.Error} and detail {atResult.AtErrorDetail?.Message}");
                            }

                            postBuilder.Add(new EmbeddedVideo(atResult.Result.Blob!, altText: altText));
                        });
                    }
                }
                
                Post blueskyPost = postBuilder.ToPost();

                if (previousPost != null)
                {
                    // HACK: idunno.Bluesky has a bug where it doesn't allow both a post to both be a quote and a reply
                    // at the same time, even though this works just fine. Hack around it by emulating the behaviour of
                    // postBuilder.InReplyTo with reflection.
                    PropertyInfo replyProperty =
                        typeof(Post).GetProperty("Reply", BindingFlags.Public | BindingFlags.Instance)!;
                    replyProperty.SetValue(blueskyPost, new ReplyReferences(previousPost, rootPost!));
                }
                
                await _retryResiliencePipeline.ExecuteAsync(async (token) =>
                {
                    AtProtoHttpResult<CreateRecordResult> atResult = await agent.Post(blueskyPost);
                    atResult.EnsureSucceeded();

                    previousPost = atResult.Result.StrongReference;
                });

                if (rootPost == null)
                {
                    rootPost = previousPost;
                }

                BlueskyRef StrongReferenceToBbCommonRef(StrongReference strongRef)
                {
                    return new BlueskyRef()
                    {
                        Cid = strongRef.Cid.ToString(),
                        Uri = strongRef.Uri.ToString()
                    };
                }

                item.BlueskyRootRef = StrongReferenceToBbCommonRef(rootPost!);
                item.BlueskyThisRef = StrongReferenceToBbCommonRef(previousPost!);
            }
        }
        finally
        {
            try
            {
                await agent.Logout();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred while logging out of Bluesky");
            }
        }
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

                byte[] quotedPostData = quotedPost.ImageData!;
                
                using MemoryStream quotedPostStream = new MemoryStream(quotedPostData);

                await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                {
                    attachments.Add(await client.UploadMedia(quotedPostStream,
                        description: DifferentPlatformQuoteImageAltText));
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
                    text += quotedPost.GetPrimaryPlatform().ToEmoji() + "\u00a0" +
                            quotedPost.GetPostUrlOnPrimaryPlatform();
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
        ThreadsClient client = new ThreadsClient(_socialSettings.ThreadsAppId!.Value, _socialSettings.ThreadsAppSecret!)
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

            List<(string mediaUrl, string mimeType, string altText)> attachments = new List<(string, string, string)>();

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
                    attachments.Add((quotedPost.ImageUrl!, "image/png", DifferentPlatformQuoteImageAltText));
                    
                    if (text != "")
                    {
                        text += "\n\n";
                    }

                    text += quotedPost.GetPrimaryPlatform().ToEmoji() + "\u00a0" +
                            quotedPost.GetPostUrlOnPrimaryPlatform();
                }
            }

            foreach (ObjectId attachmentId in item.AttachedMedia)
            {
                attachments.Add((await attachmentCache.GetMediaPreSignedUrl(attachmentId),
                    attachmentCache.GetMediaMimeType(attachmentId, SocialPlatform.Threads),
                    attachmentCache.GetMediaAltText(attachmentId)));
            }

            string containerId = null!;
            
            if (attachments.Count > 1)
            {
                List<string> subIds = new List<string>();

                foreach ((string mediaUrl, string mimeType, string altText) in attachments)
                {
                    await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                    {
                        string subId;
                        
                        if (mimeType.StartsWith("image/"))
                        {
                            subId = await client.Publishing_CreateImageMediaContainer(mediaUrl, isCarouselItem: true,
                                altText: altText);
                        }
                        else
                        {
                            subId = await client.Publishing_CreateVideoMediaContainer(mediaUrl, isCarouselItem: true,
                                altText: altText);
                        }
                        
                        subIds.Add(subId);
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
                (string mediaUrl, string mimeType, string altText) attachment = attachments[0];
                
                await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                {
                    if (attachment.mimeType.StartsWith("image/"))
                    {
                        containerId = await client.Publishing_CreateImageMediaContainer(attachment.mediaUrl, text,
                            previousId, quotedPostId: quotedPostId, altText: attachment.altText);
                    }
                    else
                    {
                        containerId = await client.Publishing_CreateVideoMediaContainer(attachment.mediaUrl, text,
                            previousId, quotedPostId: quotedPostId, altText: attachment.altText);
                    }
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

    public async Task DeletePostThreadFromSocialPlatforms(ObjectId threadId)
    {
        PostThread? postThread = _postThreadCollection.AsQueryable().FirstOrDefault(p => p._id == threadId);

        if (postThread == null)
        {
            throw new KeyNotFoundException($"Post thread {threadId} was not found");
        }

        if (postThread.State != PostThreadState.Sent)
        {
            throw new InvalidOperationException($"Post thread {threadId} is not in Sent state");
        }

        _logger.LogInformation("Deleting social posts for thread {id}", postThread._id.ToString());

        AccountGroup group = _accountGroupManager.GetAccountGroup(postThread.TargetGroup);

        if (postThread.PostToTwitter && group.Twitter != null &&
            !string.IsNullOrEmpty(_socialSettings.TwitterClientId) &&
            !string.IsNullOrEmpty(_socialSettings.TwitterClientSecret))
        {
            await DeleteFromTwitter(postThread, group);
        }

        if (postThread.PostToBluesky && group.Bluesky != null)
        {
            await DeleteFromBluesky(postThread, group.Bluesky);
        }

        if (postThread.PostToMastodon && group.Mastodon != null)
        {
            await DeleteFromMastodon(postThread, group.Mastodon);
        }

        if (postThread.PostToThreads && group.Threads != null)
        {
            await DeleteFromThreads(postThread, group.Threads);
        }

        _logger.LogInformation("Deleted social posts for thread {id}", postThread._id.ToString());

        await UpdateThreadState(postThread, PostThreadState.Deleted);
    }

    private async Task DeleteFromTwitter(PostThread postThread, AccountGroup group)
    {
        TwitterClient client =
            new TwitterClient(_socialSettings.TwitterClientId!, _socialSettings.TwitterClientSecret!);
        await client.Login(group.Twitter!.RefreshToken);

        try
        {
            foreach (PostThreadItem item in postThread.Items.AsEnumerable().Reverse())
            {
                if (item.TwitterId == null)
                {
                    continue;
                }

                try
                {
                    await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                    {
                        await client.DeleteTweet(item.TwitterId);
                    });
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to delete thread item from Twitter for thread {id}",
                        postThread._id.ToString());
                }
            }
        }
        finally
        {
            try
            {
                await client.Logout();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred while logging out of Twitter");
            }

            await _accountGroupManager.UpdateTwitterRefreshTokenForGroup(group, client.RefreshToken!);
        }
    }

    private async Task DeleteFromBluesky(PostThread postThread, BlueskyAccount account)
    {
        BlueskyAgent agent = new BlueskyAgent();
        await agent.Login(account.Identifier, account.Password);

        try
        {
            foreach (PostThreadItem item in postThread.Items.AsEnumerable().Reverse())
            {
                if (item.BlueskyThisRef == null)
                {
                    continue;
                }

                try
                {
                    StrongReference postRef = new StrongReference(item.BlueskyThisRef.Uri, item.BlueskyThisRef.Cid);

                    await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                    {
                        await agent.DeleteRecord(postRef);
                    });
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to delete thread item from Bluesky for thread {id}",
                        postThread._id.ToString());
                }
            }
        }
        finally
        {
            try
            {
                await agent.Logout();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occurred while logging out of Bluesky");
            }
        }
    }

    private async Task DeleteFromMastodon(PostThread postThread, MastodonAccount account)
    {
        MastodonClient client = new MastodonClient(account.InstanceUrl, account.AccessToken);

        foreach (PostThreadItem item in postThread.Items.AsEnumerable().Reverse())
        {
            if (item.MastodonId == null)
            {
                continue;
            }

            try
            {
                await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                {
                    await client.DeleteStatus(item.MastodonId);
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to delete thread item from Mastodon for thread {id}",
                    postThread._id.ToString());
            }
        }
    }

    private async Task DeleteFromThreads(PostThread postThread, ThreadsAccount account)
    {
        ThreadsClient client = new ThreadsClient(_socialSettings.ThreadsAppId!.Value, _socialSettings.ThreadsAppSecret!)
        {
            Credentials = new ThreadsCredentials()
            {
                CredentialType = ThreadsCredentialType.LongLived,
                AccessToken = account.AccessToken,
                Expiry = account.Expiry,
                UserId = account.UserId
            }
        };

        foreach (PostThreadItem item in postThread.Items.AsEnumerable().Reverse())
        {
            if (item.ThreadsId == null)
            {
                continue;
            }

            try
            {
                await _retryResiliencePipeline.ExecuteAsync(async (_) =>
                {
                    await client.Publishing_DeleteMediaContainer(item.ThreadsId);
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to delete thread item from Threads for thread {id}",
                    postThread._id.ToString());
            }
        }
    }
}
