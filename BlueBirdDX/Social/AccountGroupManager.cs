using BlueBirdDX.Common.Account;
using BlueBirdDX.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Unravel;
using OatmealDome.Unravel.Authentication;
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

    public async Task RefreshThreadsTokens()
    {
        await _semaphoreSlim.WaitAsync();

        try
        {
            foreach (AccountGroup group in _accountGroupCollection.AsQueryable().ToList())
            {
                ThreadsAccount? account = group.Threads;
            
                if (account == null)
                {
                    continue;
                }

                TimeSpan span = account.Expiry - DateTime.UtcNow;

                // Skip tokens that still have more than 7 days left.
                if (span.TotalDays > 7.0d)
                {
                    continue;
                }
                
                LogContext.Information("Refreshing Threads token for account group {groupId}", group._id);

                ThreadsClient client = new ThreadsClient(account.ClientId, account.ClientSecret)
                {
                    Credentials = new ThreadsCredentials()
                    {
                        CredentialType = ThreadsCredentialType.LongLived,
                        AccessToken = account.AccessToken,
                        Expiry = account.Expiry,
                        UserId = account.UserId
                    }
                };
                
                ThreadsCredentials credentials = await client.Auth_RefreshLongLivedAccessToken();

                account.AccessToken = credentials.AccessToken;
                account.Expiry = credentials.Expiry;

                await _accountGroupCollection.ReplaceOneAsync(Builders<AccountGroup>.Filter.Eq(g => g._id, group._id),
                    group);

                LogContext.Information("Threads token for account group {groupId} refreshed", group._id);
            }
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }
}