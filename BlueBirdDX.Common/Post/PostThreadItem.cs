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
}