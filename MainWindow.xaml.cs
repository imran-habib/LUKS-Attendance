#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace LuksAttendance;

public partial class MainWindow : Window
{
    private AttendanceData? _data;
    private ObservableCollection<AttendanceRow> _attendanceRows = new();
    private ObservableCollection<SalaryRow> _salaryRows = new();
    private ObservableCollection<EmployeeEntry> _employeeDb = new();
    private ObservableCollection<IssueRow> _issueRows = new();
    private string? _previousFilePath;

    public MainWindow()
    {
        InitializeComponent();
        AttendanceGrid.ItemsSource = _attendanceRows;
        SalaryGrid.ItemsSource = _salaryRows;
        EmployeeGrid.ItemsSource = _employeeDb;
        IssuesGrid.ItemsSource = _issueRows;
        LoadDefaultEmployeeDb();
    }

    private void BtnLoad_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Excel Files|*.xls;*.xlsx",
            Title = "Select Attendance File"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _data = FileReader.ReadAttendance(dlg.FileName);
            var (records, issues) = PunchParser.Parse(_data);

            _attendanceRows.Clear();
            _issueRows.Clear();

            foreach (var issue in issues)
                _issueRows.Add(issue);

            foreach (var rec in records)
                _attendanceRows.Add(SalaryCalc.BuildAttendanceRow(rec));

            // Always calculate salary with available data
            CalculateSalary();

            if (_issueRows.Count > 0)
            {
                IssuesTab.IsSelected = true;
                StatusText.Text = $"Loaded {_data.Employees.Count} employees. {_issueRows.Count} issues to resolve. Salary calculated with available data.";
            }
            else
            {
                StatusText.Text = $"Loaded {_data.Employees.Count} employees. {_attendanceRows.Count} records. Salary ready.";
            }

            BtnExportExcel.IsEnabled = true;
            BtnExportPdf.IsEnabled = true;
            BtnPrint.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnPrevious_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            Title = "Select Previous Salary Sheet (for carry-over)"
        };
        if (dlg.ShowDialog() != true) return;
        _previousFilePath = dlg.FileName;
        var (db, carryOver) = FileReader.LoadPreviousOutput(_previousFilePath);
        if (db.Count > 0)
        {
            _employeeDb.Clear();
            foreach (var entry in db) _employeeDb.Add(entry);
        }
        StatusText.Text = $"Loaded previous data: {db.Count} employees.";
    }

    private void BtnResolve_Click(object sender, RoutedEventArgs e)
    {
        if (IssuesGrid.SelectedItem is not IssueRow issue) return;
        var time = TxtResolveTime.Text.Trim();
        if (string.IsNullOrEmpty(time)) time = "17:00";

        if (!TimeHelper.IsValidTime(time))
        {
            MessageBox.Show("Invalid time format. Use HH:MM", "Error");
            return;
        }

        var rec = new PunchRecord
        {
            Name = issue.Name, Day = issue.Day,
            InTime = issue.InTime, OutTime = time,
            Status = "hr_entered"
        };
        _attendanceRows.Add(SalaryCalc.BuildAttendanceRow(rec));
        _issueRows.Remove(issue);
        CalculateSalary();
        StatusText.Text = $"Resolved. {_issueRows.Count} issues remaining.";
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        if (IssuesGrid.SelectedItem is not IssueRow issue) return;
        _issueRows.Remove(issue);
        CalculateSalary();
        StatusText.Text = $"Skipped. {_issueRows.Count} issues remaining.";
    }

    private void CalculateSalary()
    {
        _salaryRows.Clear();
        var dbDict = _employeeDb.ToDictionary(e => e.Name.ToLower(), e => e);
        var grouped = _attendanceRows.GroupBy(r => r.Name.ToLower());

        foreach (var grp in grouped.OrderBy(g => g.Key))
        {
            if (!dbDict.TryGetValue(grp.Key, out var entry)) continue;
            if (entry.Type == "excluded") continue;
            if (entry.Type == "monthly") continue;

            var sal = SalaryCalc.Calculate(grp.Key, grp.ToList(), entry.DailyRate, 0, 0);
            _salaryRows.Add(sal);
        }
    }

    private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "Salary_Sheet.xlsx" };
        if (dlg.ShowDialog() != true) return;
        ExcelExporter.Export(dlg.FileName, _attendanceRows, _salaryRows, _employeeDb);
        StatusText.Text = $"Excel exported: {dlg.FileName}";
    }

    private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = "Salary_Sheet.pdf" };
        if (dlg.ShowDialog() != true) return;
        PdfExporter.Export(dlg.FileName, _attendanceRows, _salaryRows);
        StatusText.Text = $"PDF exported: {dlg.FileName}";
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        PdfExporter.Print(_salaryRows);
    }

    private void LoadDefaultEmployeeDb()
    {
        foreach (var (name, rate, type) in DefaultData.Employees)
            _employeeDb.Add(new EmployeeEntry { Name = name, DailyRate = rate, Type = type });
    }
}
