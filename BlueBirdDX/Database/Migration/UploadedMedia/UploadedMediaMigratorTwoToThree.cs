using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Database.Migration.UploadedMedia;

public class UploadedMediaMigratorTwoToThree : SlabMongoDocumentMigrator<Common.Media.UploadedMedia>
{
    public override int OldSchemaVersion => 2;
    public override int NewSchemaVersion => 3;

    public override Task MigrateDocument(BsonDocument document)
    {
        // Technically, media uploaded with schema version 2 or older may exceed the limits enforced by each
        // social platform. Starting in 5.0.0, support was added to "re-encode" this media with a file size that is
        // within each platform's limits. However, after the project was migrated to Slab, this functionality was
        // removed as it is difficult to reimplement it with the constraints of the Slab framework. As such, instances
        // with media uploaded before 5.0.0 that are upgrading directly to 6.0.0 (i.e. skipping 5.0.0) will have
        // media that may not be accepted by some platforms.

        document.Set("HasTwitterOptimizedVersion", false);
        document.Set("HasBlueskyOptimizedVersion", false);
        document.Set("HasMastodonOptimizedVersion", false);
        document.Set("HasThreadsOptimizedVersion", false);
        
        return Task.CompletedTask;
    }
}
