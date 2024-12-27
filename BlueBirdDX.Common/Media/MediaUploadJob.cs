using MongoDB.Bson;

namespace BlueBirdDX.Common.Media;

public class MediaUploadJob
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

    public string AltText
    {
        get;
        set;
    }
    
    public DateTime CreationTime
    {
        get;
        set;
    }

    public MediaUploadJobState State
    {
        get;
        set;
    }

    public string? ErrorDetail
    {
        get;
        set;
    }

    public ObjectId? MediaId
    {
        get;
        set;
    }

    public bool IsJobForMigrationTwoToThree
    {
        get;
        set;
    }
}