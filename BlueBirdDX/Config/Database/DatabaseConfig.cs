namespace BlueBirdDX.Config.Database;

public class DatabaseConfig
{
    public string ConnectionString
    {
        get;
        set;
    } = "mongodb://root:password@127.0.0.1";

    public string Database
    {
        get;
        set;
    } = "bluebirddx";

    public DatabaseConfig()
    {
        //
    }
}