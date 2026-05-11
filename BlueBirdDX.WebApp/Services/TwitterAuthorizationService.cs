using System.Security.Cryptography;
using System.Text;
using BlueBirdDX.Common.Account;
using BlueBirdDX.Platform.Twitter;
using BlueBirdDX.WebApp.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.WebApp.Services;

public class TwitterAuthorizationService
{
    private readonly TwitterAuthorizationSettings _settings;
    private readonly IMongoCollection<AccountGroup> _accountCollection;
    private readonly Dictionary<string, TwitterAuthorizationState> _states =
        new Dictionary<string, TwitterAuthorizationState>();
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

    public TwitterAuthorizationService(IOptions<TwitterAuthorizationSettings> settings, SlabMongoService mongoService)
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
            string verifier = GenerateRandomString(128);

            using HashAlgorithm shaAlgorithm = SHA256.Create();
            byte[] verifierHash = shaAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(verifier));
            string challenge = Convert.ToBase64String(verifierHash).Replace('+', '-').Replace('/', '_').TrimEnd('=');

            TwitterAuthorizationState state = new TwitterAuthorizationState()
            {
                Id = stateId,
                Verifier = verifier,
                Challenge = challenge,
                GroupId = groupId
            };
            
            _states[stateId] = state;
            
            Dictionary<string, string> urlParameters = new Dictionary<string, string>()
            {
                { "response_type", "code" },
                { "client_id", _settings.ClientId },
                { "redirect_uri", _settings.RedirectUrl },
                { "state", state.Id },
                { "code_challenge", state.Challenge },
                { "code_challenge_method", "S256" },
                { "scope", "tweet.read tweet.write users.read offline.access"}
            };
        
            FormUrlEncodedContent formUrlEncodedContent = new FormUrlEncodedContent(urlParameters);
            string urlParametersEncoded = await formUrlEncodedContent.ReadAsStringAsync();
            
            return $"https://x.com/i/oauth2/authorize?{urlParametersEncoded}";
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
            TwitterAuthorizationState authorizationState = _states[state];

            ObjectId groupId = ObjectId.Parse(authorizationState.GroupId);
            AccountGroup group = _accountCollection.AsQueryable().FirstOrDefault(g => g._id == groupId)!;

            TwitterClient client = new TwitterClient(_settings.ClientId, _settings.ClientSecret);
            await client.LoginWithCodeAndVerifier(code, authorizationState.Verifier, _settings.RedirectUrl);

            group.Twitter = new TwitterAccount()
            {
                RefreshToken = client.RefreshToken!
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
