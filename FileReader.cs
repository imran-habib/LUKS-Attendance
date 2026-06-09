#nullable enable
using System;
using System.Collections.Generic;
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
    public static AttendanceData ReadAttendance(string path)
    {
        if (Path.GetExtension(path).Equals(".xls", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "Please save the file as .xlsx first.\nThe attendance software should have an export as .xlsx option.");

        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheets.First();
        var data = new AttendanceData();

        data.Duration = ws.Cell(2, 3).GetString();

        int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 3;
        for (int c = 3; c <= lastCol; c++)
        {
            var val = ws.Cell(3, c).GetString().Replace(".0", "").Trim();
            if (int.TryParse(val, out int dayNum))
            {
                var dayName = ws.Cell(4, c).GetString().Trim();
                data.Days.Add((c, dayNum, dayName));
            }
        }

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
            int lastRow = wsDb.LastRowUsed()?.RowNumber() ?? 1;
            for (int r = 2; r <= lastRow; r++)
            {
                var name = wsDb.Cell(r, 1).GetString().Trim();
                var rate = (int)wsDb.Cell(r, 2).GetDouble();
                var type = wsDb.Cell(r, 3).GetString().Trim().ToLower();
                if (!string.IsNullOrEmpty(name))
                    db.Add(new EmployeeEntry { Name = name, DailyRate = rate, Type = type });
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
}
