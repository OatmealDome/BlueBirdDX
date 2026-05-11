using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Amazon.S3;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Common.Social;
using BlueBirdDX.Common.Util;
using BlueBirdDX.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;
using OatmealDome.Slab.S3;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;

namespace BlueBirdDX.Social;

public class AttachmentCache
{
    private readonly SlabS3Service _s3Service;
    private readonly IMongoCollection<UploadedMedia> _uploadedMediaCollection;
    private readonly IMongoCollection<PostThread> _postThreadCollection;
    private readonly string _seleniumNodeUrl;
    private readonly string _webAppUrl;

    private readonly Dictionary<ObjectId, UploadedMedia>
        _mediaDocumentCache = new Dictionary<ObjectId, UploadedMedia>();
    private readonly Dictionary<ObjectId, byte[]> _mediaDataCache = new Dictionary<ObjectId, byte[]>();
    private readonly Dictionary<SocialPlatform, Dictionary<ObjectId, byte[]>> _mediaOptimizedDataCache =
        new Dictionary<SocialPlatform, Dictionary<ObjectId, byte[]>>()
        {
            { SocialPlatform.Twitter, new Dictionary<ObjectId, byte[]>() },
            { SocialPlatform.Bluesky, new Dictionary<ObjectId, byte[]>() },
            { SocialPlatform.Mastodon, new Dictionary<ObjectId, byte[]>() },
            { SocialPlatform.Threads, new Dictionary<ObjectId, byte[]>() }
        };

    private readonly Dictionary<string, QuotedPost> _quotedPosts = new Dictionary<string, QuotedPost>();

    public AttachmentCache(SlabS3Service s3Service, SlabMongoService mongoService, string seleniumNodeUrl,
        string webAppUrl)
    {
        _s3Service = s3Service;
        _uploadedMediaCollection = mongoService.GetCollection<UploadedMedia>("media");
        _postThreadCollection = mongoService.GetCollection<PostThread>("threads");
        _seleniumNodeUrl = seleniumNodeUrl;
        _webAppUrl = webAppUrl;
    }

    public string GetMediaMimeType(ObjectId mediaId, SocialPlatform platform)
    {
        UploadedMedia media = _mediaDocumentCache[mediaId];
        
        if (_mediaOptimizedDataCache[platform].ContainsKey(mediaId))
        {
            if (media.MimeType.StartsWith("image/"))
            {
                return "image/jpeg";
            }
            else
            {
                return "video/mp4";
            }
        }
        
        return media.MimeType;
    }

    public string GetMediaAltText(ObjectId mediaId)
    {
        return _mediaDocumentCache[mediaId].AltText;
    }

    public (int, int) GetMediaAspectRatio(ObjectId mediaId)
    {
        UploadedMedia media = _mediaDocumentCache[mediaId];

        return (media.Width, media.Height);
    }
    
    public byte[] GetMediaData(ObjectId mediaId, SocialPlatform platform)
    {
        if (_mediaOptimizedDataCache[platform].TryGetValue(mediaId, out byte[]? data))
        {
            return data;
        }
        
        return _mediaDataCache[mediaId];
    }

    public async Task<string> GetMediaPreSignedUrl(ObjectId mediaId)
    {
        return await _s3Service.GetPreSignedUrlForFile("media/" + mediaId.ToString(), HttpVerb.GET, 15);
    }
    
    public async Task AddMediaToCache(ObjectId mediaId)
    {
        if (_mediaDocumentCache.ContainsKey(mediaId))
        {
            return;
        }
        
        UploadedMedia media = _uploadedMediaCollection.AsQueryable().FirstOrDefault(m => m._id == mediaId)!;
        _mediaDocumentCache[mediaId] = media;

        byte[] data = await _s3Service.DownloadFile("media/" + mediaId.ToString());
        _mediaDataCache[mediaId] = data;

        async Task DownloadOptimizedMedia(SocialPlatform platform)
        {
            byte[] optimizedData =
                await _s3Service.DownloadFile($"media/{mediaId.ToString()}_{platform.ToString().ToLower()}");
            _mediaOptimizedDataCache[platform][mediaId] = optimizedData;
        }
        
        if (media.HasTwitterOptimizedVersion)
        {
            await DownloadOptimizedMedia(SocialPlatform.Twitter);
        }

        if (media.HasBlueskyOptimizedVersion)
        {
            await DownloadOptimizedMedia(SocialPlatform.Bluesky);
        }

        if (media.HasMastodonOptimizedVersion)
        {
            await DownloadOptimizedMedia(SocialPlatform.Mastodon);
        }

        if (media.HasThreadsOptimizedVersion)
        {
            await DownloadOptimizedMedia(SocialPlatform.Threads);
        }
    }

