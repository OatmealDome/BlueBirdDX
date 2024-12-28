namespace BlueBirdDX.Config.Video;

public class VideoConfig
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