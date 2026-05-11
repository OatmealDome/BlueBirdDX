using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Database.Migration.AccountGroup;

public class AccountGroupMigratorOneToTwo : SlabMongoDocumentMigrator<Common.Account.AccountGroup>
{
    public override int OldSchemaVersion => 1;
    public override int NewSchemaVersion => 2;

    public override Task MigrateDocument(BsonDocument document)
    {
        document.Set("Threads", BsonNull.Value);

        return Task.CompletedTask;
    }
}
