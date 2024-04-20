using System.Drawing;
using System.Net;
using System.Text;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Storage;
using BlueBirdDX.Config;
using BlueBirdDX.Config.Storage;
using BlueBirdDX.Config.WebDriver;
using BlueBirdDX.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;

namespace BlueBirdDX.Social;

public class AttachmentCache
{
    private readonly RemoteStorage _remoteStorage;
    private readonly IMongoCollection<UploadedMedia> _uploadedMediaCollection;

    private readonly Dictionary<ObjectId, UploadedMedia>
        _mediaDocumentCache = new Dictionary<ObjectId, UploadedMedia>();
    private readonly Dictionary<ObjectId, byte[]> _mediaDataCache = new Dictionary<ObjectId, byte[]>();

    private readonly Dictionary<string, byte[]> _quotedPostCache = new Dictionary<string, byte[]>();
    
    public AttachmentCache()
    {
        RemoteStorageConfig storageConfig = BbConfig.Instance.RemoteStorage;
        
        _remoteStorage = new RemoteStorage(storageConfig.ServiceUrl, storageConfig.Bucket, storageConfig.AccessKey,
            storageConfig.AccessKeySecret);
        
        _uploadedMediaCollection = DatabaseManager.Instance.GetCollection<UploadedMedia>("media");
    }

    public UploadedMedia GetMediaDocument(ObjectId mediaId)
    {
        return _mediaDocumentCache[mediaId];
    }
    
    public byte[] GetMediaData(ObjectId mediaId)
    {
        return _mediaDataCache[mediaId];
    }
    
    public async Task AddMediaToCache(ObjectId mediaId)
    {
        if (_mediaDocumentCache.ContainsKey(mediaId))
        {
            return;
        }
        
        UploadedMedia media = _uploadedMediaCollection.AsQueryable().FirstOrDefault(m => m._id == mediaId)!;
        _mediaDocumentCache[mediaId] = media;

        _mediaDataCache[mediaId] = await _remoteStorage.DownloadFile(mediaId.ToString());
    }

    public async Task AddQuotedPostToCache(string url)
    {
        ChromeOptions options = new ChromeOptions();
        options.AddArgument("--ignore-certificate-errors");
        options.AddArgument("--hide-scrollbars");
        options.AddArgument("--disable-dev-shm-usage"); // avoid "session deleted because of page crash"

        WebDriverConfig config = BbConfig.Instance.WebDriver;

        RemoteWebDriver remoteWebDriver = new RemoteWebDriver(new Uri(config.NodeUrl), options);
        remoteWebDriver.Navigate()
            .GoToUrl(string.Format(config.ScreenshotUrlFormat, "tweet", WebUtility.UrlEncode(url)));
        
        IWebElement? iframeElement = null;
    
        WebDriverWait wait = new WebDriverWait(remoteWebDriver, TimeSpan.FromSeconds(10));
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
        remoteWebDriver.Manage().Window.Size = new Size((int)(Int64)size[0], (int)(Int64)size[1]);
        
        // Wait an extra 5 seconds to let anything that isn't done loading finish.
        Thread.Sleep(5000);

        Screenshot screenshot = remoteWebDriver.GetScreenshot();
            
        remoteWebDriver.Close();
        remoteWebDriver.Quit();

        _quotedPostCache[url] = screenshot.AsByteArray;
    }

    public byte[] GetQuotedPostData(string url)
    {
        return _quotedPostCache[url];
    }
}