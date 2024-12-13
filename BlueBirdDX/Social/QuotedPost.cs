using System.Diagnostics;
using BlueBirdDX.Util;
using OatmealDome.Airship.ATProtocol.Lexicon.Types;

namespace BlueBirdDX.Social;

public class QuotedPost
{
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

    public byte[] ImageData
    {
        get;
        set;
    }

    public string ImageUrl
    {
        get;
        set;
    }

    public SocialPlatform GetPrimaryPlatform()
    {
        if (TwitterId != null)
        {
            return SocialPlatform.Twitter;
        }

        throw new UnreachableException("Not implemented for this platform");
    }

    public string GetPostUrlOnPrimaryPlatform()
    {
        SocialPlatform primaryPlatform = GetPrimaryPlatform();

        if (primaryPlatform == SocialPlatform.Twitter)
        {
            // Twitter will accept anything where the username should be.
            return $"https://twitter.com/_/status/" + TwitterId;
        }
        
        throw new UnreachableException("Not implemented for this platform");
    }
}