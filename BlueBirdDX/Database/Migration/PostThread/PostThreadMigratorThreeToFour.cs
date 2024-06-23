using MongoDB.Bson;

namespace BlueBirdDX.Database.Migration.PostThread;

public class PostThreadMigratorThreeToFour : IDocumentMigrator
{
    public bool DoesDocumentRequireMigration(BsonDocument document)
    {
        return document["SchemaVersion"] == 3;
    }

    public Task MigrateDocument(BsonDocument document)
    {
        document.Set("ParentThread", BsonNull.Value);
        
        BsonArray items = document["Items"].AsBsonArray;
        
        foreach (BsonValue element in items)
        {
            BsonDocument itemDocument = element.AsBsonDocument;
            
            itemDocument["TwitterId"] = BsonNull.Value;
            itemDocument["BlueskyRootRef"] = BsonNull.Value;
            itemDocument["BlueskyThisRef"] = BsonNull.Value;
            itemDocument["MastodonId"] = BsonNull.Value;
            itemDocument["ThreadsId"] = BsonNull.Value;
        }
        
        document.Set("SchemaVersion", 4);
        
        return Task.CompletedTask;
    }
}