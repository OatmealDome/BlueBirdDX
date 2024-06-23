using BlueBirdDX.Common.Account;
using BlueBirdDX.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;
using Serilog.Core;

namespace BlueBirdDX.Social;

public class AccountGroupManager
{
    private static AccountGroupManager? _instance;
    public static AccountGroupManager Instance => _instance!;

    private static readonly ILogger LogContext =
        Log.ForContext(Constants.SourceContextPropertyName, "AccountGroupManager");
    
    private readonly IMongoCollection<AccountGroup> _accountGroupCollection;

    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

    private AccountGroupManager()
    {
        _accountGroupCollection = DatabaseManager.Instance.GetCollection<AccountGroup>("accounts");
    }
    
    public static void Initialize()
    {
        _instance = new AccountGroupManager();
    }

    public AccountGroup GetAccountGroup(ObjectId id)
    {
        _semaphoreSlim.Wait();
        
        try
        {
            return _accountGroupCollection.AsQueryable().FirstOrDefault(a => a._id == id)!;
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }
}