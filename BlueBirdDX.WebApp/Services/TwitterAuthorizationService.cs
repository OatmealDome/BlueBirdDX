using System.Security.Cryptography;
using System.Text;
using BlueBirdDX.Grpc;
using BlueBirdDX.WebApp.Models;
using Microsoft.Extensions.Options;

namespace BlueBirdDX.WebApp.Services;

public class TwitterAuthorizationService
{
    private readonly SocialAppAuthorizationSettings _settings;
    private readonly SocialAppAuthorization.SocialAppAuthorizationClient _authorizationClient;
    private readonly Dictionary<string, TwitterAuthorizationState> _states =
        new Dictionary<string, TwitterAuthorizationState>();
    private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

    public TwitterAuthorizationService(IOptions<SocialAppAuthorizationSettings> settings,
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
            string stateId = AuthorizationUtil.GenerateRandomString(256);
            string verifier = AuthorizationUtil.GenerateRandomString(128);

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

            CreateAuthorizationUrlReply reply = await _authorizationClient.CreateTwitterAuthorizationUrlAsync(
                new CreateTwitterAuthorizationUrlRequest
                {
                    RedirectUrl = CreateCallbackUrl(),
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

    public async Task AuthorizeByCallback(string state, string code)
    {
        await _semaphoreSlim.WaitAsync();

        try
        {
            TwitterAuthorizationState authorizationState = _states[state];

            await _authorizationClient.AuthorizeTwitterCallbackAsync(new AuthorizeTwitterCallbackRequest
            {
                GroupId = authorizationState.GroupId,
                Code = code,
                Verifier = authorizationState.Verifier,
                RedirectUrl = CreateCallbackUrl()
            });

            _states.Remove(state);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    private string CreateCallbackUrl()
    {
        return $"{_settings.BaseUrl.TrimEnd('/')}/account/twitter/callback";
    }
}
