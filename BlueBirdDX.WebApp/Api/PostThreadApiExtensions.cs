using BlueBirdDX.Common.Post;
using BlueBirdDX.PublicApi;
using MongoDB.Bson;

namespace BlueBirdDX.WebApp.Api;

public static class PostThreadApiExtensions
{
    public static PostThreadApi CreateApiFromCommon(PostThread realThread)
    {
        return new PostThreadApi()
        {
            Name = realThread.Name,
            TargetGroup = realThread.TargetGroup.ToString(),
            PostToTwitter = realThread.PostToTwitter,
            PostToBluesky = realThread.PostToBluesky,
            PostToMastodon = realThread.PostToMastodon,
            PostToThreads = realThread.PostToThreads,
            State = (int)realThread.State,
            ParentThread = realThread.ParentThread.ToString(),
            ScheduledTime = realThread.ScheduledTime,
            Items = realThread.Items.Select(i => new PostThreadItemApi()
            {
                Text = i.Text,
                AttachedMedia = i.AttachedMedia.Select(m => m.ToString()).ToList(),
                QuotedPost = i.QuotedPost
            }).ToList()
        };
    }

    public static void TransferApiToCommon(this PostThreadApi apiThread, PostThread realThread)
    {
        realThread.Name = apiThread.Name;
        realThread.TargetGroup = ObjectId.Parse(apiThread.TargetGroup);
        realThread.PostToTwitter = apiThread.PostToTwitter;
        realThread.PostToBluesky = apiThread.PostToBluesky;
        realThread.PostToMastodon = apiThread.PostToMastodon;
        realThread.PostToThreads = apiThread.PostToThreads;
        realThread.State = (PostThreadState)apiThread.State;
        realThread.ParentThread = apiThread.ParentThread != null ? ObjectId.Parse(apiThread.ParentThread) : null;
        realThread.ScheduledTime = apiThread.ScheduledTime;
        realThread.Items = apiThread.Items.Select(p => new PostThreadItem()
        {
            Text = p.Text,
            AttachedMedia = p.AttachedMedia.Select(m => ObjectId.Parse(m)).ToList(),
            QuotedPost = p.QuotedPost
        }).ToList();
    }
}