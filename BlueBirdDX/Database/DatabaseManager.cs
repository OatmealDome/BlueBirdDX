using BlueBirdDX.Config;
using BlueBirdDX.Config.Database;
using BlueBirdDX.Database.Migration;
using BlueBirdDX.Database.Migration.AccountGroup;
using BlueBirdDX.Database.Migration.PostThread;
using BlueBirdDX.Database.Migration.UploadedMedia;
using MongoDB.Driver;

namespace BlueBirdDX.Database;

public class DatabaseManager
{
    private static DatabaseManager? _instance;
    public static DatabaseManager Instance => _instance!;

    private readonly IEnumerable<MigrationManager> _migrationManagers = new List<MigrationManager>()
    {
        new AccountGroupMigrationManager(),
        new PostThreadMigrationManager(),
        new UploadedMediaMigrationManager()
    };

    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;

    public DatabaseManager()
    {
        DatabaseConfig config = BbConfig.Instance.Database;
            
        _client = new MongoClient(config.ConnectionString);
        _database = _client.GetDatabase(config.Database);

        foreach (MigrationManager manager in _migrationManagers)
        {
            manager.SetUp(_database);
        }
    }

    public static void Initialize()
    {
        _instance = new DatabaseManager();
    }

    public void Dispose()
    {
        //
    }

    public async Task PerformMigration()
    {
        foreach (MigrationManager manager in _migrationManagers)
        {
            await manager.PerformMigration();
        }
    }

    public IMongoCollection<T> GetCollection<T>(string name)
    {
        return _database.GetCollection<T>(name);
    }
}