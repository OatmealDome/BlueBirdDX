using BlueBirdDX.Media;
using Quartz;

namespace BlueBirdDX.Scheduler.Job;

[DisallowConcurrentExecution]
public class BbCleanUpOldMediaUploadJobsJob : BbJob
{
    public override async Task ExecuteJob(IJobExecutionContext context)
    {
        await MediaUploadJobManager.Instance.CleanUpOldJobs();
    }
}