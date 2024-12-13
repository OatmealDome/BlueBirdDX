namespace BlueBirdDX.Util;

public static class SocialPlatformUtil
{
    public static string ToEmoji(this SocialPlatform platform)
    {
        switch (platform)
        {
            case SocialPlatform.Twitter:
                return "🐦";
            case SocialPlatform.Bluesky:
                return "🦋";
            case SocialPlatform.Mastodon:
                return "🐘";
            case SocialPlatform.Threads:
                return "🧵";
            default:
                throw new NotImplementedException("SocialPlatform not supported");
        }
    }
}