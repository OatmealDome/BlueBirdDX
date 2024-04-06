using MongoDB.Bson;

namespace BlueBirdDX.Database.Migration;

public interface IDocumentMigrator
{
    bool DoesDocumentRequireMigration(BsonDocument document);
        
    Task MigrateDocument(BsonDocument document);
}