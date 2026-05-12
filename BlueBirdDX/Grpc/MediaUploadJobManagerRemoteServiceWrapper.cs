using BlueBirdDX.Media;
using Grpc.Core;
using MongoDB.Bson;

namespace BlueBirdDX.Grpc;

public class MediaUploadJobManagerRemoteServiceWrapper : MediaUploadJobManagerRemoteService.MediaUploadJobManagerRemoteServiceBase
{
    private readonly MediaUploadJobManager _jobManager;

    public MediaUploadJobManagerRemoteServiceWrapper(MediaUploadJobManager jobManager)
    {
        _jobManager = jobManager;
    }

    public override Task<ProcessReadyMediaUploadJobReply> ProcessReadyMediaUploadJob(
        ProcessReadyMediaUploadJobRequest request, ServerCallContext context)
    {
        if (!ObjectId.TryParse(request.JobId, out ObjectId jobId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid media upload job ID"));
        }

        try
        {
            _jobManager.ProcessReadyMediaJob(jobId);
        }
        catch (KeyNotFoundException)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Media upload job was not found"));
        }

        return Task.FromResult(new ProcessReadyMediaUploadJobReply());
    }
}
