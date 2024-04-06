using System.Text.Json;

namespace BlueBirdDX.Config;

public class BbConfig
{
    private static BbConfig? _instance = null;

    public static BbConfig Instance
    {
        get
        {
            return _instance!;
        }
    }

    private const string ConfigPath = "config.json";

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