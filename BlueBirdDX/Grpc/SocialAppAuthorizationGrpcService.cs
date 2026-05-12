using BlueBirdDX.Common.Account;
using BlueBirdDX.Platform.Twitter;
using BlueBirdDX.Social;
using Grpc.Core;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;
using OatmealDome.Unravel;
using OatmealDome.Unravel.Authentication;

namespace BlueBirdDX.Grpc;

public class SocialAppAuthorizationGrpcService : SocialAppAuthorization.SocialAppAuthorizationBase
{
    private const string TwitterOAuth2Scope = "tweet.read tweet.write users.read media.write offline.access";

    private readonly SocialAppConfiguration _settings;
    private readonly IMongoCollection<AccountGroup> _accountCollection;

    public SocialAppAuthorizationGrpcService(IOptions<SocialAppConfiguration> configuration, SlabMongoService mongoService)
    {
        _settings = configuration.Value;
        _accountCollection = mongoService.GetCollection<AccountGroup>("accounts");
    }

    public override Task<CreateAuthorizationUrlReply> CreateTwitterAuthorizationUrl(
        CreateTwitterAuthorizationUrlRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(_settings.TwitterClientId))
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Twitter client ID is not configured"));
        }

        string url = TwitterClient.GenerateOAuth2AuthorizeUrl(_settings.TwitterClientId, request.RedirectUrl,
            request.State, request.Challenge, TwitterOAuth2Scope);

        return Task.FromResult(new CreateAuthorizationUrlReply
        {
            Url = url
        });
    }

    public override async Task<AuthorizeCallbackReply> AuthorizeTwitterCallback(
        AuthorizeTwitterCallbackRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(_settings.TwitterClientId) ||
            string.IsNullOrWhiteSpace(_settings.TwitterClientSecret))
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                "Twitter client credentials are not configured"));
        }

        TwitterClient client = new TwitterClient(_settings.TwitterClientId, _settings.TwitterClientSecret);
        await client.LoginWithCodeAndVerifier(request.Code, request.Verifier, request.RedirectUrl);

        await UpdateAccountGroup(request.GroupId,
            Builders<AccountGroup>.Update.Set(g => g.Twitter!.RefreshToken, client.RefreshToken!));

        return new AuthorizeCallbackReply();
    }

    public override Task<CreateAuthorizationUrlReply> CreateThreadsAuthorizationUrl(
        CreateThreadsAuthorizationUrlRequest request, ServerCallContext context)
    {
        if (_settings.ThreadsAppId == null)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Threads app ID is not configured"));
        }

        string url = ThreadsClient.Auth_GetUserOAuthAuthorizationUrl(_settings.ThreadsAppId.Value,
            request.RedirectUrl, ThreadsPermission.Basic | ThreadsPermission.ContentPublish |
            ThreadsPermission.ManageReplies | ThreadsPermission.Delete, request.State);

        return Task.FromResult(new CreateAuthorizationUrlReply
        {
            Url = url
        });
    }

    public override async Task<AuthorizeCallbackReply> AuthorizeThreadsCallback(
        AuthorizeThreadsCallbackRequest request, ServerCallContext context)
    {
        if (_settings.ThreadsAppId == null || string.IsNullOrWhiteSpace(_settings.ThreadsAppSecret))
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                "Threads app credentials are not configured"));
        }

        ThreadsClient client = new ThreadsClient(_settings.ThreadsAppId.Value, _settings.ThreadsAppSecret);
        await client.Auth_GetShortLivedAccessToken(request.Code, request.RedirectUrl);
        await client.Auth_GetLongLivedAccessToken();

        ThreadsCredentials credentials = client.Credentials!;
        await UpdateAccountGroup(request.GroupId, Builders<AccountGroup>.Update
            .Set(g => g.Threads!.AccessToken, credentials.AccessToken)
            .Set(g => g.Threads!.Expiry, credentials.Expiry)
            .Set(g => g.Threads!.UserId, credentials.UserId));

        return new AuthorizeCallbackReply();
    }

    private async Task UpdateAccountGroup(string groupId, UpdateDefinition<AccountGroup> update)
    {
        if (!ObjectId.TryParse(groupId, out ObjectId groupObjectId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid account group ID"));
        }

        UpdateResult result = await _accountCollection.UpdateOneAsync(
            Builders<AccountGroup>.Filter.Eq(g => g._id, groupObjectId), update);

        if (result.MatchedCount == 0)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Account group was not found"));
        }
    }
}
