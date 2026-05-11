using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Database.Migration.AccountGroup;

public class AccountGroupMigratorTwoToThree : SlabMongoDocumentMigrator<Common.Account.AccountGroup>
{
    public override int OldSchemaVersion => 2;
    public override int NewSchemaVersion => 3;

    public override Task MigrateDocument(BsonDocument document)
    {
        // Save OAuth 1.0a credentials for archival.
        document.Set("TwitterOAuth1", document["Twitter"]);

        document.Set("Twitter", BsonNull.Value);

        return Task.CompletedTask;
    }
}
