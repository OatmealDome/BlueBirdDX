namespace BlueBirdDX.Common.Util;

public class BlueskyRef
{
    public string Uri
    {
        get;
        set;
    } = string.Empty;

    public string Cid
    {
        get;
        set;
    } = string.Empty;

    public BlueskyRef()
    {
        //
    }

    public BlueskyRef(string uri, string cid)
    {
        Uri = uri;
        Cid = cid;
    }
}
