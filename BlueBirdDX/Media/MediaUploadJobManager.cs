using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Storage;
using BlueBirdDX.Config;
using BlueBirdDX.Config.Storage;
using BlueBirdDX.Config.Video;
using BlueBirdDX.Database;
using BlueBirdDX.Util;
using FFMpegCore;
using FFMpegCore.Enums;
using MongoDB.Bson;
using MongoDB.Driver;
using NATS.Client.Core;
using NATS.Net;
using Serilog;
using Serilog.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace BlueBirdDX.Media;

public class MediaUploadJobManager
{
    // https://developer.x.com/en/docs/x-api/v1/media/upload-media/uploading-media/media-best-practices
    private const int TwitterMaximumImageSize = 5242880;
    private const int TwitterMaximumVideoSize = 536870912;
    
    // https://github.com/bluesky-social/social-app/blob/f0cd8ab6f46f45c79de5aaf6eb7def782dc99836/src/state/models/media/image.ts#L22
    private const int BlueskyMaximumImageSize = 976560;
    // https://bsky.app/profile/layeredstrange.uk/post/3l4qpvrexds2y
    private const int BlueskyMaximumVideoSize = 52428800;
    
    // https://docs.joinmastodon.org/user/posting/
    private const int MastodonMaximumImageSize = 16777216;
    private const int MastodonMaximumVideoSize = 103809024;
    
    // https://support.buffer.com/article/617-ideal-image-sizes-and-formats-for-your-posts
    private const int ThreadsMaximumImageSize = 8388608;
    // https://help.gainapp.com/article/218-creating-content-for-threads
    // Technically, the limit is 1GB+, but let's not upload videos that size. I'll use Twitter's limit instead.
    private const int ThreadsMaximumVideoSize = TwitterMaximumVideoSize;

    private const int VideoFileSizeMargin = 5242880;
    private const int VideoTargetAudioBitrate = 128;
    
    private static readonly ILogger LogContext =
        Log.ForContext(Constants.SourceContextPropertyName, "MediaUploadJobManager");
    
    private static MediaUploadJobManager? _instance;
    public static MediaUploadJobManager Instance => _instance!;

    private readonly NatsClient _natsClient;
    private readonly RemoteStorage _remoteStorage;
    private readonly IMongoCollection<MediaUploadJob> _uploadJobCollection;
    private readonly IMongoCollection<UploadedMedia> _mediaCollection;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    private readonly bool _isVideoAvailable;

    public MediaUploadJobManager()
    {
        _natsClient = new NatsClient(BbConfig.Instance.Notification.Server);
        
        RemoteStorageConfig storageConfig = BbConfig.Instance.RemoteStorage;
        
        _remoteStorage = new RemoteStorage(storageConfig.ServiceUrl, storageConfig.Bucket, storageConfig.AccessKey,
            storageConfig.AccessKeySecret);

        _uploadJobCollection = DatabaseManager.Instance.GetCollection<MediaUploadJob>("media_jobs");
        _mediaCollection = DatabaseManager.Instance.GetCollection<UploadedMedia>("media");

        VideoConfig videoConfig = BbConfig.Instance.Video;

        if (videoConfig.FFmpegBinariesFolder != null || videoConfig.FFmpegBinariesFolder == "")
        {
            GlobalFFOptions.Configure(new FFOptions()
            {
                BinaryFolder = videoConfig.FFmpegBinariesFolder,
                TemporaryFilesFolder = videoConfig.TemporaryFolder
            });

            _isVideoAvailable = true;
        }
    }
    
    public static void Initialize()
    {
        _instance = new MediaUploadJobManager();
    }

    private async Task<byte[]> ProcessImage(UploadedMedia media, byte[] imageData,
        Dictionary<SocialPlatform, byte[]> optimizedImages)
    {
        using MemoryStream inputStream = new MemoryStream(imageData);
        using Image image = await Image.LoadAsync(inputStream);

        media.Width = image.Width;
        media.Height = image.Height;

        async Task ResizeImageToFitSizeLimit(Image targetImage, int maxSize, SocialPlatform platform)
        {
            int quality = 100;
            byte[] resizedData = imageData;

            while (resizedData.Length > maxSize)
            {
                if (quality == 0)
                {
                    throw new Exception($"Image is too large and can't be resized to fit within {maxSize} bytes");
                }
            
                using MemoryStream outputStream = new MemoryStream();

                await targetImage.SaveAsync(outputStream, new JpegEncoder()
                {
                    Quality = quality
                });

                quality--;

                resizedData = outputStream.ToArray();
            }

            optimizedImages[platform] = resizedData;
        }

        if (imageData.Length > TwitterMaximumImageSize)
        {
            await ResizeImageToFitSizeLimit(image, TwitterMaximumImageSize, SocialPlatform.Twitter);
            media.HasTwitterOptimizedVersion = true;
        }

        if (imageData.Length > BlueskyMaximumImageSize)
        {
            await ResizeImageToFitSizeLimit(image, BlueskyMaximumImageSize, SocialPlatform.Bluesky);
            media.HasBlueskyOptimizedVersion = true;
        }

        if (imageData.Length > MastodonMaximumImageSize)
        {
            await ResizeImageToFitSizeLimit(image, MastodonMaximumImageSize, SocialPlatform.Mastodon);
            media.HasMastodonOptimizedVersion = true;
        }

        if (imageData.Length > ThreadsMaximumImageSize)
        {
            await ResizeImageToFitSizeLimit(image, ThreadsMaximumImageSize, SocialPlatform.Threads);
            media.HasThreadsOptimizedVersion = true;
        }

        return imageData;
    }

