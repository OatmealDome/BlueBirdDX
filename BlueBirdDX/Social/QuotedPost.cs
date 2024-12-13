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

        if (BlueskyRef != null)
        {
            return SocialPlatform.Bluesky;
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

        if (primaryPlatform == SocialPlatform.Bluesky)
        {
            string[] splitUri = BlueskyRef!.Uri.Split('/');
            
            string did = splitUri[^3];
            string key = splitUri[^1];
            
            return $"https://bsky.app/profile/{did}/post/{key}";
        }
        
        throw new UnreachableException("Not implemented for this platform");
    }
}