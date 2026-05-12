using Amazon.Runtime;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Util;
using BlueBirdDX.Database;
using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;
using OatmealDome.Slab.S3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace BlueBirdDX.Media;

public class MediaUploadJobManager : BackgroundService
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
    
    private const int OneMebibyte = 1048576;
    
    private const int VideoFileSizeMargin = 5 * OneMebibyte;
    private const int VideoTargetAudioBitrate = 128;
    
    private readonly ILogger<MediaUploadJobManager> _logger;
    private readonly MediaUploadJobManagerConfiguration _settings;
    private readonly SlabS3Service _s3Service;
    private readonly IMongoCollection<MediaUploadJob> _uploadJobCollection;
    private readonly IMongoCollection<UploadedMedia> _mediaCollection;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    private readonly bool _isVideoAvailable;

    public MediaUploadJobManager(ILogger<MediaUploadJobManager> logger,
        IOptions<MediaUploadJobManagerConfiguration> settings, SlabS3Service s3Service, SlabMongoService mongoService)
    {
        _logger = logger;
        _settings = settings.Value;
        _s3Service = s3Service;
        _uploadJobCollection = mongoService.GetCollection<MediaUploadJob>("media_jobs");
        _mediaCollection = mongoService.GetCollection<UploadedMedia>("media");

        if (_settings.FFmpegBinariesFolder != null || _settings.FFmpegBinariesFolder == "")
        {
            GlobalFFOptions.Configure(new FFOptions()
            {
                BinaryFolder = _settings.FFmpegBinariesFolder,
                TemporaryFilesFolder = _settings.TemporaryFolder
            });

            _isVideoAvailable = true;
        }
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessAllWaitingReadyMediaJobs();
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
        string path = Path.Combine(_settings.TemporaryFolder, Path.GetRandomFileName());

        await File.WriteAllBytesAsync(path, videoData);

        IMediaAnalysis analysis = await FFProbe.AnalyseAsync(path);

        media.MimeType = "video/mp4"; // We will re-encode the input video to MP4.
        media.Width = analysis.PrimaryVideoStream!.Width;
        media.Height = analysis.PrimaryVideoStream!.Height;

        string standardPath = Path.Combine(_settings.TemporaryFolder, Path.GetRandomFileName() + ".mp4");
        
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
            int targetSize = (maximumSize - VideoFileSizeMargin) / OneMebibyte;

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

            string outPath = Path.Combine(_settings.TemporaryFolder, Path.GetRandomFileName() + ".mp4");
            
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
            _logger.LogInformation("Processing media job {jobId} with two -> three migration for media ID {mediaId}",
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

            _logger.LogInformation("Processing media job {jobId} with new media ID {mediaId}", uploadJob._id.ToString(),
                mediaId.ToString());

            unprocessedFileName = "unprocessed_media/" + uploadJob._id.ToString();
        }
        
        await _uploadJobCollection.UpdateOneAsync(Builders<MediaUploadJob>.Filter.Eq(j => j._id, uploadJob._id),
            Builders<MediaUploadJob>.Update.Set(j => j.State, MediaUploadJobState.Processing));
        
        try
        {
            byte[] data = await _s3Service.DownloadFile(unprocessedFileName);

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
                await _s3Service.DeleteFile(unprocessedFileName);
            
                await _s3Service.TransferFile(fileName, standardData, media.MimeType);
            }

            foreach (KeyValuePair<SocialPlatform, byte[]> pair in optimizedData)
            {
                await _s3Service.TransferFile($"{fileName}_{pair.Key.ToString().ToLower()}", pair.Value, "image/jpeg");
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

            _logger.LogInformation("Finished processing media job {jobId}", uploadJob._id.ToString());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while processing media job {jobId}", uploadJob._id.ToString());
            
            uploadJob.State = MediaUploadJobState.Failed;
            uploadJob.ErrorDetail = $"{e.GetType().Name} occurred while trying to process the media: {e.Message}";

            try
            {
                if (!uploadJob.IsJobForMigrationTwoToThree)
                {
                    await _s3Service.DeleteFile(unprocessedFileName);
                }
            }
            catch (Exception e2)
            {
                _logger.LogError(e2, "Additionally, an error occurred while deleting the unprocessed media for {jobId}",
                    uploadJob._id.ToString());
                
                uploadJob.ErrorDetail +=
                    $"(additionally, {e2.GetType().Name} occurred while trying to delete the unprocessed media)";
            }
        }

        await _uploadJobCollection.ReplaceOneAsync(Builders<MediaUploadJob>.Filter.Eq(j => j._id, uploadJob._id),
            uploadJob);
    }

    public async Task ProcessAllWaitingReadyMediaJobs()
    {
        _logger.LogInformation("Attempting to process all waiting media jobs");
        
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
            _logger.LogError(e, "Unexpected error during processing of waiting jobs");
        }
        finally
        {
            _semaphore.Release();
        }
        
        _logger.LogInformation("All waiting media jobs have been processed");
    }

    public void ProcessReadyMediaJob(ObjectId jobId)
    {
        MediaUploadJob? uploadJob = _uploadJobCollection.AsQueryable().FirstOrDefault(j => j._id == jobId);

        if (uploadJob == null)
        {
            throw new KeyNotFoundException($"Media upload job {jobId.ToString()} was not found");
        }
        
        if (uploadJob.State != MediaUploadJobState.Ready)
        {
            _logger.LogWarning("Ignoring media job {jobId} because it is in {state} state", jobId.ToString(),
                uploadJob.State);
            return;
        }

        _ = Task.Run(async () =>
        {
            await _semaphore.WaitAsync();

            try
            {
                await ProcessMediaJob(uploadJob);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected error during processing media job {JobId}", uploadJob._id.ToString());
            }
            finally
            {
                _semaphore.Release();
            }
        });
    }

    private async Task CleanUpOldJob(MediaUploadJob uploadJob)
    {
        _logger.LogInformation("Deleting media job {jobId}", uploadJob._id.ToString());
        
        await _uploadJobCollection.DeleteOneAsync(Builders<MediaUploadJob>.Filter.Eq(j => j._id, uploadJob._id));
        
        if (!uploadJob.IsJobForMigrationTwoToThree)
        {
            string unprocessedFileName = "unprocessed_media/" + uploadJob._id.ToString();

            try
            {
                await _s3Service.DeleteFile(unprocessedFileName);
            }
            catch (AmazonServiceException)
            {
                // ignore, this probably means that the file doesn't exist
            }
        }
    }
    
    public async Task CleanUpOldJobs()
    {
        _logger.LogInformation("Cleaning up old media jobs");
        
        await _semaphore.WaitAsync();
        
        try
        {
            DateTime referenceNow = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            
            List<MediaUploadJob> oldJobs = _uploadJobCollection.AsQueryable().ToList();

            foreach (MediaUploadJob uploadJob in oldJobs)
            {
                if (referenceNow > uploadJob.CreationTime)
                {
                    await CleanUpOldJob(uploadJob);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected error during clean up");
        }
        finally
        {
            _semaphore.Release();
        }
        
        _logger.LogInformation("Finished clean up");
    }
}
