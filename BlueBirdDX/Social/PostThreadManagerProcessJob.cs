using OatmealDome.Slab;
using Quartz;

namespace BlueBirdDX.Social;

[DisallowConcurrentExecution]
public class PostThreadManagerProcessJob : SlabJob
{
    private readonly PostThreadManager _manager;

    public PostThreadManagerProcessJob(PostThreadManager manager)
    {
        _manager = manager;
    }

    protected override async Task Run(IJobExecutionContext context)
    {
        await _manager.ProcessPostThreads();
    }
}
