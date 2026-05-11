using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Database.Migration.AccountGroup;

public class AccountGroupMigratorThreeToFour : SlabMongoDocumentMigrator<Common.Account.AccountGroup>
{
    public override int OldSchemaVersion => 3;
    public override int NewSchemaVersion => 4;

    public override Task MigrateDocument(BsonDocument document)
    {
        // Save old credentials for archival.
        document.Set("ThreadsLegacy", document["Threads"]);

        document.Set("Threads", BsonNull.Value);

        return Task.CompletedTask;
    }
}
