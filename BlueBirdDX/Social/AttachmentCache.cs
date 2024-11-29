using System.Net;
using System.Security.Cryptography;
using System.Text;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Common.Storage;
using BlueBirdDX.Config;
using BlueBirdDX.Config.Storage;
using BlueBirdDX.Config.WebDriver;
using BlueBirdDX.Database;
using BlueBirdDX.Util;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Airship.ATProtocol.Lexicon.Types;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace BlueBirdDX.Social;

public class AttachmentCache
{
    // https://developer.x.com/en/docs/x-api/v1/media/upload-media/uploading-media/media-best-practices
    private const int TwitterMaximumImageSize = 5242880;
    
    // https://github.com/bluesky-social/social-app/blob/f0cd8ab6f46f45c79de5aaf6eb7def782dc99836/src/state/models/media/image.ts#L22
    private const int BlueskyMaximumImageSize = 976560;
    
    // https://docs.joinmastodon.org/user/posting/
    private const int MastodonMaximumImageSize = 16777216;
    
    // https://support.buffer.com/article/617-ideal-image-sizes-and-formats-for-your-posts
    private const int ThreadsMaximumImageSize = 8388608;
    
    private readonly RemoteStorage _remoteStorage;
    private readonly IMongoCollection<UploadedMedia> _uploadedMediaCollection;
    private readonly IMongoCollection<PostThread> _postThreadCollection;

    private readonly Dictionary<ObjectId, UploadedMedia>
        _mediaDocumentCache = new Dictionary<ObjectId, UploadedMedia>();
    private readonly Dictionary<ObjectId, byte[]> _mediaDataCache = new Dictionary<ObjectId, byte[]>();
    private readonly Dictionary<SocialPlatform, Dictionary<ObjectId, byte[]>> _mediaResizedDataCache =
        new Dictionary<SocialPlatform, Dictionary<ObjectId, byte[]>>()
        {
            { SocialPlatform.Twitter, new Dictionary<ObjectId, byte[]>() },
            { SocialPlatform.Bluesky, new Dictionary<ObjectId, byte[]>() },
            { SocialPlatform.Mastodon, new Dictionary<ObjectId, byte[]>() },
            { SocialPlatform.Threads, new Dictionary<ObjectId, byte[]>() }
        };
    
    private readonly Dictionary<string, byte[]> _quotedPostImageCache = new Dictionary<string, byte[]>();

    private readonly Dictionary<string, QuotedPost> _quotedPosts;
    
    public AttachmentCache()
    {
        RemoteStorageConfig storageConfig = BbConfig.Instance.RemoteStorage;
        
        _remoteStorage = new RemoteStorage(storageConfig.ServiceUrl, storageConfig.Bucket, storageConfig.AccessKey,
            storageConfig.AccessKeySecret);
        
        _uploadedMediaCollection = DatabaseManager.Instance.GetCollection<UploadedMedia>("media");
        _postThreadCollection = DatabaseManager.Instance.GetCollection<PostThread>("threads");

        _quotedPosts = new Dictionary<string, QuotedPost>();
    }

    public string GetMediaMimeType(ObjectId mediaId, SocialPlatform platform)
    {
        if (_mediaResizedDataCache[platform].ContainsKey(mediaId))
        {
            return "image/jpeg";
        }
        
        return _mediaDocumentCache[mediaId].MimeType;
    }

    public string GetMediaAltText(ObjectId mediaId)
    {
        return _mediaDocumentCache[mediaId].AltText;
    }
    
    public byte[] GetMediaData(ObjectId mediaId, SocialPlatform platform)
    {
        if (_mediaResizedDataCache[platform].TryGetValue(mediaId, out byte[]? data))
        {
            return data;
        }
        
        return _mediaDataCache[mediaId];
    }

    public string GetMediaPreSignedUrl(ObjectId mediaId)
    {
        return _remoteStorage.GetPreSignedUrlForFile("media/" + mediaId.ToString(), 15);
    }
    
    public async Task AddMediaToCache(ObjectId mediaId)
    {
        if (_mediaDocumentCache.ContainsKey(mediaId))
        {
            return;
        }
        
        UploadedMedia media = _uploadedMediaCollection.AsQueryable().FirstOrDefault(m => m._id == mediaId)!;
        _mediaDocumentCache[mediaId] = media;

        byte[] data = await _remoteStorage.DownloadFile("media/" + mediaId.ToString());
        _mediaDataCache[mediaId] = data;

        async Task<byte[]> ResizeImageToFitSizeLimit(int maxSize)
        {
            int quality = 100;
            byte[] resizedData = data;

            while (resizedData.Length > maxSize)
            {
                if (quality == 0)
                {
                    throw new Exception(
                        $"Image {mediaId} is too large and can't be resized to fit within {maxSize} bytes");
                }
            
                using MemoryStream inputStream = new MemoryStream(data);
                using MemoryStream outputStream = new MemoryStream();
                
                using Image image = await Image.LoadAsync(inputStream);

                await image.SaveAsync(outputStream, new JpegEncoder()
                {
                    Quality = quality
                });

                quality--;

                resizedData = outputStream.ToArray();
            }

            return resizedData;
        }

        if (data.Length > TwitterMaximumImageSize)
        {
            _mediaResizedDataCache[SocialPlatform.Twitter][mediaId] =
                await ResizeImageToFitSizeLimit(TwitterMaximumImageSize);
        }

        if (data.Length > BlueskyMaximumImageSize)
        {
            _mediaResizedDataCache[SocialPlatform.Bluesky][mediaId] =
                await ResizeImageToFitSizeLimit(BlueskyMaximumImageSize);
        }

        if (data.Length > MastodonMaximumImageSize)
        {
            _mediaResizedDataCache[SocialPlatform.Mastodon][mediaId] =
                await ResizeImageToFitSizeLimit(MastodonMaximumImageSize);
        }

        if (data.Length > ThreadsMaximumImageSize)
        {
            _mediaResizedDataCache[SocialPlatform.Threads][mediaId] =
                await ResizeImageToFitSizeLimit(ThreadsMaximumImageSize);
        }
    }

