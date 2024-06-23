using BlueBirdDX.Social;
using Quartz;

namespace BlueBirdDX.Scheduler.Job;

[DisallowConcurrentExecution]
public class BbRefreshThreadsTokensJob : BbJob
{
    public override async Task ExecuteJob(IJobExecutionContext context)
    {
        await AccountGroupManager.Instance.RefreshThreadsTokens();
    }
}