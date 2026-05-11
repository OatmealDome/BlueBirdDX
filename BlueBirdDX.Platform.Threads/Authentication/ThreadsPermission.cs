namespace OatmealDome.Unravel.Authentication;

[Flags]
public enum ThreadsPermission
{
    Basic = 1,
    ContentPublish = 2,
    ManageReplies = 4,
    ReadReplies = 8,
    ManageInsights = 16
}