using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Common.Account;

public class AccountGroup : SlabMongoDocument
{
    public const int LatestSchemaVersion = 2;

    public string Name
    {
        get;
        set;
    } = string.Empty;

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
