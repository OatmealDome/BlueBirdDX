using BlueBirdDX.Common.Account;
using BlueBirdDX.Database;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;
using OatmealDome.Unravel;
using OatmealDome.Unravel.Authentication;

namespace BlueBirdDX.Social;

public class AccountGroupManager
{
    private readonly ILogger<AccountGroupManager> _logger;
    private readonly IMongoCollection<AccountGroup> _accountGroupCollection;
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

    public AccountGroupManager(ILogger<AccountGroupManager> logger, SlabMongoService mongoService)
    {
        _logger = logger;
        _accountGroupCollection = mongoService.GetCollection<AccountGroup>("accounts");
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

    public async Task UpdateTwitterRefreshTokenForGroup(AccountGroup group, string refreshToken)
    {
        await _accountGroupCollection.UpdateOneAsync(Builders<AccountGroup>.Filter.Eq(g => g._id, group._id),
            Builders<AccountGroup>.Update.Set(g => g.Twitter!.RefreshToken, refreshToken));
        
        group.Twitter!.RefreshToken = refreshToken;
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
                
                _logger.LogInformation("Refreshing Threads token for account group {groupId}", group._id);

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

                _logger.LogInformation("Threads token for account group {groupId} refreshed", group._id);
            }
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }
}
