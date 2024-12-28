using System.Text.Json;
using System.Text.Json.Serialization;
using BlueBirdDX.Config.Database;
using BlueBirdDX.Config.Logging;
using BlueBirdDX.Config.Notification;
using BlueBirdDX.Config.Storage;
using BlueBirdDX.Config.TextWrapper;
using BlueBirdDX.Config.Video;
using BlueBirdDX.Config.WebDriver;

namespace BlueBirdDX.Config;

public class BbConfig
{
    private static BbConfig? _instance = null;
    public static BbConfig Instance => _instance!;

    private const string ConfigPath = "config.json";
    
    [JsonPropertyName("Logging")]
    public LoggingConfig Logging
    {
        get;
        set;
    }
    
    [JsonPropertyName("Database")]
    public DatabaseConfig Database
    {
        get;
        set;
    }

    [JsonPropertyName("RemoteStorage")]
    public RemoteStorageConfig RemoteStorage
    {
        get;
        set;
    }

    [JsonPropertyName("WebDriver")]
    public WebDriverConfig WebDriver
    {
        get;
        set;
    }

    [JsonPropertyName("TextWrapper")]
    public TextWrapperConfig TextWrapper
    {
        get;
        set;
    }

    [JsonPropertyName("Notification")]
    public NotificationConfig Notification
    {
        get;
        set;
    }

    [JsonPropertyName("Video")]
    public VideoConfig Video
    {
        get;
        set;
    }

    public BbConfig()
    {
        Logging = new LoggingConfig();
        Database = new DatabaseConfig();
        RemoteStorage = new RemoteStorageConfig();
        WebDriver = new WebDriverConfig();
        TextWrapper = new TextWrapperConfig();
    }

    public static void Load()
    {
        if (Exists())
        {
            _instance = JsonSerializer.Deserialize<BbConfig>(File.ReadAllText(ConfigPath))!;
        }
        else
        {
            _instance = new BbConfig();
        }
    }
        
    public static bool Exists()
    {
        return File.Exists(ConfigPath);
    }

    public void Save()
    {
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions()
        {
            WriteIndented = true
        }));
    }
}