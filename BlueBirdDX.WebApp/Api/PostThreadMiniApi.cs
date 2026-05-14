using System.Text.Json.Serialization;
using BlueBirdDX.Common.Post;
using MongoDB.Bson;

namespace BlueBirdDX.WebApp.Api;

public class PostThreadMiniApi
{
    [JsonPropertyName("id")]
    public string Id
    {
        get;
        set;
    }
    
    [JsonPropertyName("name")]
    public string Name
    {
        get;
        set;
    }

    [JsonPropertyName("state")]
    public string State
    {
        get;
        set;
    }

    public PostThreadMiniApi(PostThread realThread)
        : this(realThread._id, realThread.Name, realThread.State)
    {
        //
    }

    public PostThreadMiniApi(ObjectId id, string name, PostThreadState state)
    {
        Id = id.ToString();
        Name = name;
        State = state.ToString();
    }
}
