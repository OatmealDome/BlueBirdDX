using Quartz;
using Serilog;
using Serilog.Core;

namespace BlueBirdDX.Scheduler.Job;

public abstract class BbJob : IJob
{
    protected BbJob()
    {
    }

    public abstract Task ExecuteJob(IJobExecutionContext context);

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await ExecuteJob(context);
        }
        catch (Exception e)
        {
            ILogger logContext = Log.ForContext(Constants.SourceContextPropertyName, this.GetType().Name);
            logContext.Error(e, "Unhandled exception in job");
        }
    }
}