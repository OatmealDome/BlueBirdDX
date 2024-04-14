using BlueBirdDX.Config;
using BlueBirdDX.Database;
using BlueBirdDX.Scheduler;
using BlueBirdDX.Scheduler.Job;
using BlueBirdDX.Social;
using BlueBirdDX.Util;
using Quartz;
using Serilog;
using Serilog.Core;
using Serilog.Events;
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
    logConfig = logConfig.WriteTo.Async(c =>
        c.Slack(BbConfig.Instance.Logging.SlackWebHookUrl, restrictedToMinimumLevel: LogEventLevel.Warning));
}

Log.Logger = logConfig.CreateLogger();

ILogger localLogContext = Log.ForContext(Constants.SourceContextPropertyName, "Program");

localLogContext.Warning("Starting up");

DatabaseManager.Initialize();

await JobScheduler.Initialize();

PostThreadManager.Initialize();

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

await Task.Delay(-1);
