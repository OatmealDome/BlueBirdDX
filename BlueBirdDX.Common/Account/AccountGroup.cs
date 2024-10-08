using MongoDB.Bson;

namespace BlueBirdDX.Common.Account;

public class AccountGroup
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
    }

    public string Name
    {
        get;
        set;
    }

    public TwitterAccount? Twitter
    {
        get;
        set;
    }

    public BlueskyAccount? Bluesky
    {
        get;
        set;
    }

    public MastodonAccount? Mastodon
    {
        get;
        set;
    }

    public ThreadsAccount? Threads
    {
        get;
        set;
    }
}