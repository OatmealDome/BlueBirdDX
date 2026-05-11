namespace OatmealDome.Unravel.Authentication;

[Flags]
public enum ThreadsPermission
{
    Basic = 1,
    ContentPublish = 2,
    ManageReplies = 4,
    ReadReplies = 8,
    ManageInsights = 16,
    Delete = 32,
    KeywordSearch = 64,
    LocationTagging = 128,
    ManageMentions = 256,
    ProfileDiscovery = 512
}
