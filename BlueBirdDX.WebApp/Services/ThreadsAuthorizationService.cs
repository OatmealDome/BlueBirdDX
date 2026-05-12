using System.Security.Cryptography;
using BlueBirdDX.Grpc;
using BlueBirdDX.WebApp.Models;
using Microsoft.Extensions.Options;

namespace BlueBirdDX.WebApp.Services;

public class ThreadsAuthorizationService
{
    private readonly ThreadsAuthorizationSettings _settings;
    private readonly SocialAppAuthorization.SocialAppAuthorizationClient _authorizationClient;
    private readonly Dictionary<string, ThreadsAuthorizationState> _states =
        new Dictionary<string, ThreadsAuthorizationState>();
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

    public ThreadsAuthorizationService(IOptions<ThreadsAuthorizationSettings> settings,
        SocialAppAuthorization.SocialAppAuthorizationClient authorizationClient)
    {
        _settings = settings.Value;
        _authorizationClient = authorizationClient;
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

            CreateAuthorizationUrlReply reply = await _authorizationClient.CreateThreadsAuthorizationUrlAsync(
                new CreateThreadsAuthorizationUrlRequest
                {
                    RedirectUrl = _settings.RedirectUrl!,
                    State = stateId
                });

            return reply.Url;
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
            
            await _authorizationClient.AuthorizeThreadsCallbackAsync(new AuthorizeThreadsCallbackRequest
            {
                GroupId = authorizationState.GroupId,
                Code = code,
                RedirectUrl = _settings.RedirectUrl!
            });

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
