using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Database.Migration.PostThread;

public class PostThreadMigratorOneToTwo : SlabMongoDocumentMigrator<Common.Post.PostThread>
{
    public override int OldSchemaVersion => 1;
    public override int NewSchemaVersion => 2;

    public override Task MigrateDocument(BsonDocument document)
    {
        document.Set("PostToTwitter", true);
        document.Set("PostToBluesky", true);
        document.Set("PostToMastodon", true);

        return Task.CompletedTask;
    }
}
