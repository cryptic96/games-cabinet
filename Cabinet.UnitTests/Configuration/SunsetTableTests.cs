using System.Text.RegularExpressions;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>
/// Holds the sunset table the evening lights follow to an independent solar calculation. The table ships in the head script as one
/// fixed list of standard-time sunrise and lamp-on minutes at 52 degrees north, 5 degrees east. The calculation in this class is
/// written here, shares nothing with the table's own source, and uses the low-precision solar position of the astronomical
/// almanac, so a wrong row, a wrong interpolation or a drift over the years shows as a difference in minutes.
/// </summary>
[Trait("Category", "Configuration")]
public partial class SunsetTableTests
{
    private const double Latitude = 52;
    private const double Longitude = 5;
    private const double StandardOffsetMinutes = 60;
    private const double SunriseZenith = 90.833;
    private const double LampLeadMinutes = 30;
    private const double TableToleranceMinutes = 5;
    private const double AnchorToleranceMinutes = 2;
    private const int SummerShiftMinutes = 60;
    private const int DaysPerYear = 365;
    private const int RowsInTable = 36;

    private static readonly Lazy<int[][]> Table = new(ReadTable);

    private static readonly int[] FirstDayOfMonth = [1, 32, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335];

    /// <summary>The table has one row for the 1st, 11th and 21st of every month, ordered by day, and every row names that calendar day.</summary>
    [Fact]
    public void The_table_has_a_row_for_the_first_eleventh_and_twenty_first_of_every_month_in_order()
    {
        var rows = Table.Value;

        rows.Should().HaveCount(RowsInTable);
        rows.Select(row => row[0]).Should().BeInAscendingOrder();

        for (var month = 0; month < 12; month++)
        {
            var days = rows.Skip(month * 3).Take(3).Select(row => row[0]).ToArray();

            days.Should().Equal([FirstDayOfMonth[month], FirstDayOfMonth[month] + 10, FirstDayOfMonth[month] + 20], $"month {month + 1}");
        }
    }

    /// <summary>The solstice anchors hold: 21 December sunrise 08:46 and sunset 16:30, 21 June sunrise 05:20 and sunset 22:04 on the clock with summer time.</summary>
    [Fact]
    public void The_solstice_anchors_read_the_same_times_in_the_table_and_the_calculation()
    {
        var december = At(Table.Value, 355);
        var june = At(Table.Value, 172);

        december.Rise.Should().BeApproximately(Minutes(8, 46), AnchorToleranceMinutes);
        (december.LampOn + LampLeadMinutes).Should().BeApproximately(Minutes(16, 30), AnchorToleranceMinutes);
        (june.Rise + SummerShiftMinutes).Should().BeApproximately(Minutes(5, 20), AnchorToleranceMinutes);
        (june.LampOn + LampLeadMinutes + SummerShiftMinutes).Should().BeApproximately(Minutes(22, 4), AnchorToleranceMinutes);

        var winterSun = SolarMinutes(new DateOnly(2027, 12, 21));
        var summerSun = SolarMinutes(new DateOnly(2027, 6, 21));

        winterSun.Rise.Should().BeApproximately(Minutes(8, 46), AnchorToleranceMinutes);
        winterSun.Set.Should().BeApproximately(Minutes(16, 30), AnchorToleranceMinutes);
        (summerSun.Rise + SummerShiftMinutes).Should().BeApproximately(Minutes(5, 20), AnchorToleranceMinutes);
        (summerSun.Set + SummerShiftMinutes).Should().BeApproximately(Minutes(22, 4), AnchorToleranceMinutes);
    }

