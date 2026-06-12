#nullable enable
using System.Collections.ObjectModel;
using System.Linq;
using ClosedXML.Excel;

namespace LuksAttendance;

public static class ExcelExporter
{
    public static void Export(string path,
        ObservableCollection<AttendanceRow> attendance,
        ObservableCollection<SalaryRow> salary,
        ObservableCollection<EmployeeEntry> employees,
        string duration = "")
    {
        using var wb = new XLWorkbook();

        // --- Salary Sheet (first, as primary output) ---
        var wsSal = wb.AddWorksheet("Salary");

        // Header with branding
        wsSal.Cell(1, 1).Value = $"LUKS SALARY SHEET — {duration}";
        wsSal.Range(1, 1, 1, 10).Merge().Style.Font.SetBold(true).Font.SetFontSize(14)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        // Column headers in row 3
        string[] salHeaders = { "Category", "Name", "Days", "OT (hrs)", "Deduction (hrs)", "Net Hours",
            "Extra Hrs", "Advance", "Arrears", "Net Salary" };
        for (int c = 0; c < salHeaders.Length; c++)
            wsSal.Cell(3, c + 1).Value = salHeaders[c];
        wsSal.Row(3).Style.Font.Bold = true;

        // Group by category
        var grouped = salary.OrderBy(s => s.Category).ThenBy(s => s.Name).ToList();
        int row = 4;
        string currentCat = "";
        foreach (var r in grouped)
        {
            if (r.Category != currentCat)
            {
                currentCat = r.Category;
                if (row > 4) row++; // blank separator
                wsSal.Cell(row, 1).Value = $"── {currentCat} ──";
                wsSal.Cell(row, 1).Style.Font.Bold = true;
                wsSal.Cell(row, 1).Style.Font.FontColor = XLColor.DarkBlue;
                row++;
            }
            wsSal.Cell(row, 1).Value = r.Category;
            wsSal.Cell(row, 2).Value = r.Name;
            wsSal.Cell(row, 3).Value = r.Days;
            wsSal.Cell(row, 4).Value = r.OtHours;
            wsSal.Cell(row, 5).Value = r.DedHours;
            wsSal.Cell(row, 6).Value = r.NetHours;
            wsSal.Cell(row, 7).Value = r.ExtraHrs;
            wsSal.Cell(row, 8).Value = (double)r.Advance;
            wsSal.Cell(row, 9).Value = (double)r.Arrears;
            wsSal.Cell(row, 10).Value = (double)r.NetSalary;
            row++;
        }

        // Total row
        row++;
        wsSal.Cell(row, 1).Value = "TOTAL";
        wsSal.Cell(row, 10).FormulaA1 = $"=SUM(J4:J{row - 1})";
        wsSal.Row(row).Style.Font.Bold = true;
        wsSal.Columns().AdjustToContents();

        // --- Attendance Sheet ---
        var wsAtt = wb.AddWorksheet("Attendance");
        wsAtt.Cell(1, 1).Value = "Name";
        wsAtt.Cell(1, 2).Value = "Day";
        wsAtt.Cell(1, 3).Value = "IN";
        wsAtt.Cell(1, 4).Value = "OUT";
        wsAtt.Cell(1, 5).Value = "Worked";
        wsAtt.Cell(1, 6).Value = "OT";
        wsAtt.Cell(1, 7).Value = "Deduction";
        wsAtt.Cell(1, 8).Value = "Status";
        for (int i = 0; i < attendance.Count; i++)
        {
            var a = attendance[i];
            wsAtt.Cell(i + 2, 1).Value = a.Name;
            wsAtt.Cell(i + 2, 2).Value = a.Day;
            wsAtt.Cell(i + 2, 3).Value = a.InTime;
            wsAtt.Cell(i + 2, 4).Value = a.OutTime;
            wsAtt.Cell(i + 2, 5).Value = a.Worked;
            wsAtt.Cell(i + 2, 6).Value = a.OT;
            wsAtt.Cell(i + 2, 7).Value = a.Deduction;
            wsAtt.Cell(i + 2, 8).Value = a.Status;
        }
        wsAtt.Row(1).Style.Font.Bold = true;
        wsAtt.Columns().AdjustToContents();

        // --- Employee DB Sheet ---
        var wsDb = wb.AddWorksheet("Employee DB");
        wsDb.Cell(1, 1).Value = "Name";
        wsDb.Cell(1, 2).Value = "Daily Rate";
        wsDb.Cell(1, 3).Value = "Type";
        wsDb.Cell(1, 4).Value = "Category";
        for (int i = 0; i < employees.Count; i++)
        {
            wsDb.Cell(i + 2, 1).Value = employees[i].Name;
            wsDb.Cell(i + 2, 2).Value = employees[i].DailyRate;
            wsDb.Cell(i + 2, 3).Value = employees[i].Type;
            wsDb.Cell(i + 2, 4).Value = employees[i].Category;
        }
        wsDb.Row(1).Style.Font.Bold = true;
        wsDb.Columns().AdjustToContents();

        wb.SaveAs(path);
    }
}
