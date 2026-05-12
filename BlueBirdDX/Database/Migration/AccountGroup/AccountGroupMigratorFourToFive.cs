using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Database.Migration.AccountGroup;

public class AccountGroupMigratorFourToFive : SlabMongoDocumentMigrator<Common.Account.AccountGroup>
{
    public override int OldSchemaVersion => 4;
    public override int NewSchemaVersion => 5;

    public override Task MigrateDocument(BsonDocument document)
    {
        if (document.TryGetValue("Twitter", out BsonValue? twitterValue) &&
            twitterValue is BsonDocument twitterDocument)
        {
            twitterDocument.Set("Premium", false);
        }

        return Task.CompletedTask;
    }
}
