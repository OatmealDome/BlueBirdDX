﻿using BlueBirdDX.Config;
using BlueBirdDX.Database;
using BlueBirdDX.Media;
using BlueBirdDX.Scheduler;
using BlueBirdDX.Scheduler.Job;
using BlueBirdDX.Social;
using BlueBirdDX.Util;
using Quartz;
using Serilog;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using Serilog.Sinks.Slack;

const string logFormat =
    "[{Timestamp:MM-dd-yyyy HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
{
    Directory.SetCurrentDirectory("/data");
}

Directory.CreateDirectory("Logs");
            
BbConfig.Load();

if (!BbConfig.Exists())
{
    BbConfig.Instance.Save();

    Console.WriteLine("Wrote initial configuration, will now exit");

    return;
}

LoggerConfiguration logConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Async(c => c.Console(outputTemplate: logFormat))
    .WriteTo.Async(c =>
        c.File("Logs/.log", outputTemplate: logFormat, rollingInterval: RollingInterval.Day));

string slackWebhookUrl = BbConfig.Instance.Logging.SlackWebHookUrl;
if (slackWebhookUrl != "")
{
    logConfig = logConfig.WriteTo.Async(c => c.Slack(slackWebhookUrl, restrictedToMinimumLevel: LogEventLevel.Warning));
}

string lokiUrl = BbConfig.Instance.Logging.LokiUrl;
if (lokiUrl != "")
{
    logConfig = logConfig.WriteTo.GrafanaLoki(lokiUrl, new []
    {
        new LokiLabel()
        {
            Key = "app",
            Value = "BlueBirdDXCore"
        }
    });
}

if (BbConfig.Instance.Logging.EnableSelfLog)
{
    SelfLog.Enable(Console.Error);
}

Log.Logger = logConfig.CreateLogger();

ILogger localLogContext = Log.ForContext(Constants.SourceContextPropertyName, "Program");

localLogContext.Warning("Starting up");

DatabaseManager.Initialize();

await DatabaseManager.Instance.PerformMigration();

await JobScheduler.Initialize();

AccountGroupManager.Initialize();

PostThreadManager.Initialize();

MediaUploadJobManager.Initialize();

await MediaUploadJobManager.Instance.ProcessAllWaitingReadyMediaJobs();

localLogContext.Information("Start up complete");

IJobDetail processJob = JobBuilder.Create<BbProcessPostThreadsJob>()
    .WithIdentity("processJob")
    .Build();

ITrigger processTrigger = TriggerBuilder.Create()
    .WithIdentity("processTrigger")
    .StartAt(DateTime.Now.GetNextInterval(TimeSpan.FromMinutes(1)))
    .WithSimpleSchedule(builder => builder
        .WithIntervalInMinutes(1)
        .RepeatForever())
    .Build();

await JobScheduler.Instance.ScheduleJob(processJob, processTrigger);

IJobDetail threadsTokenJob = JobBuilder.Create<BbRefreshThreadsTokensJob>()
    .WithIdentity("threadsTokenJob")
    .Build();

ITrigger threadsTokenTrigger = TriggerBuilder.Create()
    .WithIdentity("threadsTokenTrigger")
    .StartAt(DateTime.UtcNow)
    .WithSimpleSchedule(builder => builder
        .WithIntervalInHours(24)
        .RepeatForever())
    .Build();

await JobScheduler.Instance.ScheduleJob(threadsTokenJob, threadsTokenTrigger);

IJobDetail mediaJobCleanUpJob = JobBuilder.Create<BbCleanUpOldMediaUploadJobsJob>()
    .WithIdentity("mediaJobCleanUpJob")
    .Build();

ITrigger mediaJobCleanUpTrigger = TriggerBuilder.Create()
    .WithIdentity("mediaJobCleanUpTrigger")
    .StartAt(DateTime.UtcNow)
    .WithSimpleSchedule(builder => builder
        .WithIntervalInHours(24)
        .RepeatForever())
    .Build();

await JobScheduler.Instance.ScheduleJob(mediaJobCleanUpJob, mediaJobCleanUpTrigger);

_ = Task.Run(async () =>
{
    await MediaUploadJobManager.Instance.ListenForReadyMediaJobs();
});

await Task.Delay(-1);
