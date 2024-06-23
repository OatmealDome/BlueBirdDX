using MongoDB.Bson;

namespace BlueBirdDX.Database.Migration.AccountGroup;

public class AccountGroupMigratorOneToTwo : IDocumentMigrator
{
    public bool DoesDocumentRequireMigration(BsonDocument document)
    {
        return document["SchemaVersion"] == 1;
    }

    public Task MigrateDocument(BsonDocument document)
    {
        document.Set("Threads", BsonNull.Value);

        document.Set("SchemaVersion", 2);

        return Task.CompletedTask;
    }
}