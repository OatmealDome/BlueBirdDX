using OatmealDome.Slab;
using Quartz;

namespace BlueBirdDX.Social;

[DisallowConcurrentExecution]
public class AccountGroupManagerRefreshThreadsJob : SlabJob
{
    private readonly AccountGroupManager _manager;

    public AccountGroupManagerRefreshThreadsJob(AccountGroupManager manager)
    {
        _manager = manager;
    }

    protected override async Task Run(IJobExecutionContext context)
    {
        await _manager.RefreshThreadsTokens();
    }
}
