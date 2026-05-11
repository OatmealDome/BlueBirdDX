using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Database.Migration.PostThread;

public class PostThreadMigratorTwoToThree : SlabMongoDocumentMigrator<Common.Post.PostThread>
{
    public override int OldSchemaVersion => 2;
    public override int NewSchemaVersion => 3;

    public bool DoesDocumentRequireMigration(BsonDocument document)
    {
        return document["SchemaVersion"] == 2;
    }

    public override Task MigrateDocument(BsonDocument document)
    {
        document.Set("PostToThreads", false);

        return Task.CompletedTask;
    }
}
