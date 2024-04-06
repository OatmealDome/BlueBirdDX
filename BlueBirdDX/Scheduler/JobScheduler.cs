using Quartz;
using Quartz.Impl;

namespace BlueBirdDX.Scheduler;

public class JobScheduler
{
    private static JobScheduler? _instance;
    public static JobScheduler Instance => _instance!;
    
    private readonly IScheduler _scheduler;

    private JobScheduler(IScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public static async Task Initialize()
    {
        StdSchedulerFactory factory = new StdSchedulerFactory();
        IScheduler scheduler = await factory.GetScheduler();

        await scheduler.Start();

        _instance = new JobScheduler(scheduler);
    }

    public async Task ScheduleJob(IJobDetail detail, ITrigger trigger)
    {
        await _scheduler.ScheduleJob(detail, trigger);
    }
}