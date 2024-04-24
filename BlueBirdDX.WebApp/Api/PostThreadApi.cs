using System.Text.Json.Serialization;
using BlueBirdDX.Common.Post;
using MongoDB.Bson;

namespace BlueBirdDX.WebApp.Api;

// Exposes a subset of PostThread for API use.
public class PostThreadApi
{
    [JsonPropertyName("name")]
    public string Name
    {
        get;
        set;
    }

    [JsonPropertyName("target_group")]
    public string TargetGroup
    {
        get;
        set;
    }

    [JsonPropertyName("post_to_twitter")]
    public bool PostToTwitter
    {
        get;
        set;
    }

    [JsonPropertyName("post_to_bluesky")]
    public bool PostToBluesky
    {
        get;
        set;
    }

    [JsonPropertyName("post_to_mastodon")]
    public bool PostToMastodon
    {
        get;
        set;
    }

    [JsonPropertyName("state")]
    public PostThreadState State
    {
        get;
        set;
    }

    [JsonPropertyName("scheduled_time")]
    public DateTime ScheduledTime
    {
        get;
        set;
    }

    [JsonPropertyName("items")]
    public List<PostThreadItemApi> Items
    {
        get;
        set;
    }
    
    public PostThreadApi()
    {
        Name = "";
        TargetGroup = "000000000000000000000000";
        PostToTwitter = true;
        PostToBluesky = true;
        PostToMastodon = true;
        State = PostThreadState.Draft;
        ScheduledTime = DateTime.UnixEpoch;
        Items = new List<PostThreadItemApi>();
        Items.Add(new PostThreadItemApi());
    }
    
    public PostThreadApi(PostThread realThread)
    {
        Name = realThread.Name;
        TargetGroup = realThread.TargetGroup.ToString();
        PostToTwitter = realThread.PostToTwitter;
        PostToBluesky = realThread.PostToBluesky;
        PostToMastodon = realThread.PostToMastodon;
        State = realThread.State;
        ScheduledTime = realThread.ScheduledTime;
        Items = realThread.Items.Select(i => new PostThreadItemApi(i)).ToList();
    }

    public void TransferToNormal(PostThread realThread)
    {
        realThread.Name = Name;
        realThread.TargetGroup = ObjectId.Parse(TargetGroup);
        realThread.PostToTwitter = PostToTwitter;
        realThread.PostToBluesky = PostToBluesky;
        realThread.PostToMastodon = PostToMastodon;
        realThread.State = State;
        realThread.ScheduledTime = ScheduledTime;
        realThread.Items = Items.Select(p => new PostThreadItem()
        {
            Text = p.Text,
            AttachedMedia = p.AttachedMedia.Select(m => ObjectId.Parse(m)).ToList(),
            QuotedPost = p.QuotedPost
        }).ToList();
    }
}