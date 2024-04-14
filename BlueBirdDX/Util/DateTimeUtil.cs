namespace BlueBirdDX.Util;

public static class DateTimeUtil
{
    // https://stackoverflow.com/a/7029464
    public static DateTime GetNextInterval(this DateTime dt, TimeSpan d)
    {
        return new DateTime((dt.Ticks + d.Ticks - 1) / d.Ticks * d.Ticks, dt.Kind);
    }
}