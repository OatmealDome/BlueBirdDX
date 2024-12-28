using BlueBirdDX.Common.Media;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BlueBirdDX.Database.Migration.UploadedMedia;

public class UploadedMediaMigratorTwoToThree : IDocumentMigrator
{
    public bool DoesDocumentRequireMigration(BsonDocument document)
    {
        return document["SchemaVersion"] == 2;
    }

    public async Task MigrateDocument(BsonDocument document)
    {
        IMongoCollection<MediaUploadJob> jobCollection =
            DatabaseManager.Instance.GetCollection<MediaUploadJob>("media_jobs");
        
        await jobCollection.InsertOneAsync(new MediaUploadJob()
        {
            SchemaVersion = MediaUploadJob.LatestSchemaVersion,
            Name = document.GetValue("Name").AsString + " (migration 2 to 3)",
            MimeType = document.GetValue("MimeType").AsString,
            CreationTime = DateTime.UtcNow,
            State = MediaUploadJobState.Ready,
            MediaId = document.GetValue("_id").AsObjectId,
            IsJobForMigrationTwoToThree = true
        });
        
        document.Set("HasTwitterOptimizedVersion", false);
        document.Set("HasBlueskyOptimizedVersion", false);
        document.Set("HasMastodonOptimizedVersion", false);
        document.Set("HasThreadsOptimizedVersion", false);
        
        document.Set("SchemaVersion", 3);
    }
}