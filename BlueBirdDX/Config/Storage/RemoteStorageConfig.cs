namespace BlueBirdDX.Config.Storage;

public class RemoteStorageConfig
{
    public string ServiceUrl
    {
        get;
        set;
    } = "https://nyc3.digitaloceanspaces.com";

    public string Bucket
    {
        get;
        set;
    } = "bluebirddx";

    public string AccessKey
    {
        get;
        set;
    } = "";

    public string AccessKeySecret
    {
        get;
        set;
    } = "";
    
    public RemoteStorageConfig()
    {
        //
    }
}