    private async Task<byte[]> ProcessVideo(UploadedMedia media, byte[] videoData,
        Dictionary<SocialPlatform, byte[]> optimizedVideos)
    {
        string path = Path.Combine(BbConfig.Instance.Video.TemporaryFolder, Path.GetRandomFileName());

        await File.WriteAllBytesAsync(path, videoData);

        IMediaAnalysis analysis = await FFProbe.AnalyseAsync(path);

        media.MimeType = "video/mp4"; // We will re-encode the input video to MP4.
        media.Width = analysis.PrimaryVideoStream!.Width;
        media.Height = analysis.PrimaryVideoStream!.Height;

        string standardPath = Path.Combine(BbConfig.Instance.Video.TemporaryFolder, Path.GetRandomFileName() + ".mp4");
        
        bool returnCode = FFMpegArguments
            .FromFileInput(path)
            .OutputToFile(standardPath, false, options => options
                .WithVideoCodec(VideoCodec.LibX264)
                .WithAudioCodec(AudioCodec.Aac))
            .ProcessSynchronously();

        if (!returnCode)
        {
            throw new Exception("Failed to re-encode video to H.264 & AAC");
        }

        byte[] standardVideo = await File.ReadAllBytesAsync(standardPath);

        // This method implements the two-pass algorithm found here:
        // https://trac.ffmpeg.org/wiki/Encode/H.264#twopass
        
        async Task EncodeVideoForPlatform(SocialPlatform platform, int maximumSize)
        {
            int targetSize = maximumSize - VideoFileSizeMargin;

            double targetVideoBitrate =
                ((targetSize * 8388.608) / analysis.Duration.TotalSeconds) - VideoTargetAudioBitrate;

            int roundedVideoBitrate = (int)Math.Round(targetVideoBitrate / 100d, 0) * 100;

            bool localReturnCode = FFMpegArguments
                .FromFileInput(path)
                .OutputToFile("/dev/null", true, options => options
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithVideoBitrate(roundedVideoBitrate)
                    .WithCustomArgument("-fps_mode cfr")
                    .WithCustomArgument("-pass 1")
                    .ForceFormat("null"))
                .ProcessSynchronously();

            if (!localReturnCode)
            {
                throw new Exception("First pass for platform-specific video encoding failed");
            }

            string outPath = Path.Combine(BbConfig.Instance.Video.TemporaryFolder, Path.GetRandomFileName() + ".mp4");
            
            localReturnCode = FFMpegArguments
                .FromFileInput(path)
                .OutputToFile(outPath, false, options => options
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithVideoBitrate(roundedVideoBitrate)
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithAudioBitrate((int)VideoTargetAudioBitrate)
                    .WithCustomArgument("-pass 2"))
                .ProcessSynchronously();

            if (!localReturnCode)
            {
                throw new Exception("Second pass for platform-specific video re-encoding failed");
            }

            optimizedVideos[platform] = await File.ReadAllBytesAsync(outPath);
        }
        
        if (standardVideo.Length > TwitterMaximumVideoSize)
        {
            await EncodeVideoForPlatform(SocialPlatform.Twitter, TwitterMaximumVideoSize);
            media.HasTwitterOptimizedVersion = true;
        }

        if (standardVideo.Length > BlueskyMaximumVideoSize)
        {
            await EncodeVideoForPlatform(SocialPlatform.Bluesky, BlueskyMaximumVideoSize);
            media.HasBlueskyOptimizedVersion = true;
        }

        if (standardVideo.Length > MastodonMaximumVideoSize)
        {
            await EncodeVideoForPlatform(SocialPlatform.Mastodon, MastodonMaximumVideoSize);
            media.HasMastodonOptimizedVersion = true;
        }

        if (standardVideo.Length > ThreadsMaximumVideoSize)
        {
            await EncodeVideoForPlatform(SocialPlatform.Threads, ThreadsMaximumVideoSize);
            media.HasThreadsOptimizedVersion = true;
        }
        
        return standardVideo;
    }

