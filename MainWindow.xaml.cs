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
            Title = "Select Attendance File(s) — hold Ctrl to select multiple",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            if (dlg.FileNames.Length == 1)
                _data = FileReader.ReadAttendance(dlg.FileNames[0]);
            else
                _data = FileReader.ReadMultiple(dlg.FileNames);

            var (records, issues) = PunchParser.Parse(_data);

            _attendanceRows.Clear();
            _issueRows.Clear();

            foreach (var issue in issues)
                _issueRows.Add(issue);

            foreach (var rec in records)
                _attendanceRows.Add(SalaryCalc.BuildAttendanceRow(rec));

            CalculateSalary();
            BtnRecalc.IsEnabled = true;
            BtnExportExcel.IsEnabled = true;
            BtnExportPdf.IsEnabled = true;
            BtnPrint.IsEnabled = true;

            string fileInfo = dlg.FileNames.Length > 1 ? $"{dlg.FileNames.Length} files merged. " : "";
            if (_issueRows.Count > 0)
            {
                IssuesTab.IsSelected = true;
                StatusText.Text = $"{fileInfo}Loaded {_data.Employees.Count} employees, {_data.Days.Count} days. {_issueRows.Count} issues to resolve.";
            }
            else
            {
                StatusText.Text = $"{fileInfo}Loaded {_data.Employees.Count} employees, {_data.Days.Count} days. Salary ready.";
            }
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
            Title = "Select Previous Salary Sheet"
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
            Name = issue.Name, DayLabel = issue.DayLabel,
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

    private void BtnRecalc_Click(object sender, RoutedEventArgs e)
    {
        CalculateSalary();
        StatusText.Text = "Salary recalculated from attendance data.";
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

            var sal = SalaryCalc.Calculate(grp.Key, grp.ToList(), entry.DailyRate, 0, 0, entry.Category);
            _salaryRows.Add(sal);
        }
    }

    private string GetDateSuffix()
    {
        return _data?.Duration?.Replace("/", "-").Replace(" ~ ", "_to_").Replace(" + ", "_") ?? DateTime.Now.ToString("yyyy-MM-dd");
    }

    private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"Salary_Sheet_{GetDateSuffix()}.xlsx" };
        if (dlg.ShowDialog() != true) return;
        ExcelExporter.Export(dlg.FileName, _attendanceRows, _salaryRows, _employeeDb, _data?.Duration ?? "");
        StatusText.Text = $"Excel exported: {dlg.FileName}";
    }

    private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = $"Salary_Sheet_{GetDateSuffix()}.pdf" };
        if (dlg.ShowDialog() != true) return;
        PdfExporter.Export(dlg.FileName, _attendanceRows, _salaryRows, _data?.Duration ?? "");
        StatusText.Text = $"PDF exported: {dlg.FileName}";
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        PdfExporter.Print(_salaryRows);
    }

    private async void BtnDeviceFetch_Click(object sender, RoutedEventArgs e)
    {
        var ip = TxtDeviceIp.Text.Trim();
        int port = int.TryParse(TxtDevicePort.Text.Trim(), out int p) ? p : 5005;

        BtnDeviceConnect.IsEnabled = false;
        TxtDeviceStatus.Text = $"Connecting to {ip}:{port}...";

        try
        {
            using var device = new ZkDevice(ip, port);
            bool connected = await device.ConnectAsync();
            if (!connected)
            {
                TxtDeviceStatus.Text = "❌ Failed to connect. Check IP and port.";
                BtnDeviceConnect.IsEnabled = true;
                return;
            }

            TxtDeviceStatus.Text = "Connected. Fetching attendance logs...";
            var logs = device.GetAttendanceLogs();
            device.Disconnect();

            DeviceLogsGrid.ItemsSource = logs;
            TxtDeviceStatus.Text = $"✅ Fetched {logs.Count} records from device.";
        }
        catch (Exception ex)
        {
            TxtDeviceStatus.Text = $"❌ Error: {ex.Message}";
        }
        BtnDeviceConnect.IsEnabled = true;
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        var help = @"═══════════════════════════════════════════
         LUKS Attendance & Salary - Help
═══════════════════════════════════════════

HOW TO USE THIS SOFTWARE:
─────────────────────────────────────────

STEP 1:  Click '📂 Load Attendance'
         → Select the attendance file (.xls or .xlsx)
           from the attendance machine.
         → You can select MULTIPLE files (hold Ctrl)
           if the week spans two months!
           The system will merge them correctly.

STEP 2:  Go to '⚠️ Issues' tab
         → You will see employees with missing
           OUT times (especially on the last day).
         → Select each row, type the OUT time
           (e.g. 17:00) and click 'Resolve'.
         → Click 'Skip' if the employee was absent.

STEP 3:  Check '📋 Attendance' tab
         → Review all attendance records.
         → Each row shows the DATE and DAY NAME.
         → You CAN EDIT any cell if something is wrong
           (e.g. wrong IN/OUT time was entered).
         → After editing, click '🔄 Recalculate'
           to update the salary.

STEP 4:  Check '💰 Salary' tab
         → Review each employee's salary.
         → You can edit Advance, Arrears, and Extra Hrs.
         → Net Salary updates automatically.

STEP 5:  Export or Print
         → '📊 Export Excel' saves a full Excel file.
         → '📄 Export PDF' saves a PDF for records.
         → '🖨️ Print' sends to your printer.

─────────────────────────────────────────
IMPORTANT NOTES:

• Each day shows date and day name
  (e.g. '23-Jan (Fr)' means January 23, Friday)

• If the week falls across two months,
  select BOTH files when loading — they merge
  automatically by date.

• Worked hours = Time present minus 1 hour lunch
  (lunch only deducted if OUT after 1:00 PM)

• Half day (OUT before 1:00 PM) = no lunch deducted,
  all hours count as work.

• Standard working day = 8 hours
  More than 8h → Overtime (OT)
  Less than 8h → Deduction

• Hourly Rate = Daily Rate ÷ 8

• Hours are rounded to nearest 15 minutes.

• '👥 Employee DB' tab has all employee rates.
  You can edit rates here.

• '📁 Load Previous' loads last week's salary file
  to carry over the Employee DB rates.
═══════════════════════════════════════════";

        MessageBox.Show(help, "Help - How to Use", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadDefaultEmployeeDb()
    {
        foreach (var (name, rate, type, category) in DefaultData.Employees)
            _employeeDb.Add(new EmployeeEntry { Name = name, DailyRate = rate, Type = type, Category = category });
    }
}
