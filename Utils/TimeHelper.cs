namespace TripWiseAPI.Utils;
using Microsoft.Extensions.Configuration;
using System;

public class TimeHelper
{
        public static DateTime GetVietnamTime()
        {
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
        }
}
