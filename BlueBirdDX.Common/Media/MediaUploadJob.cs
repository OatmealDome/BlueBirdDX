using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Common.Media;

public class MediaUploadJob : SlabMongoDocument
{
    public const int LatestSchemaVersion = 4;

    public string Name
    {
        get;
        set;
    } = string.Empty;

    public string MimeType
    {
        get;
        set;
    } = string.Empty;

    public string AltText
    {
        get;
        set;
    } = string.Empty;

    public DateTime CreationTime
    {
        get;
        set;
    } = DateTime.MinValue;

    public MediaUploadJobState State
    {
        get;
        set;
    } = MediaUploadJobState.Uploading;

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
}
