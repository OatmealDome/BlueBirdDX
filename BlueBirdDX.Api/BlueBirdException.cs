namespace BlueBirdDX.Api;

public sealed class BlueBirdException : Exception
{
    public BlueBirdException() : base()
    {
        //
    }
    
    public BlueBirdException(string message) : base(message)
    {
        //
    }
}