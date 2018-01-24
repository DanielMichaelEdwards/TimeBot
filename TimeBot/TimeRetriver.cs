using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
public class TimeRetriver
{
    private DateTime time;
    private string country;
    public TimeRetriver()
    {
        time = DateTime.Now;
    }
    public TimeRetriver(string country)
    {
        this.country = country;
        setAsCountryTime();        
    }

    private void setAsCountryTime()
    {

        var countryTimeInfo = TimeZoneInfo.FindSystemTimeZoneById(country);

        DateTimeOffset localTime = DateTimeOffset.Now;

        DateTimeOffset countryTime = TimeZoneInfo.ConvertTime(localTime, countryTimeInfo);

        time = countryTime.DateTime;
    }

    public override String ToString()
    {
        return "" + time.Hour + ":" + time.Minute + ":" + time.Second;
    }

}