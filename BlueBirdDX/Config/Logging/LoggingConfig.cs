namespace BlueBirdDX.Config.Logging;

public class LoggingConfig
{
    public string SlackWebHookUrl
    {
        get;
        set;
    }

    public LoggingConfig()
    {
        SlackWebHookUrl = "";
    }
}