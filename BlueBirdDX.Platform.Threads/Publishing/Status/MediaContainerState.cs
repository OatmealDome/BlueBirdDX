namespace OatmealDome.Unravel.Publishing;

public sealed class MediaContainerState
{
    public MediaContainerStatus Status
    {
        get;
        set;
    }

    public string? ErrorMessage
    {
        get;
        set;
    }
}