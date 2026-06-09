using System.Collections.Generic;
using System.Collections.ObjectModel;
using ClosedXML.Excel;

namespace LuksAttendance;

public static class ExcelExporter
{
    public static void Export(string path,
        ObservableCollection<AttendanceRow> attendance,
        ObservableCollection<SalaryRow> salary,
        ObservableCollection<EmployeeEntry> employees)
    {
        using var wb = new XLWorkbook();

        // Attendance sheet
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
            var r = attendance[i];
            wsAtt.Cell(i + 2, 1).Value = r.Name;
            wsAtt.Cell(i + 2, 2).Value = r.Day;
            wsAtt.Cell(i + 2, 3).Value = r.InTime;
            wsAtt.Cell(i + 2, 4).Value = r.OutTime;
            wsAtt.Cell(i + 2, 5).Value = r.Worked;
            wsAtt.Cell(i + 2, 6).Value = r.OT;
            wsAtt.Cell(i + 2, 7).Value = r.Deduction;
            wsAtt.Cell(i + 2, 8).Value = r.Status;
        }
        wsAtt.Row(1).Style.Font.Bold = true;
        wsAtt.Columns().AdjustToContents();

        // Salary sheet
        var wsSal = wb.AddWorksheet("Salary");
        string[] salHeaders = { "Name", "Days", "OT (hrs)", "Deduction (hrs)", "Net Hours",
            "Daily Rate", "Hourly Rate", "Extra Hrs", "Advance", "Arrears", "Net Salary" };
        for (int c = 0; c < salHeaders.Length; c++)
            wsSal.Cell(1, c + 1).Value = salHeaders[c];
        for (int i = 0; i < salary.Count; i++)
        {
            var r = salary[i];
            wsSal.Cell(i + 2, 1).Value = r.Name;
            wsSal.Cell(i + 2, 2).Value = r.Days;
            wsSal.Cell(i + 2, 3).Value = r.OtHours;
            wsSal.Cell(i + 2, 4).Value = r.DedHours;
            wsSal.Cell(i + 2, 5).Value = r.NetHours;
            wsSal.Cell(i + 2, 6).Value = r.DailyRate;
            wsSal.Cell(i + 2, 7).Value = r.HourlyRate;
            wsSal.Cell(i + 2, 8).Value = r.ExtraHrs;
            wsSal.Cell(i + 2, 9).Value = (double)r.Advance;
            wsSal.Cell(i + 2, 10).Value = (double)r.Arrears;
            wsSal.Cell(i + 2, 11).Value = (double)r.NetSalary;
        }
        // Total row
        int tr = salary.Count + 2;
        wsSal.Cell(tr, 1).Value = "TOTAL";
        wsSal.Cell(tr, 11).FormulaA1 = $"=SUM(K2:K{tr - 1})";
        wsSal.Row(1).Style.Font.Bold = true;
        wsSal.Row(tr).Style.Font.Bold = true;
        wsSal.Columns().AdjustToContents();

        // Employee DB sheet
        var wsDb = wb.AddWorksheet("Employee DB");
        wsDb.Cell(1, 1).Value = "Name";
        wsDb.Cell(1, 2).Value = "Daily Rate";
        wsDb.Cell(1, 3).Value = "Type";
        for (int i = 0; i < employees.Count; i++)
        {
            wsDb.Cell(i + 2, 1).Value = employees[i].Name;
            wsDb.Cell(i + 2, 2).Value = employees[i].DailyRate;
            wsDb.Cell(i + 2, 3).Value = employees[i].Type;
        }
        wsDb.Row(1).Style.Font.Bold = true;
        wsDb.Columns().AdjustToContents();

        wb.SaveAs(path);
    }
}
