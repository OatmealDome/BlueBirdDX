using MongoDB.Bson;

namespace BlueBirdDX.Common.Media;

public class UploadedMedia
{
    public const int LatestSchemaVersion = 1;

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

    public string MimeType
    {
        get;
        set;
    }

    public DateTime CreationTime
    {
        get;
        set;
    }
}