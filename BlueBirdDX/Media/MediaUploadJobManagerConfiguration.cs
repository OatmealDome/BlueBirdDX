namespace BlueBirdDX.Media;

public class MediaUploadJobManagerConfiguration
{
    public string NatsServer
    {
        get;
        set;
    } = "127.0.0.1";

    public string? FFmpegBinariesFolder
    {
        get;
        set;
    }

    public string TemporaryFolder
    {
        get;
        set;
    } = "/tmp";
}
