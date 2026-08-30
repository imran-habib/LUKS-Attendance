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
    // ═══ Public constants (used by SalaryRulesWindow for display) ═══
    public const int StandardWorkHours = 8;
    public const int LunchBreakMinutes = 60;
    public const int LunchCutoffHour = 13;
    public const int RoundingMinutes = 15;
    public const int GraceWindowHours = 1; // ±1h exclusive

    // Standard auto-fill shift duration (configurable)
    public static int DefaultShiftHours { get; set; } = 9;

    /// <summary>
    /// Grace window is (-1h, +1h) EXCLUSIVE by business decision (confirmed with owner).
    /// At exactly 7h or 9h effective, OT/deduction DOES apply. This is intentional.
    /// Grace is now determined ONLY by EmployeeEntry.IsGrace flag — no hardcoded names.
    /// </summary>
    public static AttendanceRow BuildAttendanceRow(PunchRecord rec, bool isGrace = false)
    {
        if (string.IsNullOrEmpty(rec.InTime) || string.IsNullOrEmpty(rec.OutTime))
            return new AttendanceRow { Name = rec.Name, Day = rec.DayLabel, Status = rec.Status };

        var presence = CalcPresence(rec.InTime, rec.OutTime);
        var effective = CalcEffective(presence, rec.OutTime);
        effective = RoundToNearest(effective, RoundingMinutes);
        var diff = effective - TimeSpan.FromHours(StandardWorkHours);

        // Grace from EmployeeEntry.IsGrace only — no hardcoded name list
        if (isGrace && diff > TimeSpan.FromHours(-GraceWindowHours) && diff < TimeSpan.FromHours(GraceWindowHours))
            diff = TimeSpan.Zero;

        // Hide +1 internal format from user-facing UI
        var displayOut = rec.OutTime.EndsWith("+1") ? rec.OutTime[..5] + " (next day)" : rec.OutTime;

        return new AttendanceRow
        {
            Name = rec.Name, Day = rec.DayLabel,
            InTime = rec.InTime, OutTime = displayOut,
            Worked = FormatTs(effective),
            OT = diff > TimeSpan.Zero ? FormatTs(diff) : "",
            Deduction = diff < TimeSpan.Zero ? FormatTs(diff.Negate()) : "",
            Status = rec.Status
        };
    }

    public static SalaryRow Calculate(string nameKey, List<AttendanceRow> rows, int dailyRate,
        decimal advance, decimal arrears, string category = "", bool isMonthly = false, bool isOtExempt = false)
    {
        int daysWorked = rows.Count(r => !string.IsNullOrEmpty(r.Worked));

        // Monthly: just days x rate, no OT/deduction
        if (isMonthly)
        {
            var mRow = new SalaryRow
            {
                Category = category,
                Name = string.Join(" ", nameKey.Split(' ').Where(w => w.Length > 0).Select(w =>
                    char.ToUpper(w[0]) + w[1..])),
                Days = daysWorked, OtHours = 0, DedHours = 0,
                NetHours = 0, DailyRate = dailyRate,
                HourlyRate = 0,
            };
            mRow.ExtraHrs = 0;
            mRow.Advance = advance;
            mRow.Arrears = arrears;
            return mRow;
        }

        var totalOt = TimeSpan.Zero;
        var totalDed = TimeSpan.Zero;

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.OT) && string.IsNullOrEmpty(row.Deduction)) continue;
            if (!string.IsNullOrEmpty(row.OT)) totalOt += ParseTs(row.OT);
            if (!string.IsNullOrEmpty(row.Deduction)) totalDed += ParseTs(row.Deduction);
        }

        double otH = Math.Round(totalOt.TotalHours, 2);
        double dedH = Math.Round(totalDed.TotalHours, 2);
        // OT-exempt: still track hours for display but NetHours = 0 so pay is unaffected
        double netH = isOtExempt ? 0 : Math.Round(otH - dedH, 2);
        double hourlyRate = dailyRate / 8.0;

        var row2 = new SalaryRow
        {
            Category = category,
            Name = string.Join(" ", nameKey.Split(' ')
                .Where(w => w.Length > 0)
                .Select(w => char.ToUpper(w[0]) + w[1..])),
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
        bool nextDay = outTime.EndsWith("+1");
        var outClean = nextDay ? outTime[..5] : outTime;
        var inDt = TimeSpan.ParseExact(inTime, "hh\\:mm", CultureInfo.InvariantCulture);
        var outDt = TimeSpan.ParseExact(outClean, "hh\\:mm", CultureInfo.InvariantCulture);
        if (nextDay) outDt += TimeSpan.FromHours(24);
        var diff = outDt - inDt;
        if (diff < TimeSpan.Zero) diff += TimeSpan.FromHours(24);
        return diff;
    }

    private static readonly TimeSpan NightLunchThreshold = TimeSpan.FromHours(5);

    private static TimeSpan CalcEffective(TimeSpan presence, string outTime)
    {
        var outClean = outTime.EndsWith("+1") ? outTime[..5] : outTime;
        int outHour = int.Parse(outClean[..2]);
        // Cross-midnight lunch only if shift >= 5h
        if (outTime.EndsWith("+1"))
            return presence >= NightLunchThreshold ? presence - TimeSpan.FromMinutes(LunchBreakMinutes) : presence;
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
        if (string.IsNullOrEmpty(s) || s.Length != 5 || s[2] != ':') return false;
        return int.TryParse(s[..2], out int h) && int.TryParse(s[3..], out int m)
               && h >= 0 && h <= 23 && m >= 0 && m <= 59;
    }
}
