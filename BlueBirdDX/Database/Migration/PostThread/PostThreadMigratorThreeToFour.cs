using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Database.Migration.PostThread;

public class PostThreadMigratorThreeToFour : SlabMongoDocumentMigrator<Common.Post.PostThread>
{
    public override int OldSchemaVersion => 3;
    public override int NewSchemaVersion => 4;

    public override Task MigrateDocument(BsonDocument document)
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
        
        return Task.CompletedTask;
    }
}
