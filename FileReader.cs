#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ExcelDataReader;

namespace LuksAttendance;

public class AttendanceData
{
    public string Duration { get; set; } = "";
    public List<DayInfo> Days { get; set; } = new();
    public List<EmployeeData> Employees { get; set; } = new();
}

public class DayInfo
{
    public int Col { get; set; }
    public int DayNum { get; set; }
    public string DayName { get; set; } = "";
    public string DateLabel { get; set; } = ""; // e.g. "23-Jan (Fr)"
}

public class EmployeeData
{
    public string No { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, string> Punches { get; set; } = new(); // key = DateLabel
}

public static class FileReader
{
    private static readonly Regex DurationRe = new(@"(\d{4})/(\d{2})/(\d{2})\s*~\s*(\d{2})/(\d{2})");

    public static AttendanceData ReadAttendance(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        if (ext == ".xls")
            return ReadXls(path);
        return ReadXlsx(path);
    }

    /// <summary>Merge multiple attendance files into one dataset.</summary>
    public static AttendanceData ReadMultiple(string[] paths)
    {
        var merged = new AttendanceData();
        var empDict = new Dictionary<string, EmployeeData>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var data = ReadAttendance(path);
            if (string.IsNullOrEmpty(merged.Duration))
                merged.Duration = data.Duration;
            else
                merged.Duration += " + " + data.Duration;

            // Merge days (avoid duplicates by DateLabel)
            var existingLabels = new HashSet<string>(merged.Days.Select(d => d.DateLabel));
            foreach (var day in data.Days)
            {
                if (!existingLabels.Contains(day.DateLabel))
                {
                    merged.Days.Add(day);
                    existingLabels.Add(day.DateLabel);
                }
            }

            // Merge employees
            foreach (var emp in data.Employees)
            {
                if (!empDict.TryGetValue(emp.Name, out var existing))
                {
                    existing = new EmployeeData { No = emp.No, Name = emp.Name };
                    empDict[emp.Name] = existing;
                }
                foreach (var (label, punch) in emp.Punches)
                {
                    existing.Punches[label] = punch; // later file overwrites if same date
                }
            }
        }

        merged.Employees = empDict.Values.ToList();
        // Sort days by actual date order
        merged.Days = merged.Days.OrderBy(d => d.DayNum).ToList();
        return merged;
    }

    private static List<DayInfo> BuildDayInfos(string duration, List<(int col, int dayNum, string dayName)> rawDays)
    {
        var result = new List<DayInfo>();
        // Parse duration like "2026/01/23 ~ 01/29"
        var match = DurationRe.Match(duration);
        int year = 0, month = 0;
        if (match.Success)
        {
            year = int.Parse(match.Groups[1].Value);
            month = int.Parse(match.Groups[2].Value);
        }

        foreach (var (col, dayNum, dayName) in rawDays)
        {
            string label;
            if (year > 0)
            {
                // Determine month: if dayNum < first day in list, it's next month
                int firstDay = rawDays[0].dayNum;
                int actualMonth = dayNum >= firstDay ? month : (month % 12) + 1;
                int actualYear = dayNum >= firstDay ? year : (actualMonth == 1 ? year + 1 : year);
                try
                {
                    var dt = new DateTime(actualYear, actualMonth, dayNum);
                    label = $"{dayNum:D2}-{dt:MMM} ({dayName})";
                }
                catch
                {
                    label = $"{dayNum} ({dayName})";
                }
            }
            else
            {
                label = $"{dayNum} ({dayName})";
            }
            result.Add(new DayInfo { Col = col, DayNum = dayNum, DayName = dayName, DateLabel = label });
        }
        return result;
    }

