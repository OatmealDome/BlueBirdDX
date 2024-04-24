using MongoDB.Bson;

namespace BlueBirdDX.Database.Migration.PostThread;

public class PostThreadMigratorOneToTwo : IDocumentMigrator
{
    public bool DoesDocumentRequireMigration(BsonDocument document)
    {
        return document["SchemaVersion"] == 1;
    }

    public Task MigrateDocument(BsonDocument document)
    {
        document.Set("PostToTwitter", true);
        document.Set("PostToBluesky", true);
        document.Set("PostToMastodon", true);

        document.Set("SchemaVersion", 2);

        return Task.CompletedTask;
    }
}