using BlueBirdDX.Util;
using OatmealDome.Airship.ATProtocol.Lexicon.Types;

namespace BlueBirdDX.Social;

public class QuotedPost
{
    public required string SanitizedUrl
    {
        get;
        set;
    }
    
    public string? TwitterId
    {
        get;
        set;
    }

    public StrongRef? BlueskyRef
    {
        get;
        set;
    }

    public string? MastodonId
    {
        get;
        set;
    }

    public string? ThreadsId
    {
        get;
        set;
    }
}