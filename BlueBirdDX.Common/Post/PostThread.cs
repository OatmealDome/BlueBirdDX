using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Common.Post;

public class PostThread : SlabMongoDocument
{
    public const int LatestSchemaVersion = 4;

    public string Name
    {
        get;
        set;
    } = string.Empty;

    public ObjectId TargetGroup
    {
        get;
        set;
    } = ObjectId.Empty;

    public bool PostToTwitter
    {
        get;
        set;
    } = false;

    public bool PostToBluesky
    {
        get;
        set;
    } = false;

    public bool PostToMastodon
    {
        get;
        set;
    } = false;

    public bool PostToThreads
    {
        get;
        set;
    } = false;

    public ObjectId? ParentThread
    {
        get;
        set;
    }

    public DateTime ScheduledTime
    {
        get;
        set;
    } = DateTime.MinValue;

    public PostThreadState State
    {
        get;
        set;
    } = PostThreadState.Draft;

    public string? ErrorMessage
    {
        get;
        set;
    }

    public List<PostThreadItem> Items
    {
        get;
        set;
    } = new List<PostThreadItem>();
}
