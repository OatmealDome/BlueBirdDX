using MongoDB.Bson;

namespace BlueBirdDX.Common.Post;

public class PostThread
{
    public const int LatestSchemaVersion = 1;

    public ObjectId _id
    {
        get;
        set;
    }

    public string Name
    {
        get;
        set;
    }

    public ObjectId TargetGroup
    {
        get;
        set;
    }

    public DateTime? ScheduledTime
    {
        get;
        set;
    }

    public PostThreadState State
    {
        get;
        set;
    }

    public string? ErrorMessage
    {
        get;
        set;
    }

    public List<PostThreadItem> Items
    {
        get;
        set;
    }
}