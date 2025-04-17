namespace CXP.Utilities
{
    public partial class Utils
    {
        public static DateTime UnixTimestampToDateTime(double unixTimestampSeconds)
        {
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimestampSeconds).ToLocalTime();
            return dateTime;
        }

        public static DateTime ConvertUtcToEST(DateTime dateTimeUTC)
        {
            TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime dateTimeEST = TimeZoneInfo.ConvertTimeFromUtc(dateTimeUTC, easternTimeZone);
            return dateTimeEST;
        }

        public static DateTimeOffset ConvertUtcToEST(DateTimeOffset dateTime)
        {
            var easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            return TimeZoneInfo.ConvertTime(dateTime, easternZone);
        }
    }
}
