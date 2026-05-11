using System.Diagnostics;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Common.Util;
using MongoDB.Driver;
using OatmealDome.Airship.ATProtocol.Repo;
using OatmealDome.Airship.Bluesky;

namespace BlueBirdDX.Common.Social;

public class QuotedPost
{
    public string? TwitterId
    {
        get;
        set;
    }

    public BlueskyRef? BlueskyRef
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

    public byte[]? ImageData
    {
        get;
        set;
    }

    public string? ImageUrl
    {
        get;
        set;
    }

    public static async Task<QuotedPost> BuildInitialFromUrl(string url,
        IMongoCollection<PostThread> postThreadCollection)
    {
        QuotedPost quotedPost = new QuotedPost();

        Uri uri = new Uri(url);

        if (uri.Host == "x.com" || uri.Host == "twitter.com")
        {
            int queryParametersIdx = url.IndexOf('?');
        
            if (queryParametersIdx != -1)
            {
                url = url.Substring(0, queryParametersIdx);
            }
            
            quotedPost.TwitterId = url.Split('/')[^1];

            PostThreadItem? postThreadItem = postThreadCollection.AsQueryable().SelectMany(t => t.Items)
                .FirstOrDefault(i => i.TwitterId == quotedPost.TwitterId);

            if (postThreadItem != null)
            {
                quotedPost.BlueskyRef = postThreadItem.BlueskyThisRef != null
                    ? new BlueskyRef(postThreadItem.BlueskyThisRef.Uri, postThreadItem.BlueskyThisRef.Cid)
                    : null;
                quotedPost.MastodonId = postThreadItem.MastodonId;
                quotedPost.ThreadsId = postThreadItem.ThreadsId;
            }
        }
        else if (uri.Host == "bsky.app")
        {
            // for example: https://bsky.app/profile/oatmealdome.bsky.social/post/3lcwbawa4n323

            string[] splitPath = uri.PathAndQuery.Split('/');

            string repo = splitPath[^3];
            string key = splitPath[^1];

            BlueskyClient client = new BlueskyClient();
            ATReturnedRecord<OatmealDome.Airship.Bluesky.Feed.Post> returnedRecord = await client.Post_Get(repo, key);

            quotedPost.BlueskyRef = new BlueskyRef(returnedRecord.Ref.Uri, returnedRecord.Ref.Cid);

            PostThreadItem? postThreadItem = postThreadCollection.AsQueryable().SelectMany(t => t.Items)
                .FirstOrDefault(i => i.BlueskyThisRef != null && i.BlueskyThisRef.Uri == quotedPost.BlueskyRef.Uri);

            if (postThreadItem != null)
            {
                quotedPost.TwitterId = postThreadItem.TwitterId;
                quotedPost.MastodonId = postThreadItem.MastodonId;
                quotedPost.ThreadsId = postThreadItem.ThreadsId;
            }
        }
        else
        {
            throw new NotImplementedException("Unsupported URL");
        }

        return quotedPost;
    }

    public SocialPlatform GetPrimaryPlatform()
    {
        if (TwitterId != null)
        {
            return SocialPlatform.Twitter;
        }

        if (BlueskyRef != null)
        {
            return SocialPlatform.Bluesky;
        }

        throw new UnreachableException("Not implemented for this platform");
    }

    public string GetPostUrlOnPrimaryPlatform()
    {
        SocialPlatform primaryPlatform = GetPrimaryPlatform();

        if (primaryPlatform == SocialPlatform.Twitter)
        {
            // Twitter will accept anything where the username should be.
            return $"https://twitter.com/_/status/" + TwitterId;
        }

        if (primaryPlatform == SocialPlatform.Bluesky)
        {
            string[] splitUri = BlueskyRef!.Uri.Split('/');
            
            string did = splitUri[^3];
            string key = splitUri[^1];
            
            return $"https://bsky.app/profile/{did}/post/{key}";
        }
        
        throw new UnreachableException("Not implemented for this platform");
    }
}