    private string ComputeRemoteFileNameForQuotedPost(string url)
    {
        using SHA256 sha = SHA256.Create();
        
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
        
        return "quoted_posts/" + Convert.ToHexString(hash).ToLower();
    }

    public async Task AddQuotedPostToCache(string url)
    {
        Uri uri = new Uri(url);

        string sanitizedUrl = url;

        if (uri.Host == "x.com")
        {
            sanitizedUrl = sanitizedUrl.Replace("x.com", "twitter.com");
        }
        
        int queryParametersIdx = sanitizedUrl.IndexOf('?');
        
        if (queryParametersIdx != -1)
        {
            sanitizedUrl = sanitizedUrl.Substring(0, queryParametersIdx);
        }
        
        QuotedPost quotedPost = new QuotedPost()
        {
            SanitizedUrl = sanitizedUrl
        };
        
        if (uri.Host == "x.com" || uri.Host == "twitter.com")
        {
            quotedPost.TwitterId = sanitizedUrl.Split('/')[^1];
            
            PostThreadItem? postThreadItem = _postThreadCollection.AsQueryable().SelectMany(t => t.Items)
                .FirstOrDefault(i => i.TwitterId == quotedPost.TwitterId);

            if (postThreadItem != null)
            {
                quotedPost.BlueskyRef = postThreadItem.BlueskyThisRef != null
                    ? new StrongRef(postThreadItem.BlueskyThisRef.Uri, postThreadItem.BlueskyThisRef.Cid)
                    : null;
                quotedPost.MastodonId = postThreadItem.MastodonId;
                quotedPost.ThreadsId = postThreadItem.ThreadsId;
            }
        }
        else
        {
            throw new NotImplementedException("Unsupported URL");
        }

        _quotedPosts[url] = quotedPost;
        
        ChromeOptions options = new ChromeOptions();
        options.AddArgument("--ignore-certificate-errors");
        options.AddArgument("--hide-scrollbars");
        options.AddArgument("--disable-dev-shm-usage"); // avoid "session deleted because of page crash"

        WebDriverConfig config = BbConfig.Instance.WebDriver;

        using RemoteWebDriver remoteWebDriver = new RemoteWebDriver(new Uri(config.NodeUrl), options);

        remoteWebDriver.Navigate()
            .GoToUrl(string.Format(config.ScreenshotUrlFormat, "tweet", WebUtility.UrlEncode(sanitizedUrl)));
        
        IWebElement? iframeElement = null;
    
        WebDriverWait wait = new WebDriverWait(remoteWebDriver, TimeSpan.FromSeconds(30));
        wait.Until((driver) =>
        {
            try
            {
                // This element appears when the embed has fully loaded.
                driver.FindElement(By.Id("rufous-sandbox"));

                iframeElement = driver.FindElement(By.Id("twitter-widget-0"));
            }
            catch (Exception)
            {
                return false;
            }

            string visibility = iframeElement.GetCssValue("visibility");
            
            if (visibility != "visible")
            {
                return false;
            }

            return true;
        });

        int ParsePixelValue(string val)
        {
            // strip "px" and parse
            return int.Parse(val.Substring(0, val.Length - 2));
        }

        int iframeWidth = ParsePixelValue(iframeElement!.GetCssValue("width"));
        int iframeHeight = ParsePixelValue(iframeElement!.GetCssValue("height"));
    
        ICollection<object> collection = (ICollection<object>)remoteWebDriver.ExecuteScript("return [window.outerWidth - window.innerWidth + arguments[0], window.outerHeight - window.innerHeight + arguments[1]]", new object[] { iframeWidth + 30, iframeHeight + 30 });
        IList<object> size = collection.ToList();
        remoteWebDriver.Manage().Window.Size = new System.Drawing.Size((int)(Int64)size[0], (int)(Int64)size[1]);
        
        // Wait an extra 5 seconds to let anything that isn't done loading finish.
        Thread.Sleep(5000);

        Screenshot screenshot = remoteWebDriver.GetScreenshot();

        byte[] data = screenshot.AsByteArray;

        _quotedPostImageCache[url] = data;
        
        _remoteStorage.TransferFile(ComputeRemoteFileNameForQuotedPost(url), data, "image/png");
    }

    public QuotedPost GetQuotedPost(string url)
    {
        return _quotedPosts[url];
    }

    public byte[] GetQuotedPostImageData(string url)
    {
        return _quotedPostImageCache[url];
    }

    public string GetQuotedPostImagePreSignedUrl(string url)
    {
        return _remoteStorage.GetPreSignedUrlForFile(ComputeRemoteFileNameForQuotedPost(url), 15);
    }
}