    private async Task ProcessMediaJob(MediaUploadJob uploadJob)
    {
        UploadedMedia media;
        string unprocessedFileName;

        if (uploadJob.IsJobForMigrationTwoToThree)
        {
            LogContext.Information("Processing media job {jobId} with two -> three migration for media ID {mediaId}",
                uploadJob._id.ToString(), uploadJob._id.ToString());
            
            media = _mediaCollection.AsQueryable().FirstOrDefault(m => m._id == uploadJob.MediaId)!;

            unprocessedFileName = "media/" + media._id.ToString();
        }
        else
        {
            ObjectId mediaId = ObjectId.GenerateNewId();
            
            media = new UploadedMedia()
            {
                _id = mediaId,
                SchemaVersion = UploadedMedia.LatestSchemaVersion,
                Name = uploadJob.Name,
                AltText = uploadJob.AltText,
                MimeType = uploadJob.MimeType,
                CreationTime = DateTime.UtcNow
            };

            LogContext.Information("Processing media job {jobId} with new media ID {mediaId}", uploadJob._id.ToString(),
                mediaId.ToString());

            unprocessedFileName = "unprocessed_media/" + uploadJob._id.ToString();
        }
        
        try
        {
            byte[] data = await _remoteStorage.DownloadFile(unprocessedFileName);

            byte[] standardData;

            Dictionary<SocialPlatform, byte[]> optimizedData = new Dictionary<SocialPlatform, byte[]>();

            if (uploadJob.MimeType.StartsWith("image/") || uploadJob.IsJobForMigrationTwoToThree)
            {
                standardData = await ProcessImage(media, data, optimizedData);
            }
            else if (uploadJob.MimeType.StartsWith("video/"))
            {
                if (!_isVideoAvailable)
                {
                    throw new NotSupportedException("Video support is not configured");
                }
                
                standardData = await ProcessVideo(media, data, optimizedData);
            }
            else
            {
                throw new NotImplementedException($"Mime type {uploadJob.MimeType} is not supported");
            }
            
            string fileName = $"media/{media._id.ToString()}";

            if (!uploadJob.IsJobForMigrationTwoToThree)
            {
                await _remoteStorage.DeleteFile(unprocessedFileName);
            
                _remoteStorage.TransferFile(fileName, standardData, media.MimeType);
            }

            foreach (KeyValuePair<SocialPlatform, byte[]> pair in optimizedData)
            {
                _remoteStorage.TransferFile($"{fileName}_{pair.Key.ToString().ToLower()}", pair.Value, "image/jpeg");
            }

            if (!uploadJob.IsJobForMigrationTwoToThree)
            {
                await _mediaCollection.InsertOneAsync(media);
            }
            else
            {
                await _mediaCollection.ReplaceOneAsync(Builders<UploadedMedia>.Filter.Eq(m => m._id, media._id), media);
            }

            uploadJob.State = MediaUploadJobState.Success;
            uploadJob.MediaId = media._id;

            LogContext.Information("Finished processing media job {jobId}", uploadJob._id.ToString());
        }
        catch (Exception e)
        {
            LogContext.Error(e, "An error occurred while processing media job {jobId}", uploadJob._id.ToString());
            
            uploadJob.State = MediaUploadJobState.Failed;
            uploadJob.ErrorDetail = $"{e.GetType().Name} occurred while trying to process the media: {e.Message}";

            try
            {
                if (!uploadJob.IsJobForMigrationTwoToThree)
                {
                    await _remoteStorage.DeleteFile(unprocessedFileName);
                }
            }
            catch (Exception e2)
            {
                LogContext.Error(e2, "Additionally, an error occurred while deleting the unprocessed media for {jobId}", uploadJob._id.ToString());
                
                uploadJob.ErrorDetail +=
                    $"(additionally, {e2.GetType().Name} occurred while trying to delete the unprocessed media)";
            }
        }

        await _uploadJobCollection.ReplaceOneAsync(Builders<MediaUploadJob>.Filter.Eq(j => j._id, uploadJob._id),
            uploadJob);
    }

    public async Task ProcessAllWaitingReadyMediaJobs()
    {
        LogContext.Information("Attempting to process all waiting media jobs");
        
        await _semaphore.WaitAsync();

        try
        {
            foreach (MediaUploadJob uploadJob in _uploadJobCollection.AsQueryable()
                         .Where(j => j.State == MediaUploadJobState.Ready))
            {
                await ProcessMediaJob(uploadJob);
            }
        }
        catch (Exception e)
        {
            LogContext.Error(e, "Unexpected error during processing of waiting jobs");
        }
        finally
        {
            _semaphore.Release();
        }
        
        LogContext.Information("All waiting media jobs have been processed");
    }

    public async Task ListenForReadyMediaJobs()
    {
        await foreach (NatsMsg<string> message in _natsClient.SubscribeAsync<string>("media.jobs.ready"))
        {
            ObjectId objectId = ObjectId.Parse(message.Data);
            MediaUploadJob uploadJob = _uploadJobCollection.AsQueryable().FirstOrDefault(j => j._id == objectId)!;

            _ = Task.Run(async () =>
            {
                await _semaphore.WaitAsync();

                try
                {
                    await ProcessMediaJob(uploadJob);
                }
                catch (Exception e)
                {
                    LogContext.Error(e, "Unexpected error during processing a ready job");
                }
                finally
                {
                    _semaphore.Release();
                }
            });
        }
    }
}