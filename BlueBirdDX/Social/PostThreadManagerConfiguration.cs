namespace BlueBirdDX.Social;

public class PostThreadManagerConfiguration
{
    public string TextWrapperServer
    {
        get;
        set;
    } = "http://textwrapper";

    public string SeleniumNodeUrl
    {
        get;
        set;
    } = "http://selenium-standalone-chrome:4444/wd/hub";

    public string WebAppUrl
    {
        get;
        set;
    } = "http://bluebirddxwebapp";

    public string? TwitterClientId
    {
        get;
        set;
    }

    public string? TwitterClientSecret
    {
        get;
        set;
    }
}
