using MongoDB.Bson;

namespace BlueBirdDX.Common.Media;

public class UploadedMedia
{
    public const int LatestSchemaVersion = 3;

    public ObjectId _id
    {
        get;
        set;
    }

    public int SchemaVersion
    {
        get;
        set;
    } = LatestSchemaVersion;

    public string Name
    {
        get;
        set;
    }

    public string AltText
    {
        get;
        set;
    }

    public string MimeType
    {
        get;
        set;
    }

    public int Width
    {
        get;
        set;
    }

    public int Height
    {
        get;
        set;
    }

    public DateTime CreationTime
    {
        get;
        set;
    }

    public bool HasTwitterOptimizedVersion
    {
        get;
        set;
    }
    
    public bool HasBlueskyOptimizedVersion
    {
        get;
        set;
    }
    
    public bool HasMastodonOptimizedVersion
    {
        get;
        set;
    }
    
    public bool HasThreadsOptimizedVersion
    {
        get;
        set;
    }
}