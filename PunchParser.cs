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

public class IssueRow
{
    public string Name { get; set; } = "";
    public string DayLabel { get; set; } = "";
    public string Type { get; set; } = "";
    public string InTime { get; set; } = "";
    public string OutTime { get; set; } = ""; // was "Raw", now shows expected OUT time needed
}

public static class PunchParser
{
    private static readonly Regex TimeRe = new(@"\d{2}:\d{2}");

    public static (List<PunchRecord> records, List<IssueRow> issues) Parse(AttendanceData data)
    {
        var records = new List<PunchRecord>();
        var issues = new List<IssueRow>();
        string lastDayLabel = data.Days.Count > 0 ? data.Days[^1].DateLabel : "";

        foreach (var emp in data.Employees)
        {
            foreach (var day in data.Days)
            {
                if (!emp.Punches.TryGetValue(day.DateLabel, out var raw)) continue;
                var times = TimeRe.Matches(raw);
                if (times.Count == 0) continue;

                bool isLastDay = day.DateLabel == lastDayLabel;

                if (times.Count == 1)
                {
                    issues.Add(new IssueRow
                    {
                        Name = emp.Name, DayLabel = day.DateLabel,
                        Type = isLastDay ? "Last Day OUT" : "Missing OUT",
                        InTime = times[0].Value,
                        OutTime = "?" // needs HR input
                    });
                }
                else if (times.Count == 2)
                {
                    records.Add(new PunchRecord
                    {
                        Name = emp.Name, DayLabel = day.DateLabel,
                        InTime = times[0].Value, OutTime = times[1].Value
                    });
                }
                else
                {
                    string inTime = times[0].Value;
                    for (int i = 0; i < times.Count; i++)
                    {
                        int h = int.Parse(times[i].Value[..2]);
                        if (h >= 6) { inTime = times[i].Value; break; }
                    }
                    string outTime = times[^1].Value;

                    records.Add(new PunchRecord
                    {
                        Name = emp.Name, DayLabel = day.DateLabel,
                        InTime = inTime, OutTime = outTime,
                        Status = "multi_punch_verify"
                    });
                    issues.Add(new IssueRow
                    {
                        Name = emp.Name, DayLabel = day.DateLabel,
                        Type = "Multi-Punch (verify)",
                        InTime = inTime,
                        OutTime = outTime + " (auto)"
                    });
                }
            }
        }
        return (records, issues);
    }
}
