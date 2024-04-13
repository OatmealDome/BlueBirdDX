using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Post;
using BlueBirdDX.WebApp.Models;
using MongoDB.Driver;

namespace BlueBirdDX.WebApp.Services;

public class DatabaseService
{
    private readonly MongoClient _client;

    public readonly IMongoCollection<AccountGroup> AccountGroupCollection;
    public readonly IMongoCollection<PostThread> PostThreadCollection;

    public DatabaseService(DatabaseSettings settings)
    {
        _client = new MongoClient(settings.ConnectionString);

        IMongoDatabase database = _client.GetDatabase(settings.DatabaseName);

        AccountGroupCollection = database.GetCollection<AccountGroup>("accounts");
        PostThreadCollection = database.GetCollection<PostThread>("threads");
    }
}