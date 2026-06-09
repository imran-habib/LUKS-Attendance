using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace LuksAttendance;

public class AttendanceData
{
    public string Duration { get; set; } = "";
    public List<(int Col, int DayNum, string DayName)> Days { get; set; } = new();
    public List<EmployeeData> Employees { get; set; } = new();
}

public class EmployeeData
{
    public string No { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<int, string> Punches { get; set; } = new();
}

public static class FileReader
{
    private static readonly Regex TimeRe = new(@"\d{2}:\d{2}");

    public static AttendanceData ReadAttendance(string path)
    {
        // Convert .xls to .xlsx if needed
        if (Path.GetExtension(path).Equals(".xls", StringComparison.OrdinalIgnoreCase))
            path = ConvertXls(path);

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        var data = new AttendanceData();

        // Row 2: duration
        data.Duration = ws.Cell(2, 3).GetString();

        // Row 3: day numbers, Row 4: day names
        for (int c = 3; c <= ws.LastColumnUsed()?.ColumnNumber() ?? 3; c++)
        {
            var val = ws.Cell(3, c).GetString().Replace(".0", "").Trim();
            if (int.TryParse(val, out int dayNum))
            {
                var dayName = ws.Cell(4, c).GetString().Trim();
                data.Days.Add((c, dayNum, dayName));
            }
        }

        // Row 5+: employees
        for (int r = 5; r <= ws.LastRowUsed()?.RowNumber() ?? 5; r++)
        {
            var name = ws.Cell(r, 2).GetString().Trim();
            if (string.IsNullOrEmpty(name)) continue;

            var emp = new EmployeeData
            {
                No = ws.Cell(r, 1).GetString().Replace(".0", "").Trim(),
                Name = name
            };

            foreach (var (col, dayNum, _) in data.Days)
            {
                var cell = ws.Cell(r, col).GetString().Trim();
                if (!string.IsNullOrEmpty(cell))
                    emp.Punches[dayNum] = cell;
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
            for (int r = 2; r <= wsDb.LastRowUsed()?.RowNumber(); r++)
            {
                var name = wsDb.Cell(r, 1).GetString().Trim();
                var rate = (int)(wsDb.Cell(r, 2).GetDouble());
                var type = wsDb.Cell(r, 3).GetString().Trim().ToLower();
                if (!string.IsNullOrEmpty(name))
                    db.Add(new EmployeeEntry { Name = name, DailyRate = rate, Type = type });
            }
        }

        if (wb.Worksheets.TryGetWorksheet("Salary", out var wsSal))
        {
            for (int r = 2; r <= wsSal.LastRowUsed()?.RowNumber(); r++)
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

    private static string ConvertXls(string path)
    {
        // For .xls files, use NPOI or just read as-is with ClosedXML won't work
        // Workaround: copy to .xlsx (ClosedXML only supports .xlsx)
        // User should save as .xlsx, or we use ExcelDataReader
        throw new NotSupportedException(
            "Please save the file as .xlsx first, or use the .xlsx version.\n" +
            "The attendance software should have an option to export as .xlsx.");
    }
}
