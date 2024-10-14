using System.Text.Json.Serialization;

namespace BlueBirdDX.Api;

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
    
    [JsonPropertyName("post_to_threads")]
    public bool PostToThreads
    {
        get;
        set;
    }

    [JsonPropertyName("state")]
    public int State
    {
        get;
        set;
    }

    [JsonPropertyName("parent_thread")]
    public string? ParentThread
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
        PostToThreads = true;
        State = 0; // Draft
        ParentThread = null;
        ScheduledTime = DateTime.UnixEpoch;
        Items = new List<PostThreadItemApi>();
        Items.Add(new PostThreadItemApi());
    }
}
