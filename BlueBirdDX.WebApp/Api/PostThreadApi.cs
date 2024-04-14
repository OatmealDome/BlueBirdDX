using System.Text.Json.Serialization;
using BlueBirdDX.Common.Post;

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
        State = PostThreadState.Draft;
        ScheduledTime = DateTime.UnixEpoch;
        Items = new List<PostThreadItemApi>();
        Items.Add(new PostThreadItemApi());
    }
    
    public PostThreadApi(PostThread realThread)
    {
        Name = realThread.Name;
        State = realThread.State;
        ScheduledTime = realThread.ScheduledTime;
        Items = realThread.Items.Select(i => new PostThreadItemApi(i)).ToList();
    }

    public void TransferToNormal(PostThread realThread)
    {
        realThread.Name = Name;
        realThread.State = State;
        realThread.ScheduledTime = ScheduledTime;
        realThread.Items = Items.Select(p => new PostThreadItem()
        {
            Text = p.Text
        }).ToList();
    }
}