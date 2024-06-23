using MongoDB.Bson;

namespace BlueBirdDX.Database.Migration.PostThread;

public class PostThreadMigratorTwoToThree : IDocumentMigrator
{
    public bool DoesDocumentRequireMigration(BsonDocument document)
    {
        return document["SchemaVersion"] == 2;
    }

    public Task MigrateDocument(BsonDocument document)
    {
        document.Set("PostToThreads", false);

        document.Set("SchemaVersion", 3);

        return Task.CompletedTask;
    }
}