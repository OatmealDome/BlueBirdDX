using MongoDB.Bson;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.Common.Media;

public class UploadedMedia : SlabMongoDocument
{
    public const int LatestSchemaVersion = 3;

    public string Name
    {
        get;
        set;
    } = string.Empty;

    public string AltText
    {
        get;
        set;
    } = string.Empty;

    public string MimeType
    {
        get;
        set;
    } = string.Empty;

    public int Width
    {
        get;
        set;
    } = 0;

    public int Height
    {
        get;
        set;
    } = 0;

    public DateTime CreationTime
    {
        get;
        set;
    } = DateTime.MinValue;

    public bool HasTwitterOptimizedVersion
    {
        get;
        set;
    } = false;

    public bool HasBlueskyOptimizedVersion
    {
        get;
        set;
    } = false;
    
    public bool HasMastodonOptimizedVersion
    {
        get;
        set;
    } = false;
    
    public bool HasThreadsOptimizedVersion
    {
        get;
        set;
    } = false;
}
