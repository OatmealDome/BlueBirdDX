namespace BlueBirdDX.Config.WebDriver;

public class WebDriverConfig
{
    public string NodeUrl
    {
        get;
        set;
    } = "http://selenium-standalone-chrome:4444/wd/hub";

    public string ScreenshotUrlFormat
    {
        get;
        set;
    } = "http://bluebirddxwebapp/quote/{0}?url={1}";
}