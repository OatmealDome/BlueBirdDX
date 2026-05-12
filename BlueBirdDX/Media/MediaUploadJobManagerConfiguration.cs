namespace BlueBirdDX.Media;

public class MediaUploadJobManagerConfiguration
{
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
