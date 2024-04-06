using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;
using Serilog.Core;

namespace BlueBirdDX.Database.Migration;

public abstract class MigrationManager
{
    private readonly ILogger _logContext;

    protected abstract string CollectionName
    {
        get;
    }
        
    protected abstract IEnumerable<IDocumentMigrator> Migrators
    {
        get;
    }

    private IMongoCollection<BsonDocument> Collection;


    protected MigrationManager()
    {
        _logContext = Log.ForContext(Constants.SourceContextPropertyName, this.GetType().Name);
    }

    public void SetUp(IMongoDatabase database)
    {
        Collection = database.GetCollection<BsonDocument>(CollectionName);
    }

    public async Task PerformMigration()
    {
        foreach (BsonDocument document in Collection.AsQueryable())
        {
            foreach (IDocumentMigrator migrator in Migrators)
            {
                if (migrator.DoesDocumentRequireMigration(document))
                {
                    BsonElement documentId = document.GetElement("_id");
                        
                    try
                    {
                        await migrator.MigrateDocument(document);

                        ReplaceOneResult result = await Collection.ReplaceOneAsync(
                            Builders<BsonDocument>.Filter.Eq("_id", documentId.Value.AsObjectId), document);
                    }
                    catch (Exception e)
                    {
                        _logContext.Error(e, "Migration failed on document {DocumentId}", documentId);

                        continue;
                    }
                        
                    _logContext.Information("Migrated document {DocumentId} with {Migrator}",
                        documentId, migrator.GetType().Name);
                }
            }
        }
    }
}