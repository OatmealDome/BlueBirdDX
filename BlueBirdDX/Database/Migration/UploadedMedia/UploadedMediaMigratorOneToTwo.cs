using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Database.Migration.UploadedMedia;

public class UploadedMediaMigratorOneToTwo : SlabMongoDocumentMigrator<Common.Media.UploadedMedia>
{
    public override int OldSchemaVersion => 1;
    public override int NewSchemaVersion => 2;

    public override Task MigrateDocument(BsonDocument document)
    {
        // These will be set in the migrator for 2 -> 3.
        document.Set("Width", 0);
        document.Set("Height", 0);

        return Task.CompletedTask;
    }
}
