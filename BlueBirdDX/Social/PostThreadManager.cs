namespace BlueBirdDX.Social;

public class PostThreadManager
{
    private static PostThreadManager? _instance;
    public static PostThreadManager Instance => _instance!;

    private PostThreadManager()
    {
        
    }
    
    public static void Initialize()
    {
        _instance = new PostThreadManager();
    }

    public async Task ProcessPostThreads()
    {
        // TODO
        throw new NotImplementedException();
    }
}