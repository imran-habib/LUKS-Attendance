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
    // Punches before 03:30 next day count as previous day's exit
    private const int MidnightCutoffHour = 3;
    private const int MidnightCutoffMin = 30;

    private static bool IsMidnightExit(string time)
    {
        int h = int.Parse(time[..2]);
        int m = int.Parse(time[3..]);
        return h < MidnightCutoffHour || (h == MidnightCutoffHour && m <= MidnightCutoffMin);
    }

    public static (List<PunchRecord> records, List<IssueRow> issues) Parse(AttendanceData data)
    {
        var records = new List<PunchRecord>();
        var issues = new List<IssueRow>();
        string lastDayLabel = data.Days.Count > 0 ? data.Days[^1].DateLabel : "";

        // Track midnight exit punches (before 3:30am) as previous day's OUT
        var prevDayOut = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        for (int d = 1; d < data.Days.Count; d++)
        {
            var day = data.Days[d];
            foreach (var emp in data.Employees)
            {
                if (!emp.Punches.TryGetValue(day.DateLabel, out var raw)) continue;
                var times = TimeRe.Matches(raw);
                if (times.Count == 0) continue;

                string lastMidnight = ""; foreach (Match mt in times) { if (IsMidnightExit(mt.Value)) lastMidnight = mt.Value; else break; } if (!string.IsNullOrEmpty(lastMidnight))
                {
                    var prevLabel = data.Days[d - 1].DateLabel;
                    prevDayOut[emp.Name.ToLower() + "|" + prevLabel] = lastMidnight;
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

                // Filter out midnight exit punches — they belong to previous day
                var validTimes = new List<string>();
                foreach (Match t in times)
                {
                    if (!IsMidnightExit(t.Value))
                        validTimes.Add(t.Value);
                }

                if (validTimes.Count == 0)
                {
                    // Bug #9 fix: if first day and only midnight punches, keep as issue
                    if (data.Days.IndexOf(day) == 0)
                    {
                        var firstTime = times[0].Value;
                        issues.Add(new IssueRow
                        {
                            Name = emp.Name, DayLabel = day.DateLabel,
                            Type = "Unmatched exit (prior period)",
                            InTime = firstTime, OutTime = "?"
                        });
                    }
                    continue;
                }

                // Pick first valid IN (>= 6am preferred) and last as OUT
                string inTime = validTimes[0];
                foreach (var t in validTimes)
                {
                    if (int.Parse(t[..2]) >= 6) { inTime = t; break; }
                }
                string outTime = validTimes[^1];

                // Check if a midnight punch on next day serves as this day's OUT
                string prevOutKey = emp.Name.ToLower() + "|" + day.DateLabel;
                bool hasMidnightExit = prevDayOut.TryGetValue(prevOutKey, out var midnightTime);

                if (validTimes.Count == 1)
                {
                    if (hasMidnightExit)
                    {
                        // Single punch today + exit after midnight = cross-midnight shift
                        // Calculate equivalent OUT by adding 24h to midnight punch
                        // e.g. IN=08:10, exit=01:06 -> OUT expressed as 25:06 for calc
                        // We store as special format "HH:MM+1" meaning next-day time
                        records.Add(new PunchRecord
                        {
                            Name = emp.Name, DayLabel = day.DateLabel,
                            InTime = inTime, OutTime = midnightTime + "+1",
                            Status = "midnight_exit"
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
                    // 2+ valid punches: first IN, last OUT
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
