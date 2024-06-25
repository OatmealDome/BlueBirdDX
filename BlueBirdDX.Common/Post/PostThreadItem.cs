using BlueBirdDX.Common.Util;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

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

    [BsonIgnore]
    public string? QuotedPostSanitized
    {
        get
        {
            if (QuotedPost == null)
            {
                return null;
            }
            
            // Remove the query parameters (usually just analytics stuff) and always use twitter.com as the domain.

            string url = QuotedPost.Replace("x.com", "twitter.com");
            
            int queryParametersIdx = url.IndexOf('?');
        
            if (queryParametersIdx != -1)
            {
                url = url.Substring(0, queryParametersIdx);
            }

            return url;
        }
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