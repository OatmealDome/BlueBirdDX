using System.Security.Cryptography;
using System.Text;
using BlueBirdDX.Grpc;
using BlueBirdDX.WebApp.Models;
using Microsoft.Extensions.Options;

namespace BlueBirdDX.WebApp.Services;

public class SocialAppAuthorizationService
{
    private readonly SocialAppAuthorizationSettings _settings;
    private readonly SocialAppAuthorization.SocialAppAuthorizationClient _authorizationClient;
    private readonly Dictionary<string, TwitterAuthorizationState> _twitterStates =
        new Dictionary<string, TwitterAuthorizationState>();
    private readonly Dictionary<string, ThreadsAuthorizationState> _threadsStates =
        new Dictionary<string, ThreadsAuthorizationState>();
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

    public SocialAppAuthorizationService(IOptions<SocialAppAuthorizationSettings> settings,
        SocialAppAuthorization.SocialAppAuthorizationClient authorizationClient)
    {
        _settings = settings.Value;
        _authorizationClient = authorizationClient;
    }

    public async Task<string> CreateTwitterAuthorizationAttemptUrl(string groupId)
    {
        await _semaphoreSlim.WaitAsync();

        try
        {
            string stateId = AuthorizationUtil.GenerateRandomString(256);
            string verifier = AuthorizationUtil.GenerateRandomString(128);

            using HashAlgorithm shaAlgorithm = SHA256.Create();
            byte[] verifierHash = shaAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(verifier));
            string challenge = Convert.ToBase64String(verifierHash).Replace('+', '-').Replace('/', '_').TrimEnd('=');

            TwitterAuthorizationState state = new TwitterAuthorizationState
            {
                Id = stateId,
                Verifier = verifier,
                Challenge = challenge,
                GroupId = groupId
            };

            _twitterStates[stateId] = state;

            CreateAuthorizationUrlReply reply = await _authorizationClient.CreateTwitterAuthorizationUrlAsync(
                new CreateTwitterAuthorizationUrlRequest
                {
                    RedirectUrl = CreateCallbackUrl("twitter"),
                    State = state.Id,
                    Challenge = state.Challenge
                });

            return reply.Url;
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public async Task AuthorizeTwitterByCallback(string state, string code)
    {
        await _semaphoreSlim.WaitAsync();

        try
        {
            TwitterAuthorizationState authorizationState = _twitterStates[state];

            await _authorizationClient.AuthorizeTwitterCallbackAsync(new AuthorizeTwitterCallbackRequest
            {
                GroupId = authorizationState.GroupId,
                Code = code,
                Verifier = authorizationState.Verifier,
                RedirectUrl = CreateCallbackUrl("twitter")
            });

            _twitterStates.Remove(state);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public async Task<string> CreateThreadsAuthorizationAttemptUrl(string groupId)
    {
        await _semaphoreSlim.WaitAsync();

        try
        {
            string stateId = GenerateRandomString(256);

            ThreadsAuthorizationState state = new ThreadsAuthorizationState
            {
                Id = stateId,
                GroupId = groupId
            };

            _threadsStates[stateId] = state;

            CreateAuthorizationUrlReply reply = await _authorizationClient.CreateThreadsAuthorizationUrlAsync(
                new CreateThreadsAuthorizationUrlRequest
                {
                    RedirectUrl = CreateCallbackUrl("threads"),
                    State = stateId
                });

            return reply.Url;
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    public async Task AuthorizeThreadsByCallback(string state, string code)
    {
        await _semaphoreSlim.WaitAsync();

        try
        {
            ThreadsAuthorizationState authorizationState = _threadsStates[state];

            await _authorizationClient.AuthorizeThreadsCallbackAsync(new AuthorizeThreadsCallbackRequest
            {
                GroupId = authorizationState.GroupId,
                Code = code,
                RedirectUrl = CreateCallbackUrl("threads")
            });

            _threadsStates.Remove(state);
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

    private string CreateCallbackUrl(string platform)
    {
        return $"{_settings.BaseUrl.TrimEnd('/')}/account/{platform}/callback";
    }
}
