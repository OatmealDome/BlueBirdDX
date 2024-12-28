using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.WebApp.Models;
using MongoDB.Driver;

namespace BlueBirdDX.WebApp.Services;

public class DatabaseService
{
    private readonly MongoClient _client;

    public readonly IMongoCollection<AccountGroup> AccountGroupCollection;
    public readonly IMongoCollection<UploadedMedia> UploadedMediaCollection;
    public readonly IMongoCollection<MediaUploadJob> MediaUploadJobCollection;
    public readonly IMongoCollection<PostThread> PostThreadCollection;

    public DatabaseService(DatabaseSettings settings)
    {
        _client = new MongoClient(settings.ConnectionString);

        IMongoDatabase database = _client.GetDatabase(settings.DatabaseName);

        AccountGroupCollection = database.GetCollection<AccountGroup>("accounts");
        UploadedMediaCollection = database.GetCollection<UploadedMedia>("media");
        MediaUploadJobCollection = database.GetCollection<MediaUploadJob>("media_jobs");
        PostThreadCollection = database.GetCollection<PostThread>("threads");
    }
}