using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Database.Migration.MediaUploadJob;

public class MediaUploadJobMigratorOneToTwo : SlabMongoDocumentMigrator<Common.Media.MediaUploadJob>
{
    public override int OldSchemaVersion => 1;
    public override int NewSchemaVersion => 2;

    public override Task MigrateDocument(BsonDocument document)
    {
        document.Remove("IsJobForMigrationTwoToThree");

        return Task.CompletedTask;
    }
}