    /// <summary>Every day of four years, leap year included, the interpolated table is within five minutes of the calculation for both moments.</summary>
    [Theory]
    [InlineData(2027)]
    [InlineData(2028)]
    [InlineData(2029)]
    [InlineData(2030)]
    public void Every_day_of_the_year_the_interpolated_table_is_within_five_minutes_of_the_sun(int year)
    {
        var rows = Table.Value;
        var worst = 0.0;
        var worstDay = default(DateOnly);

        for (var date = new DateOnly(year, 1, 1); date.Year == year; date = date.AddDays(1))
        {
            var sun = SolarMinutes(date);
            var table = At(rows, date.DayOfYear);
            var difference = Math.Max(Math.Abs(table.Rise - sun.Rise), Math.Abs(table.LampOn + LampLeadMinutes - sun.Set));

            if (difference > worst)
            {
                worst = difference;
                worstDay = date;
            }
        }

        worst.Should().BeLessThanOrEqualTo(TableToleranceMinutes, $"the worst day was {worstDay:yyyy-MM-dd}");
    }

    private static (double Rise, double LampOn) At(int[][] rows, int dayOfYear)
    {
        var upperIndex = Array.FindIndex(rows, row => row[0] > dayOfYear);
        var wrapped = new[] { rows[0][0] + DaysPerYear, rows[0][1], rows[0][2] };
        var upper = upperIndex < 0 ? wrapped : rows[upperIndex];
        var lower = rows[upperIndex < 0 ? rows.Length - 1 : upperIndex - 1];
        var share = (double)(dayOfYear - lower[0]) / (upper[0] - lower[0]);

        return (lower[1] + (share * (upper[1] - lower[1])), lower[2] + (share * (upper[2] - lower[2])));
    }

    /// <summary>
    /// Sunrise and sunset in standard minutes since midnight, from the low-precision solar position of the astronomical almanac:
    /// mean longitude and anomaly, ecliptic longitude, then right ascension and declination, the equation of time as the
    /// difference of mean longitude and right ascension, and the hour angle for the sun's upper edge at the horizon with
    /// refraction. The sun is taken at local noon, which is exact enough for a day's two moments.
    /// </summary>
    private static (double Rise, double Set) SolarMinutes(DateOnly date)
    {
        var julianDay = date.DayNumber + 1721425.5 + ((720 - StandardOffsetMinutes) / 1440.0);
        var days = julianDay - 2451545.0;
        var meanLongitude = Normalise(280.460 + (0.9856474 * days));
        var meanAnomaly = ToRadians(Normalise(357.528 + (0.9856003 * days)));
        var eclipticLongitude = ToRadians(meanLongitude + (1.915 * Math.Sin(meanAnomaly)) + (0.020 * Math.Sin(2 * meanAnomaly)));
        var obliquity = ToRadians(23.439 - (0.0000004 * days));
        var rightAscension = Math.Atan2(Math.Cos(obliquity) * Math.Sin(eclipticLongitude), Math.Cos(eclipticLongitude));
        var declination = Math.Asin(Math.Sin(obliquity) * Math.Sin(eclipticLongitude));
        var equationOfTime = 4 * Wrap(meanLongitude - ToDegrees(rightAscension));
        var latitude = ToRadians(Latitude);
        var cosineHourAngle = (Math.Cos(ToRadians(SunriseZenith)) - (Math.Sin(latitude) * Math.Sin(declination))) / (Math.Cos(latitude) * Math.Cos(declination));
        var hourAngle = ToDegrees(Math.Acos(cosineHourAngle));
        var noon = 720 - (4 * Longitude) - equationOfTime + StandardOffsetMinutes;

        return (noon - (4 * hourAngle), noon + (4 * hourAngle));
    }

    private static double Normalise(double degrees) => degrees - (360 * Math.Floor(degrees / 360));

    private static double Wrap(double degrees) => Normalise(degrees + 180) - 180;

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;

    private static double Minutes(int hours, int minutes) => (hours * 60) + minutes;

    private static int[][] ReadTable()
    {
        var path = Path.Combine(RepositoryPaths.ServiceDirectory(), "wwwroot", "js", "evening.js");
        var rows = File.ReadAllLines(path)
            .Select(line => TableRow().Match(line))
            .Where(match => match.Success)
            .Select(match => new[] { int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value) })
            .ToArray();

        return rows;
    }

    [GeneratedRegex(@"^\s*\[(\d+), (\d+), (\d+)\],\s*$")]
    private static partial Regex TableRow();
}
