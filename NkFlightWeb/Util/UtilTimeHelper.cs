namespace NkFlightWeb.Util
{
    public class UtilTimeHelper
    {
        public static DateTime ConvertToDateTimeByTimeSpane(long jsTimeStamp)
        {
            System.DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 当地时区
            DateTime dt = startTime.AddMilliseconds(jsTimeStamp);
            return dt;
        }
    }
}