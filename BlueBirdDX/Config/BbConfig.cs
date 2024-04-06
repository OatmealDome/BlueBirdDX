using System.Text.Json;
using System.Text.Json.Serialization;
using BlueBirdDX.Config.Database;
using BlueBirdDX.Config.Logging;

namespace BlueBirdDX.Config;

public class BbConfig
{
    private static BbConfig? _instance = null;
    public static BbConfig Instance => _instance!;

    private const string ConfigPath = "config.json";
    
    [JsonPropertyName("Logging")]
    public LoggingConfig LoggingConfig
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

    public BbConfig()
    {
        //
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