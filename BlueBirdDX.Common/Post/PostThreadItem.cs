using BlueBirdDX.Common.Util;
using MongoDB.Bson;

namespace BlueBirdDX.Common.Post;

public class PostThreadItem
{
    public string Text
    {
        get;
        set;
    }

    public List<ObjectId> AttachedMedia
    {
        get;
        set;
    }

    public string? QuotedPost
    {
        get;
        set;
    }

    public string? TwitterId
    {
        get;
        set;
    }

    public BlueskyRef? BlueskyRootRef
    {
        get;
        set;
    }

    public BlueskyRef? BlueskyThisRef
    {
        get;
        set;
    }

    public string? MastodonId
    {
        get;
        set;
    }

    public string? ThreadsId
    {
        get;
        set;
    }
}