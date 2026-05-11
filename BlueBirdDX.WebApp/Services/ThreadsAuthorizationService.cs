using System.Security.Cryptography;
using BlueBirdDX.Common.Account;
using BlueBirdDX.WebApp.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;
using OatmealDome.Unravel;
using OatmealDome.Unravel.Authentication;

namespace BlueBirdDX.WebApp.Services;

public class ThreadsAuthorizationService
{
    private readonly ThreadsAuthorizationSettings _settings;
    private readonly IMongoCollection<AccountGroup> _accountCollection;
    private readonly Dictionary<string, ThreadsAuthorizationState> _states =
        new Dictionary<string, ThreadsAuthorizationState>();
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

    public ThreadsAuthorizationService(IOptions<ThreadsAuthorizationSettings> settings, SlabMongoService mongoService)
    {
        _settings = settings.Value;
        _accountCollection = mongoService.GetCollection<AccountGroup>("accounts");
    }

    public async Task<string> CreateAuthorizationAttemptUrl(string groupId)
    {
        await _semaphoreSlim.WaitAsync();

        try
        {
            string stateId = GenerateRandomString(256);

            ThreadsAuthorizationState state = new ThreadsAuthorizationState()
            {
                Id = stateId,
                GroupId = groupId
            };
            
            _states[stateId] = state;

            return ThreadsClient.Auth_GetUserOAuthAuthorizationUrl(_settings.AppId!.Value, _settings.RedirectUrl!,
                ThreadsPermission.Basic | ThreadsPermission.ContentPublish | ThreadsPermission.ManageReplies |
                ThreadsPermission.Delete, stateId);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public async Task AuthorizeByCallback(string state, string code)
    {
        await _semaphoreSlim.WaitAsync();

        try
        {
            ThreadsAuthorizationState authorizationState = _states[state];

            ObjectId groupId = ObjectId.Parse(authorizationState.GroupId);
            AccountGroup group = _accountCollection.AsQueryable().FirstOrDefault(g => g._id == groupId)!;
            
            ThreadsClient client = new ThreadsClient(_settings.AppId!.Value, _settings.AppSecret!);
            await client.Auth_GetShortLivedAccessToken(code, _settings.RedirectUrl!);
            await client.Auth_GetLongLivedAccessToken();

            group.Threads = new ThreadsAccount()
            {
                AccessToken = client.Credentials!.AccessToken,
                Expiry = client.Credentials.Expiry,
                UserId = client.Credentials.UserId,
            };

            await _accountCollection.ReplaceOneAsync(Builders<AccountGroup>.Filter.Eq("_id", groupId), group);

            _states.Remove(state);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    private string GenerateRandomString(int length)
    {
        const string characterPool = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return RandomNumberGenerator.GetString(characterPool, length);
    }
}
