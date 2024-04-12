using BlueBirdDX.Social;
using Quartz;

namespace BlueBirdDX.Scheduler.Job;

public class BbProcessPostThreadsJob : BbJob
{
    public override async Task ExecuteJob(IJobExecutionContext context)
    {
        await PostThreadManager.Instance.ProcessPosts();
    }
}