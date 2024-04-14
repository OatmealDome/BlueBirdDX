using System.Text.Json.Serialization;
using BlueBirdDX.Common.Post;

namespace BlueBirdDX.WebApp.Api;

public class AccountGroupMiniPostThreadApi
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

    public AccountGroupMiniPostThreadApi(PostThread realThread)
    {
        Id = realThread._id.ToString();
        Name = realThread.Name;
        State = realThread.State.ToString();
    }
}