using System.Diagnostics;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Common.Util;
using idunno.AtProto;
using idunno.Bluesky;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed;
using MongoDB.Driver;
using PostThread = BlueBirdDX.Common.Post.PostThread;

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

            BlueskyAgent agent = new BlueskyAgent();

            Handle handle = Handle.FromString(repo);

            AtProtoHttpResult<ProfileViewDetailed> profileView = await agent.GetProfile(handle);
            profileView.EnsureSucceeded();

            // "at://{repo}/app.bsky.feed.post/{key}" should be a valid URI, but this doesn't work for some reason.
            AtUri atUri = AtUri.FromString($"at://{profileView.Result.Did}/app.bsky.feed.post/{key}");

            AtProtoHttpResult<PostView> view = await agent.GetPost(atUri);
            view.EnsureSucceeded();

            quotedPost.BlueskyRef = new BlueskyRef(view.Result.Uri.ToString(), view.Result.Cid.ToString());

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
