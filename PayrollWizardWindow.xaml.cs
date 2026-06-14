#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace LuksAttendance;

public partial class PayrollWizardWindow : Window
{
    private int _step = 0;
    private AttendanceData? _data;
    private ObservableCollection<AttendanceRow> _attRows = new();
    private ObservableCollection<IssueRow> _issues = new();
    private ObservableCollection<SalaryRow> _salaryRows = new();
    private ObservableCollection<EmployeeEntry> _employeeDb;
    private readonly TextBlock[] _stepLabels;

    public ObservableCollection<SalaryRow> ResultSalaryRows => _salaryRows;
    public ObservableCollection<AttendanceRow> ResultAttendanceRows => _attRows;
    public bool Completed { get; private set; }

    public PayrollWizardWindow(ObservableCollection<EmployeeEntry> employeeDb)
    {
        InitializeComponent();
        _employeeDb = employeeDb;
        _stepLabels = new[] { Step1Label, Step2Label, Step3Label, Step4Label };
        WizIssuesGrid.ItemsSource = _issues;
        WizSalaryGrid.ItemsSource = _salaryRows;

        DpWizStart.SelectedDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
        DpWizEnd.SelectedDate = DpWizStart.SelectedDate?.AddDays(6);

        UpdateStep();
    }

    private void UpdateStep()
    {
        StepTabs.SelectedIndex = _step;
        BtnWizBack.IsEnabled = _step > 0;
        BtnWizNext.Content = _step == 3 ? "✔ Finish" : "Next →";

        string[] titles = { "Step 1 of 4: Select Period", "Step 2 of 4: Import Attendance", "Step 3 of 4: Resolve Issues", "Step 4 of 4: Review & Export" };
        TxtStepTitle.Text = titles[_step];

        for (int i = 0; i < _stepLabels.Length; i++)
            _stepLabels[i].Foreground = i == _step ? Brushes.Yellow : i < _step ? Brushes.LightGreen : new SolidColorBrush(Color.FromRgb(0xAA, 0xB8, 0xD2));

        if (_step == 2) TxtWizIssueCount.Text = $"{_issues.Count} issues";
        if (_step == 3) ComputeSalary();
    }

    private void BtnWizNext_Click(object sender, RoutedEventArgs e)
    {
        if (_step == 0)
        {
            if (DpWizStart.SelectedDate == null || DpWizEnd.SelectedDate == null)
            {
                MessageBox.Show("Please select start and end dates.", "Missing Dates");
                return;
            }
        }
        if (_step == 1 && _attRows.Count == 0)
        {
            MessageBox.Show("Please import attendance data before proceeding.", "No Data");
            return;
        }
        if (_step == 3)
        {
            Completed = true;
            DialogResult = true;
            Close();
            return;
        }
        _step++;
        UpdateStep();
    }

