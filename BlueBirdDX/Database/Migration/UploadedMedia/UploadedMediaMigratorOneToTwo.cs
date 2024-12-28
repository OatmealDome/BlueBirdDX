using MongoDB.Bson;

namespace BlueBirdDX.Database.Migration.UploadedMedia;

public class UploadedMediaMigratorOneToTwo : IDocumentMigrator
{
    public bool DoesDocumentRequireMigration(BsonDocument document)
    {
        return document["SchemaVersion"] == 1;
    }

    public async Task MigrateDocument(BsonDocument document)
    {
        // These will be set in the migrator for 2 -> 3.
        document.Set("Width", 0);
        document.Set("Height", 0);
        
        document.Set("SchemaVersion", 2);
    }
}