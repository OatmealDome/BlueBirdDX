using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Database.Migration.MediaUploadJob;

public class MediaUploadJobMigratorThreeToFour : SlabMongoDocumentMigrator<Common.Media.MediaUploadJob>
{
    public override int OldSchemaVersion => 3;
    public override int NewSchemaVersion => 4;

    public override Task MigrateDocument(BsonDocument document)
    {
        document.Remove("IsJobForMigrationTwoToThree");

        return Task.CompletedTask;
    }
}