    private void BtnWizBack_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 0) _step--;
        UpdateStep();
    }

    private void BtnWizCancel_Click(object sender, RoutedEventArgs e) => Close();

    // Step 2: Import
    private void BtnWizLoadFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Excel Files|*.xls;*.xlsx",
            Title = "Select Attendance File(s)",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _data = dlg.FileNames.Length == 1
                ? FileReader.ReadAttendance(dlg.FileNames[0])
                : FileReader.ReadMultiple(dlg.FileNames);
            ProcessImport();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Import Failed");
        }
    }

    private void BtnWizLoadDevice_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Use the Device tab in the main window to import from device first,\nthen use 'Load from Excel' with the exported file.",
            "Device Import", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ProcessImport()
    {
        if (_data == null) return;
        var (records, issues) = PunchParser.Parse(_data);

        _attRows.Clear();
        _issues.Clear();

        var empTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var emp in _employeeDb)
            if (!string.IsNullOrWhiteSpace(emp.Name))
                empTypes[emp.Name.ToLower()] = emp.Type;

        var isMonthly = RbMonthly.IsChecked == true;

        foreach (var issue in issues)
        {
            if (empTypes.TryGetValue(issue.Name.ToLower(), out var iType) && iType == "excluded") continue;
            if (isMonthly && empTypes.TryGetValue(issue.Name.ToLower(), out var mType) && mType == "monthly")
            {
                var pRec = new PunchRecord { Name = issue.Name, DayLabel = issue.DayLabel, InTime = issue.InTime, OutTime = issue.InTime, Status = "present" };
                _attRows.Add(SalaryCalc.BuildAttendanceRow(pRec));
                continue;
            }
            if (!isMonthly && empTypes.TryGetValue(issue.Name.ToLower(), out var wType) && wType != "weekly") continue;
            if (isMonthly && empTypes.TryGetValue(issue.Name.ToLower(), out var wType2) && wType2 != "monthly") continue;
            _issues.Add(issue);
        }

        foreach (var rec in records)
        {
            if (empTypes.TryGetValue(rec.Name.ToLower(), out var rType) && rType == "excluded") continue;
            if (!isMonthly && empTypes.TryGetValue(rec.Name.ToLower(), out var wt) && wt != "weekly") continue;
            if (isMonthly && empTypes.TryGetValue(rec.Name.ToLower(), out var mt) && mt != "monthly") continue;
            _attRows.Add(SalaryCalc.BuildAttendanceRow(rec));
        }

        int employees = _attRows.Select(r => r.Name).Distinct().Count();
        int days = _attRows.Select(r => r.Day).Distinct().Count();
        TxtWizSummary.Text = $"✅ Loaded successfully!\n\n• Employees: {employees}\n• Days: {days}\n• Missing punches: {_issues.Count}\n• Attendance records: {_attRows.Count}";
        TxtWizSummary.Foreground = System.Windows.Media.Brushes.Black;
    }

    // Step 3: Issues
    private void BtnWizAutoFill_Click(object sender, RoutedEventArgs e)
    {
        int filled = 0;
        foreach (var issue in _issues)
        {
            if (string.IsNullOrEmpty(issue.InTime) || !TimeHelper.IsValidTime(issue.InTime)) continue;
            var parts = issue.InTime.Split(':');
            int h = int.Parse(parts[0]) + 9;
            int m = int.Parse(parts[1]);
            if (h >= 24) h -= 24;
            issue.OutTime = $"{h:D2}:{m:D2}";
            filled++;
        }
        TxtWizIssueCount.Text = $"Auto-filled {filled} OUT times. Edit if needed, then Resolve All.";
    }

    private void BtnWizResolveAll_Click(object sender, RoutedEventArgs e)
    {
        var toResolve = _issues.ToList();
        int resolved = 0;
        foreach (var issue in toResolve)
        {
            string outTime = issue.OutTime?.Trim() ?? "";
            if (!TimeHelper.IsValidTime(outTime)) continue;
            var rec = new PunchRecord { Name = issue.Name, DayLabel = issue.DayLabel, InTime = issue.InTime, OutTime = outTime, Status = "resolved" };
            _attRows.Add(SalaryCalc.BuildAttendanceRow(rec));
            _issues.Remove(issue);
            resolved++;
        }
        TxtWizIssueCount.Text = $"Resolved {resolved}. {_issues.Count} remaining.";
    }

    // Step 4: Salary
    private void ComputeSalary()
    {
        _salaryRows.Clear();
        var dbDict = new Dictionary<string, EmployeeEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var emp in _employeeDb)
        {
            var key = emp.Name.ToLower();
            if (!string.IsNullOrWhiteSpace(key) && !dbDict.ContainsKey(key))
                dbDict[key] = emp;
        }

        var mode = RbMonthly.IsChecked == true ? "monthly" : "weekly";
        var grouped = _attRows.Where(r => !string.IsNullOrWhiteSpace(r.Name)).GroupBy(r => r.Name.ToLower());
        foreach (var grp in grouped.OrderBy(g => g.Key))
        {
            if (!dbDict.TryGetValue(grp.Key, out var entry)) continue;
            if (entry.Type == "excluded") continue;
            if (entry.Type != mode) continue;
            var sal = SalaryCalc.Calculate(grp.Key, grp.ToList(), entry.DailyRate, 0, 0, entry.Category);
            _salaryRows.Add(sal);
        }

        // Traffic lights
        try
        {
            var averages = DatabaseService.GetEmployeeAverages(4);
            foreach (var row in _salaryRows)
            {
                if (averages.TryGetValue(row.Name, out double avg) && avg > 0)
                {
                    double diff = Math.Abs((double)row.NetSalary - avg) / avg;
                    row.StatusIndicator = diff <= 0.15 ? "🟢" : diff <= 0.30 ? "🟡" : "🔴";
                }
                else row.StatusIndicator = "⚪";
            }
        }
        catch { }

        decimal total = _salaryRows.Sum(r => r.NetSalary);
        double otTotal = _salaryRows.Sum(r => r.OtHours);
        TxtWizTotals.Text = $"Total Payout: Rs {total:N0}  |  Employees: {_salaryRows.Count}  |  Total OT: {otTotal:F1}h";
    }

    // Exports
    private void BtnWizExcel_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"Salary_Wizard.xlsx" };
        if (dlg.ShowDialog() != true) return;
        var range = $"{DpWizStart.SelectedDate:dd-MMM-yyyy} to {DpWizEnd.SelectedDate:dd-MMM-yyyy}";
        ExcelExporter.Export(dlg.FileName, _attRows, _salaryRows, _employeeDb, range);
        MessageBox.Show($"Exported: {dlg.FileName}", "Export Done");
    }

    private void BtnWizPdf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = $"Salary_Wizard.pdf" };
        if (dlg.ShowDialog() != true) return;
        var range = $"{DpWizStart.SelectedDate:dd-MMM-yyyy} to {DpWizEnd.SelectedDate:dd-MMM-yyyy}";
        PdfExporter.Export(dlg.FileName, _attRows, _salaryRows, range);
        MessageBox.Show($"Exported: {dlg.FileName}", "Export Done");
    }

    private void BtnWizPrint_Click(object sender, RoutedEventArgs e)
    {
        PdfExporter.Print(_salaryRows);
    }
}