    private static AttendanceData ReadXls(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var ds = reader.AsDataSet();
        var table = ds.Tables[0];

        var data = new AttendanceData();
        if (table.Rows.Count > 1)
            data.Duration = table.Rows[1][2]?.ToString() ?? "";

        // Raw days from row 2 (index 2)
        var rawDays = new List<(int col, int dayNum, string dayName)>();
        if (table.Rows.Count > 2)
        {
            var dayRow = table.Rows[2];
            for (int c = 2; c < table.Columns.Count; c++)
            {
                var val = dayRow[c]?.ToString()?.Replace(".0", "").Trim() ?? "";
                if (int.TryParse(val, out int dayNum))
                {
                    var dayName = table.Rows.Count > 3 ? table.Rows[3][c]?.ToString()?.Trim() ?? "" : "";
                    rawDays.Add((c, dayNum, dayName));
                }
            }
        }

        data.Days = BuildDayInfos(data.Duration, rawDays);

        for (int r = 4; r < table.Rows.Count; r++)
        {
            var name = table.Rows[r][1]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(name)) continue;

            var emp = new EmployeeData
            {
                No = table.Rows[r][0]?.ToString()?.Replace(".0", "").Trim() ?? "",
                Name = name
            };

            foreach (var day in data.Days)
            {
                var cell = table.Rows[r][day.Col]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(cell))
                    emp.Punches[day.DateLabel] = cell;
            }
            data.Employees.Add(emp);
        }

        return data;
    }

    private static AttendanceData ReadXlsx(string path)
    {
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        var data = new AttendanceData();

        data.Duration = ws.Cell(2, 3).GetString();

        var rawDays = new List<(int col, int dayNum, string dayName)>();
        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 3;
        for (int c = 3; c <= lastCol; c++)
        {
            var val = ws.Cell(3, c).GetString().Replace(".0", "").Trim();
            if (int.TryParse(val, out int dayNum))
            {
                var dayName = ws.Cell(4, c).GetString().Trim();
                rawDays.Add((c, dayNum, dayName));
            }
        }

        data.Days = BuildDayInfos(data.Duration, rawDays);

        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 5;
        for (int r = 5; r <= lastRow; r++)
        {
            var name = ws.Cell(r, 2).GetString().Trim();
            if (string.IsNullOrEmpty(name)) continue;

            var emp = new EmployeeData
            {
                No = ws.Cell(r, 1).GetString().Replace(".0", "").Trim(),
                Name = name
            };

            foreach (var day in data.Days)
            {
                var cell = ws.Cell(r, day.Col).GetString().Trim();
                if (!string.IsNullOrEmpty(cell))
                    emp.Punches[day.DateLabel] = cell;
            }
            data.Employees.Add(emp);
        }

        return data;
    }

    public static (List<EmployeeEntry> db, Dictionary<string, (decimal advance, decimal arrears)> carryOver)
        LoadPreviousOutput(string path)
    {
        var db = new List<EmployeeEntry>();
        var carryOver = new Dictionary<string, (decimal, decimal)>();
        if (!File.Exists(path)) return (db, carryOver);

        using var wb = new XLWorkbook(path);

        if (wb.Worksheets.TryGetWorksheet("Employee DB", out var wsDb))
        {
            int lastRow = wsDb.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var name = wsDb.Cell(r, 1).GetString().Trim();
                var rate = (int)wsDb.Cell(r, 2).GetDouble();
                var type = wsDb.Cell(r, 3).GetString().Trim().ToLower();
                var category = wsDb.Cell(r, 4).GetString().Trim();
                if (!string.IsNullOrEmpty(name))
                    db.Add(new EmployeeEntry { Name = name, DailyRate = rate, Type = type, Category = category });
            }
        }

        if (wb.Worksheets.TryGetWorksheet("Salary", out var wsSal))
        {
            int lastRow = wsSal.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var name = wsSal.Cell(r, 1).GetString().Trim().ToLower();
                if (string.IsNullOrEmpty(name) || name == "total") continue;
                var adv = (decimal)wsSal.Cell(r, 9).GetDouble();
                var arr = (decimal)wsSal.Cell(r, 10).GetDouble();
                carryOver[name] = (adv, arr);
            }
        }

        return (db, carryOver);
    }

    public static (List<SalaryRow> rows, string weekRange) ReadSalaryExport(string path)
    {
        var rows = new List<SalaryRow>();
        string weekRange = "";

        using var wb = new XLWorkbook(path);
        if (!wb.Worksheets.TryGetWorksheet("Salary", out var ws))
            return (rows, weekRange);

        var header = ws.Cell(1, 1).GetString();
        var dashIdx = header.IndexOf('\u2014');
        if (dashIdx > 0)
            weekRange = header[(dashIdx + 1)..].Trim();

        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 3;
        for (int r = 4; r <= lastRow; r++)
        {
            var name = ws.Cell(r, 2).GetString().Trim();
            if (string.IsNullOrEmpty(name) || name == "TOTAL") continue;
            if (name.StartsWith("\u2500\u2500")) continue;

            var cat = ws.Cell(r, 1).GetString().Trim();
            if (cat.StartsWith("\u2500\u2500")) cat = "";

            int days = (int)ws.Cell(r, 3).GetDouble();
            if (days == 0 && string.IsNullOrEmpty(ws.Cell(r, 3).GetString().Trim())) continue;

            rows.Add(new SalaryRow
            {
                Category = cat,
                Name = name,
                Days = days,
                OtHours = ws.Cell(r, 4).GetDouble(),
                DedHours = ws.Cell(r, 5).GetDouble(),
                NetHours = ws.Cell(r, 6).GetDouble(),
                DailyRate = (int)ws.Cell(r, 7).GetDouble(),
                HourlyRate = ws.Cell(r, 8).GetDouble(),
                ExtraHrs = ws.Cell(r, 9).GetDouble(),
                Advance = (decimal)ws.Cell(r, 10).GetDouble(),
                Arrears = (decimal)ws.Cell(r, 11).GetDouble(),
                NetSalary = (decimal)ws.Cell(r, 12).GetDouble()
            });
        }

        return (rows, weekRange);
    }
}
