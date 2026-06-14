#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LuksAttendance;

public class AttendanceRow
{
    public string Name { get; set; } = "";
    public string Day { get; set; } = "";  // e.g. "23-Jan (Fr)"
    public string InTime { get; set; } = "";
    public string OutTime { get; set; } = "";
    public string Worked { get; set; } = "";
    public string OT { get; set; } = "";
    public string Deduction { get; set; } = "";
    public string Status { get; set; } = "";
}

public class SalaryRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? prop = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

    private void Recalculate()
    {
        NetSalary = Math.Round(
            (decimal)(Days * DailyRate + (NetHours + _extraHrs) * HourlyRate)
            - _advance + _arrears, 2);
    }

    public string Category { get; set; } = "";
    public string StatusIndicator { get; set; } = "⚪";
    public string Name { get; set; } = "";
    public int Days { get; set; }
    public double OtHours { get; set; }
    public double DedHours { get; set; }
    public double NetHours { get; set; }
    public int DailyRate { get; set; }
    public double HourlyRate { get; set; }

    private double _extraHrs;
    public double ExtraHrs
    {
        get => _extraHrs;
        set { _extraHrs = value; OnPropertyChanged(); Recalculate(); }
    }

    private decimal _advance;
    public decimal Advance
    {
        get => _advance;
        set { _advance = value; OnPropertyChanged(); Recalculate(); }
    }

    private decimal _arrears;
    public decimal Arrears
    {
        get => _arrears;
        set { _arrears = value; OnPropertyChanged(); Recalculate(); }
    }

    private decimal _netSalary;
    public decimal NetSalary
    {
        get => _netSalary;
        set { _netSalary = value; OnPropertyChanged(); }
    }
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
            return new AttendanceRow { Name = rec.Name, Day = rec.DayLabel, Status = rec.Status };

        var presence = CalcPresence(rec.InTime, rec.OutTime);
        presence = RoundToNearest(presence, RoundingMinutes);
        var effective = CalcEffective(presence, rec.OutTime);
        var diff = effective - TimeSpan.FromHours(StandardWorkHours);

        return new AttendanceRow
        {
            Name = rec.Name, Day = rec.DayLabel,
            InTime = rec.InTime, OutTime = rec.OutTime,
            Worked = FormatTs(effective),
            OT = diff > TimeSpan.Zero ? FormatTs(diff) : "",
            Deduction = diff < TimeSpan.Zero ? FormatTs(diff.Negate()) : "",
            Status = rec.Status
        };
    }

    public static SalaryRow Calculate(string nameKey, List<AttendanceRow> rows, int dailyRate,
        decimal advance, decimal arrears, string category = "")
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

        var row2 = new SalaryRow
        {
            Category = category,
            Name = string.Join(" ", nameKey.Split(' ').Select(w =>
                char.ToUpper(w[0]) + w[1..])),
            Days = daysWorked, OtHours = otH, DedHours = dedH,
            NetHours = netH, DailyRate = dailyRate,
            HourlyRate = Math.Round(hourlyRate, 2),
        };
        row2.ExtraHrs = 0;
        row2.Advance = advance;
        row2.Arrears = arrears;
        return row2;
    }

    private static TimeSpan CalcPresence(string inTime, string outTime)
    {
        var inDt = TimeSpan.ParseExact(inTime, "hh\\:mm", CultureInfo.InvariantCulture);
        var outDt = TimeSpan.ParseExact(outTime, "hh\\:mm", CultureInfo.InvariantCulture);
        var diff = outDt - inDt;
        if (diff < TimeSpan.Zero) diff += TimeSpan.FromHours(24);
        return diff;
    }

    private static TimeSpan CalcEffective(TimeSpan presence, string outTime)
    {
        int outHour = int.Parse(outTime[..2]);
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