    private string ComputeRemoteFileNameForQuotedPost(string url)
    {
        using SHA256 sha = SHA256.Create();
        
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
        
        return "quoted_posts/" + Convert.ToHexString(hash).ToLower();
    }

    public async Task<QuotedPost> AddQuotedPostToCache(string url)
    {
        QuotedPost quotedPost = await QuotedPost.BuildInitialFromUrl(url, _postThreadCollection);
        
        _quotedPosts[url] = quotedPost;
        
        SocialPlatform primaryPlatform = quotedPost.GetPrimaryPlatform();
        
        ChromeOptions options = new ChromeOptions();
        options.AddArgument("--ignore-certificate-errors");
        options.AddArgument("--hide-scrollbars");
        options.AddArgument("--disable-dev-shm-usage"); // avoid "session deleted because of page crash"

        using RemoteWebDriver remoteWebDriver = new RemoteWebDriver(new Uri(_seleniumNodeUrl), options);

        StringBuilder urlBuilder = new StringBuilder();
        urlBuilder.Append(_webAppUrl);

        if (urlBuilder[^1] != '/')
        {
            urlBuilder.Append('/');
        }

        urlBuilder.Append("quote/");
        urlBuilder.Append(primaryPlatform.ToString().ToLower());
        urlBuilder.Append('?');

        Dictionary<string, string> queryParameters = new Dictionary<string, string>();

        if (primaryPlatform == SocialPlatform.Twitter)
        {
            queryParameters["url"] = quotedPost.GetPostUrlOnPrimaryPlatform();
        }
        else if (primaryPlatform == SocialPlatform.Bluesky)
        {
            queryParameters["uri"] = quotedPost.BlueskyRef!.Uri;
            queryParameters["cid"] = quotedPost.BlueskyRef.Cid;
        }

        FormUrlEncodedContent queryContent = new FormUrlEncodedContent(queryParameters);
        urlBuilder.Append(await queryContent.ReadAsStringAsync());
        
        remoteWebDriver.Navigate().GoToUrl(urlBuilder.ToString());
        
        IWebElement? iframeElement = null;
    
        WebDriverWait wait = new WebDriverWait(remoteWebDriver, TimeSpan.FromSeconds(30));

        if (primaryPlatform == SocialPlatform.Twitter)
        {
            wait.Until(driver =>
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
        }
        else if (primaryPlatform == SocialPlatform.Bluesky)
        {
            wait.Until(driver =>
            {
                try
                {
                    iframeElement = driver.FindElement(By.TagName("iframe"));

                    string styleAttribute = iframeElement.GetAttribute("style");

                    if (!styleAttribute.Contains("height"))
                    {
                        return false;
                    }
                }
                catch (Exception)
                {
                    return false;
                }

                return true;
            });

            // Not sure what else we can do here...
            // There doesn't seem to be an easy way to check if the iframe is done loading.
        }
        
        double ParsePixelValue(string val)
        {
            // strip "px" and parse
            return double.Parse(val.Substring(0, val.Length - 2), CultureInfo.InvariantCulture);
        }

        double iframeWidth = ParsePixelValue(iframeElement!.GetCssValue("width"));
        double iframeHeight = ParsePixelValue(iframeElement!.GetCssValue("height"));

        int ConvertDimensionToInt(object o)
        {
            int i;
            
            if (o is double d)
            {
                double roundedUp = Math.Ceiling(d);
                i = (int)roundedUp;
            }
            else if (o is long l)
            {
                i = Convert.ToInt32(l);
            }
            else
            {
                throw new UnreachableException($"Unexpected dimension type {o.GetType()}");
            }
            
            return i;
        }
        
        ICollection<object> collection = (ICollection<object>)remoteWebDriver.ExecuteScript("return [window.outerWidth - window.innerWidth + arguments[0], window.outerHeight - window.innerHeight + arguments[1]]", new object[] { iframeWidth + 30, iframeHeight + 30 });
        IList<object> size = collection.ToList();
        remoteWebDriver.Manage().Window.Size =
            new System.Drawing.Size(ConvertDimensionToInt(size[0]), ConvertDimensionToInt(size[1]));
        
        // Wait an extra 5 seconds to let anything that isn't done loading finish.
        Thread.Sleep(5000);

        Screenshot screenshot = remoteWebDriver.GetScreenshot();

        quotedPost.ImageData = screenshot.AsByteArray;

        string remoteImageFileName = ComputeRemoteFileNameForQuotedPost(url);

        await _s3Service.TransferFile(remoteImageFileName, quotedPost.ImageData, "image/png");

        quotedPost.ImageUrl = await _s3Service.GetPreSignedUrlForFile(remoteImageFileName, HttpVerb.GET, 15);

        return quotedPost;
    }

    public QuotedPost GetQuotedPost(string url)
    {
        return _quotedPosts[url];
    }
}
