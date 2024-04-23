namespace BlueBirdDX.Config.Logging;

public class LoggingConfig
{
    public string SlackWebHookUrl
    {
        get;
        set;
    }

    public string LokiUrl
    {
        get;
        set;
    }

    public bool EnableSelfLog
    {
        get;
        set;
    }

    public LoggingConfig()
    {
        SlackWebHookUrl = "";
        LokiUrl = "";
        EnableSelfLog = false;
    }
}