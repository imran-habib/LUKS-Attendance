#nullable enable
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LuksAttendance;

public class PunchRecord
{
    public string Name { get; set; } = "";
    public string DayLabel { get; set; } = "";
    public string InTime { get; set; } = "";
    public string OutTime { get; set; } = "";
    public string Status { get; set; } = "ok";
}

public class IssueRow : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string Name { get; set; } = "";
    public string DayLabel { get; set; } = "";
    public string Type { get; set; } = "";
    public string InTime { get; set; } = "";
    private string _outTime = "";
    public string OutTime
    {
        get => _outTime;
        set { _outTime = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(OutTime))); }
    }
}

public static class PunchParser
{
    private static readonly Regex TimeRe = new(@"\d{2}:\d{2}");
    private const int MidnightCutoffHour = 5;

    public static (List<PunchRecord> records, List<IssueRow> issues) Parse(AttendanceData data)
    {
        var records = new List<PunchRecord>();
        var issues = new List<IssueRow>();
        string lastDayLabel = data.Days.Count > 0 ? data.Days[^1].DateLabel : "";

        // Track midnight punches (before 5am) as previous day's OUT
        var prevDayOut = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        for (int d = 1; d < data.Days.Count; d++)
        {
            var day = data.Days[d];
            foreach (var emp in data.Employees)
            {
                if (!emp.Punches.TryGetValue(day.DateLabel, out var raw)) continue;
                var times = TimeRe.Matches(raw);
                if (times.Count == 0) continue;

                int firstHour = int.Parse(times[0].Value[..2]);
                if (firstHour < MidnightCutoffHour)
                {
                    var prevLabel = data.Days[d - 1].DateLabel;
                    prevDayOut[emp.Name.ToLower() + "|" + prevLabel] = times[0].Value;
                }
            }
        }

        // Main pass
        foreach (var emp in data.Employees)
        {
            foreach (var day in data.Days)
            {
                if (!emp.Punches.TryGetValue(day.DateLabel, out var raw)) continue;
                var times = TimeRe.Matches(raw);
                if (times.Count == 0) continue;

                bool isLastDay = day.DateLabel == lastDayLabel;

                // Filter out midnight punches (< 5am) — they belong to previous day
                var validTimes = new List<string>();
                foreach (Match t in times)
                {
                    int h = int.Parse(t.Value[..2]);
                    if (h >= MidnightCutoffHour)
                        validTimes.Add(t.Value);
                }

                if (validTimes.Count == 0)
                    continue;

                // Pick first valid IN (>= 6am preferred) and last as OUT
                string inTime = validTimes[0];
                foreach (var t in validTimes)
                {
                    if (int.Parse(t[..2]) >= 6) { inTime = t; break; }
                }
                string outTime = validTimes[^1];

                // Check if a midnight punch on next day serves as this day's OUT
                string prevOutKey = emp.Name.ToLower() + "|" + day.DateLabel;
                bool hasMidnightExit = prevDayOut.ContainsKey(prevOutKey);

                if (validTimes.Count == 1)
                {
                    if (hasMidnightExit)
                    {
                        // Single punch today but employee exited after midnight — count as full day
                        records.Add(new PunchRecord
                        {
                            Name = emp.Name, DayLabel = day.DateLabel,
                            InTime = inTime, OutTime = "23:59"
                        });
                    }
                    else
                    {
                        issues.Add(new IssueRow
                        {
                            Name = emp.Name, DayLabel = day.DateLabel,
                            Type = isLastDay ? "Last Day OUT" : "Missing OUT",
                            InTime = inTime,
                            OutTime = "?"
                        });
                    }
                }
                else
                {
                    // 2+ valid punches: first IN, last OUT — no duplicate issue
                    records.Add(new PunchRecord
                    {
                        Name = emp.Name, DayLabel = day.DateLabel,
                        InTime = inTime, OutTime = outTime
                    });
                }
            }
        }
        return (records, issues);
    }
}
