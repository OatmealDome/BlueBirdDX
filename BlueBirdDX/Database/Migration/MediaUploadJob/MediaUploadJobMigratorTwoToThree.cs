using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Database.Migration.MediaUploadJob;

public class MediaUploadJobMigratorTwoToThree : SlabMongoDocumentMigrator<Common.Media.MediaUploadJob>
{
    public override int OldSchemaVersion => 2;
    public override int NewSchemaVersion => 3;

    public override Task MigrateDocument(BsonDocument document)
    {
        // Dummy migrator. This is a version that doesn't exist due to a bug with the document's schema version.
        return Task.CompletedTask;
    }
}
