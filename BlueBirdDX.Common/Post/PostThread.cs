using MongoDB.Bson;

namespace BlueBirdDX.Common.Post;

public class PostThread
{
    public const int LatestSchemaVersion = 2;

    public ObjectId _id
    {
        get;
        set;
    }

    public int SchemaVersion
    {
        get;
        set;
    } = LatestSchemaVersion;

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

    public bool PostToTwitter
    {
        get;
        set;
    }

    public bool PostToBluesky
    {
        get;
        set;
    }

    public bool PostToMastodon
    {
        get;
        set;
    }

    public DateTime ScheduledTime
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