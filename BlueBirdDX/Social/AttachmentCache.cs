using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Storage;
using BlueBirdDX.Config;
using BlueBirdDX.Config.Storage;
using BlueBirdDX.Database;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BlueBirdDX.Social;

public class AttachmentCache
{
    private readonly RemoteStorage _remoteStorage;
    private readonly IMongoCollection<UploadedMedia> _uploadedMediaCollection;

    private readonly Dictionary<ObjectId, UploadedMedia>
        _mediaDocumentCache = new Dictionary<ObjectId, UploadedMedia>();
    private readonly Dictionary<ObjectId, byte[]> _mediaDataCache = new Dictionary<ObjectId, byte[]>();
    
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
}