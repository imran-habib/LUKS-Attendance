using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LuksAttendance;

public class PunchRecord
{
    public string Name { get; set; } = "";
    public int Day { get; set; }
    public string InTime { get; set; } = "";
    public string OutTime { get; set; } = "";
    public string Status { get; set; } = "ok";
}

public class IssueRow
{
    public string Name { get; set; } = "";
    public int Day { get; set; }
    public string Type { get; set; } = "";
    public string InTime { get; set; } = "";
    public string Raw { get; set; } = "";
}

public static class PunchParser
{
    private static readonly Regex TimeRe = new(@"\d{2}:\d{2}");

    public static (List<PunchRecord> records, List<IssueRow> issues) Parse(AttendanceData data)
    {
        var records = new List<PunchRecord>();
        var issues = new List<IssueRow>();
        int lastDay = data.Days.Count > 0 ? data.Days[^1].DayNum : 0;

        foreach (var emp in data.Employees)
        {
            foreach (var (_, dayNum, _) in data.Days)
            {
                if (!emp.Punches.TryGetValue(dayNum, out var raw)) continue;
                var times = TimeRe.Matches(raw);
                if (times.Count == 0) continue;

                bool isLastDay = dayNum == lastDay;

                if (times.Count == 1)
                {
                    var inTime = times[0].Value;
                    issues.Add(new IssueRow
                    {
                        Name = emp.Name, Day = dayNum,
                        Type = isLastDay ? "Last Day OUT" : "Missing OUT",
                        InTime = inTime, Raw = raw.Replace("\n", " ").Trim()
                    });
                }
                else if (times.Count == 2)
                {
                    records.Add(new PunchRecord
                    {
                        Name = emp.Name, Day = dayNum,
                        InTime = times[0].Value, OutTime = times[1].Value
                    });
                }
                else
                {
                    // 3+ punches: first >= 06:00 is IN, last is OUT
                    string inTime = times[0].Value;
                    for (int i = 0; i < times.Count; i++)
                    {
                        int h = int.Parse(times[i].Value[..2]);
                        if (h >= 6) { inTime = times[i].Value; break; }
                    }
                    string outTime = times[^1].Value;

                    records.Add(new PunchRecord
                    {
                        Name = emp.Name, Day = dayNum,
                        InTime = inTime, OutTime = outTime,
                        Status = "multi_punch_verify"
                    });
                    issues.Add(new IssueRow
                    {
                        Name = emp.Name, Day = dayNum,
                        Type = "Multi-Punch (verify)",
                        InTime = inTime, Raw = raw.Replace("\n", " ").Trim()
                    });
                }
            }
        }
        return (records, issues);
    }
}
