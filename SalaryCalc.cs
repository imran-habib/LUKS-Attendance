using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LuksAttendance;

public class AttendanceRow
{
    public string Name { get; set; } = "";
    public int Day { get; set; }
    public string InTime { get; set; } = "";
    public string OutTime { get; set; } = "";
    public string Worked { get; set; } = "";
    public string OT { get; set; } = "";
    public string Deduction { get; set; } = "";
    public string Status { get; set; } = "";
}

public class SalaryRow
{
    public string Name { get; set; } = "";
    public int Days { get; set; }
    public double OtHours { get; set; }
    public double DedHours { get; set; }
    public double NetHours { get; set; }
    public int DailyRate { get; set; }
    public double HourlyRate { get; set; }
    public double ExtraHrs { get; set; }
    public decimal Advance { get; set; }
    public decimal Arrears { get; set; }
    public decimal NetSalary { get; set; }
}

public static class SalaryCalc
{
    private const int StandardWorkHours = 8;
    private const int LunchBreakMinutes = 60;
    private const int LunchCutoffHour = 13;
    private const int RoundingMinutes = 15;

    public static AttendanceRow BuildAttendanceRow(PunchRecord rec)
    {
        if (string.IsNullOrEmpty(rec.InTime) || string.IsNullOrEmpty(rec.OutTime))
            return new AttendanceRow { Name = rec.Name, Day = rec.Day, Status = rec.Status };

        var presence = CalcPresence(rec.InTime, rec.OutTime);
        presence = RoundToNearest(presence, RoundingMinutes);
        var effective = CalcEffective(presence, rec.OutTime);
        var diff = effective - TimeSpan.FromHours(StandardWorkHours);

        return new AttendanceRow
        {
            Name = rec.Name, Day = rec.Day,
            InTime = rec.InTime, OutTime = rec.OutTime,
            Worked = FormatTs(effective),
            OT = diff > TimeSpan.Zero ? FormatTs(diff) : "",
            Deduction = diff < TimeSpan.Zero ? FormatTs(diff.Negate()) : "",
            Status = rec.Status
        };
    }

    public static SalaryRow Calculate(string nameKey, List<AttendanceRow> rows, int dailyRate,
        decimal advance, decimal arrears)
    {
        var totalOt = TimeSpan.Zero;
        var totalDed = TimeSpan.Zero;
        int daysWorked = rows.Count(r => !string.IsNullOrEmpty(r.Worked));

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.OT) && string.IsNullOrEmpty(row.Deduction)) continue;
            if (!string.IsNullOrEmpty(row.OT)) totalOt += ParseTs(row.OT);
            if (!string.IsNullOrEmpty(row.Deduction)) totalDed += ParseTs(row.Deduction);
        }

        double otH = Math.Round(totalOt.TotalHours, 2);
        double dedH = Math.Round(totalDed.TotalHours, 2);
        double netH = Math.Round(otH - dedH, 2);
        double hourlyRate = dailyRate / 8.0;
        decimal netSalary = Math.Round(
            (decimal)(daysWorked * dailyRate + netH * hourlyRate) - advance + arrears, 2);

        return new SalaryRow
        {
            Name = string.Join(" ", nameKey.Split(' ').Select(w =>
                char.ToUpper(w[0]) + w[1..])),
            Days = daysWorked, OtHours = otH, DedHours = dedH,
            NetHours = netH, DailyRate = dailyRate,
            HourlyRate = Math.Round(hourlyRate, 2),
            Advance = advance, Arrears = arrears, NetSalary = netSalary
        };
    }

    private static TimeSpan CalcPresence(string inTime, string outTime)
    {
        var inDt = TimeSpan.ParseExact(inTime, "hh\\:mm", CultureInfo.InvariantCulture);
        var outDt = TimeSpan.ParseExact(outTime, "hh\\:mm", CultureInfo.InvariantCulture);
        var diff = outDt - inDt;
        if (diff < TimeSpan.Zero) diff += TimeSpan.FromHours(24); // cross-midnight
        return diff;
    }

    private static TimeSpan CalcEffective(TimeSpan presence, string outTime)
    {
        int outHour = int.Parse(outTime[..2]);
        // Deduct lunch if OUT >= 13:00
        if (outHour >= LunchCutoffHour)
            return presence - TimeSpan.FromMinutes(LunchBreakMinutes);
        return presence;
    }

    private static TimeSpan RoundToNearest(TimeSpan ts, int minutes)
    {
        double totalMin = ts.TotalMinutes;
        double rounded = Math.Round(totalMin / minutes) * minutes;
        return TimeSpan.FromMinutes(rounded);
    }

    private static string FormatTs(TimeSpan ts)
    {
        int totalMin = (int)Math.Abs(ts.TotalMinutes);
        string sign = ts < TimeSpan.Zero ? "-" : "";
        return $"{sign}{totalMin / 60:D2}:{totalMin % 60:D2}";
    }

    private static TimeSpan ParseTs(string s)
    {
        bool neg = s.StartsWith('-');
        if (neg) s = s[1..];
        var parts = s.Split(':');
        return new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), 0) * (neg ? -1 : 1);
    }
}

public static class TimeHelper
{
    public static bool IsValidTime(string s)
    {
        if (s.Length != 5 || s[2] != ':') return false;
        return int.TryParse(s[..2], out int h) && int.TryParse(s[3..], out int m)
               && h >= 0 && h <= 23 && m >= 0 && m <= 59;
    }
}
