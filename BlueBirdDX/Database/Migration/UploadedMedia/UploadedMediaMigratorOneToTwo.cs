using BlueBirdDX.Common.Storage;
using BlueBirdDX.Config;
using BlueBirdDX.Config.Storage;
using MongoDB.Bson;
using SixLabors.ImageSharp;

namespace BlueBirdDX.Database.Migration.UploadedMedia;

public class UploadedMediaMigratorOneToTwo : IDocumentMigrator
{
    private readonly RemoteStorage _remoteStorage;

    public UploadedMediaMigratorOneToTwo()
    {
        RemoteStorageConfig storageConfig = BbConfig.Instance.RemoteStorage;

        _remoteStorage = new RemoteStorage(storageConfig.ServiceUrl, storageConfig.Bucket, storageConfig.AccessKey,
            storageConfig.AccessKeySecret);
    }
    public bool DoesDocumentRequireMigration(BsonDocument document)
    {
        return document["SchemaVersion"] == 1;
    }

    public async Task MigrateDocument(BsonDocument document)
    {
        ObjectId mediaId = document.GetValue("_id").AsObjectId;

        byte[] data = await _remoteStorage.DownloadFile("media/" + mediaId.ToString());
        using MemoryStream memoryStream = new MemoryStream(data);
        
        using Image image = await Image.LoadAsync(memoryStream);

        document.Set("Width", image.Width);
        document.Set("Height", image.Height);
        
        document.Set("SchemaVersion", 2);
    }
}