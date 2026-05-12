using BlueBirdDX.Social;
using Grpc.Core;
using MongoDB.Bson;

namespace BlueBirdDX.Grpc;

public class PostThreadManagerRemoteServiceWrapper : PostThreadManagerRemoteService.PostThreadManagerRemoteServiceBase
{
    private readonly PostThreadManager _postThreadManager;

    public PostThreadManagerRemoteServiceWrapper(PostThreadManager postThreadManager)
    {
        _postThreadManager = postThreadManager;
    }

    public override async Task<DeletePostThreadFromSocialPlatformsReply> DeletePostThreadFromSocialPlatforms(
        DeletePostThreadFromSocialPlatformsRequest request, ServerCallContext context)
    {
        if (!ObjectId.TryParse(request.ThreadId, out ObjectId threadId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid thread ID"));
        }

        try
        {
            await _postThreadManager.DeletePostThreadFromSocialPlatforms(threadId);
        }
        catch (KeyNotFoundException)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Post thread was not found"));
        }
        catch (InvalidOperationException e)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, e.Message));
        }

        return new DeletePostThreadFromSocialPlatformsReply();
    }
}
