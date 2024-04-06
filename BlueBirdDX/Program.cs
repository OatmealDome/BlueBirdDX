using BlueBirdDX.Config;
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

string slackWebhookUrl = BbConfig.Instance.LoggingConfig.SlackWebHookUrl;
if (slackWebhookUrl != "")
{
    logConfig = logConfig.WriteTo.Async(c =>
        c.Slack(BbConfig.Instance.LoggingConfig.SlackWebHookUrl, restrictedToMinimumLevel: LogEventLevel.Warning));
}

Log.Logger = logConfig.CreateLogger();

ILogger localLogContext = Log.ForContext(Constants.SourceContextPropertyName, "Program");

localLogContext.Warning("Starting up");