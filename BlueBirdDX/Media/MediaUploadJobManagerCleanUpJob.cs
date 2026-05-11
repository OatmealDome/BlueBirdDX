using OatmealDome.Slab;
using Quartz;

namespace BlueBirdDX.Media;

[DisallowConcurrentExecution]
public class MediaUploadJobManagerCleanUpJob : SlabJob
{
    private readonly MediaUploadJobManager _manager;

    public MediaUploadJobManagerCleanUpJob(MediaUploadJobManager manager)
    {
        _manager = manager;
    }

    protected override async Task Run(IJobExecutionContext context)
    {
        await _manager.CleanUpOldJobs();
    }
}
