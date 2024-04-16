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
    
    private readonly Dictionary<ObjectId, byte[]> _mediaCache = new Dictionary<ObjectId, byte[]>();
    
    public AttachmentCache()
    {
        RemoteStorageConfig storageConfig = BbConfig.Instance.RemoteStorage;
        
        _remoteStorage = new RemoteStorage(storageConfig.ServiceUrl, storageConfig.Bucket, storageConfig.AccessKey,
            storageConfig.AccessKeySecret);
        
        _uploadedMediaCollection = DatabaseManager.Instance.GetCollection<UploadedMedia>("media");
    }
    
    public byte[] GetMedia(ObjectId mediaId)
    {
        return _mediaCache[mediaId];
    }
    
    public async Task AddMediaToCache(ObjectId mediaId)
    {
        if (_mediaCache.ContainsKey(mediaId))
        {
            return;
        }

        _mediaCache[mediaId] = await _remoteStorage.DownloadFile(mediaId.ToString());
    }
}