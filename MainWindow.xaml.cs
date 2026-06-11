#nullable enable
using System;
using System.Collections.Generic;
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
        _ = AutoUpdater.CheckForUpdateAsync();
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

    private void BtnAddManualAttendance_Click(object sender, RoutedEventArgs e)
    {
        var names = _employeeDb.Select(emp => emp.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        var days = _attendanceRows.Select(r => r.Day).Distinct().OrderBy(d => d).ToList();

        var dlg = new ManualEntryDialog(names, days) { Owner = this };
        dlg.ShowDialog();

        if (!dlg.Confirmed) return;

        foreach (var day in dlg.SelectedDays)
        {
            var rec = new PunchRecord
            {
                Name = dlg.SelectedName,
                DayLabel = day,
                InTime = dlg.InTime,
                OutTime = dlg.OutTime,
                Status = "manual"
            };
            _attendanceRows.Add(SalaryCalc.BuildAttendanceRow(rec));
        }

        CalculateSalary();
        StatusText.Text = $"Added {dlg.SelectedDays.Count} manual entries for {dlg.SelectedName}.";
    }

    private void CalculateSalary()
    {
        _salaryRows.Clear();
        var dbDict = new Dictionary<string, EmployeeEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var emp in _employeeDb)
        {
            var key = emp.Name.ToLower();
            if (!string.IsNullOrWhiteSpace(key) && !dbDict.ContainsKey(key))
                dbDict[key] = emp;
        }
        var grouped = _attendanceRows.Where(r => !string.IsNullOrWhiteSpace(r.Name)).GroupBy(r => r.Name.ToLower());

        foreach (var grp in grouped.OrderBy(g => g.Key))
        {
            if (!dbDict.TryGetValue(grp.Key, out var entry)) continue;
            if (entry.Type == "excluded") continue;
            if (entry.Type == "monthly") continue;

            var sal = SalaryCalc.Calculate(grp.Key, grp.ToList(), entry.DailyRate, 0, 0, entry.Category);
            _salaryRows.Add(sal);
        }
        AutoSaveToDb();
    }

    private void AutoSaveToDb()
    {
        try
        {
            if (_salaryRows.Count == 0) return;
            var range = GetActualDateRange();
            var parts = range.Split(" to ");
            string weekStart = parts.Length > 0 ? parts[0].Trim() : range;
            string weekEnd = parts.Length > 1 ? parts[1].Trim() : weekStart;
            DatabaseService.SaveWeek(weekStart, weekEnd, _salaryRows);
        }
        catch { }
    }

    private void BtnBatchImport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            Title = "Select old salary export files to import into DB",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;

        int imported = 0;
        foreach (var file in dlg.FileNames)
        {
            try
            {
                var (rows, weekRange) = FileReader.ReadSalaryExport(file);
                if (rows.Count > 0 && !string.IsNullOrEmpty(weekRange))
                {
                    var parts = weekRange.Split(" to ");
                    string ws = parts.Length > 0 ? parts[0].Trim() : weekRange;
                    string we = parts.Length > 1 ? parts[1].Trim() : ws;
                    DatabaseService.SaveWeek(ws, we, rows);
                    imported++;
                }
            }
            catch { }
        }
        StatusText.Text = $"Batch import: {imported}/{dlg.FileNames.Length} files imported to database.";
    }

    private void BtnChangeDbLocation_Click(object sender, RoutedEventArgs e)
    {
        App.PromptDbLocation();
        StatusText.Text = $"Database location: {DatabaseService.DbPath}";
    }

    private string GetDateSuffix()
    {
        var range = GetActualDateRange();
        return range.Replace("/", "-").Replace(" ~ ", "_to_").Replace(" ", "");
    }

    private string GetActualDateRange()
    {
        var days = _attendanceRows
            .Select(r => r.Day)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .ToList();

        if (days.Count == 0)
            return _data?.Duration ?? DateTime.Now.ToString("yyyy-MM-dd");

        var dates = new List<DateTime>();
        foreach (var d in days)
        {
            var part = d.Split('(')[0].Trim();
            if (DateTime.TryParseExact(part, "dd-MMM",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
            {
                dt = new DateTime(DateTime.Today.Year, dt.Month, dt.Day);
                dates.Add(dt);
            }
        }

        if (dates.Count == 0)
            return _data?.Duration ?? DateTime.Now.ToString("yyyy-MM-dd");

        var min = dates.Min();
        var max = dates.Max();
        return $"{min:dd-MMM-yyyy} to {max:dd-MMM-yyyy}";
    }

    private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"Salary_Sheet_{GetDateSuffix()}.xlsx" };
        if (dlg.ShowDialog() != true) return;
        ExcelExporter.Export(dlg.FileName, _attendanceRows, _salaryRows, _employeeDb, GetActualDateRange());
        StatusText.Text = $"Excel exported: {dlg.FileName}";
    }

    private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = $"Salary_Sheet_{GetDateSuffix()}.pdf" };
        if (dlg.ShowDialog() != true) return;
        PdfExporter.Export(dlg.FileName, _attendanceRows, _salaryRows, GetActualDateRange());
        StatusText.Text = $"PDF exported: {dlg.FileName}";
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        PdfExporter.Print(_salaryRows);
    }

    private ZkDevice? _device;
    private Dictionary<string, string> _deviceUsers = new();

    private async void BtnDeviceConnect_Click(object sender, RoutedEventArgs e)
    {
        var ip = TxtDeviceIp.Text.Trim();
        int port = int.TryParse(TxtDevicePort.Text.Trim(), out int p) ? p : 5005;

        if (_device != null)
        {
            _device.Disconnect();
            _device = null;
            BtnDeviceConnect.Content = "🔌 Connect";
            BtnDeviceConnect.Background = System.Windows.Media.Brushes.LightGray;
            PnlDeviceImport.Visibility = Visibility.Collapsed;
            TxtDeviceStatus.Text = "Disconnected.";
            return;
        }

        BtnDeviceConnect.IsEnabled = false;
        TxtDeviceStatus.Text = $"Connecting to {ip}:{port}...";

        try
        {
            _device = new ZkDevice(ip, port);
            bool connected = await _device.ConnectAsync();

            if (!connected)
            {
                _device = null;
                BtnDeviceConnect.Background = System.Windows.Media.Brushes.IndianRed;
                BtnDeviceConnect.Content = "🔌 Connect (Failed)";
                TxtDeviceStatus.Text = "❌ Connection failed.";
                MessageBox.Show(
                    $"Could not connect to device at {ip}:{port}\n\n" +
                    "Please check:\n" +
                    "• Is the attendance machine turned on?\n" +
                    "• Is this PC on the same network (192.168.1.x)?\n" +
                    "• Is the IP and Port correct?\n" +
                    "• Is the Ethernet cable connected to the machine?",
                    "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                BtnDeviceConnect.IsEnabled = true;
                return;
            }

            TxtDeviceStatus.Text = "✅ Connected! Fetching user list...";
            _deviceUsers = _device.GetUsers();

            BtnDeviceConnect.Content = "🟢 Connected (Click to Disconnect)";
            BtnDeviceConnect.Background = System.Windows.Media.Brushes.LightGreen;
            PnlDeviceImport.Visibility = Visibility.Visible;

            DpTo.SelectedDate = DateTime.Today;
            DpFrom.SelectedDate = DateTime.Today.AddDays(-6);

            TxtDeviceStatus.Text = $"✅ Connected! {_deviceUsers.Count} employees found on device. Select date range and click Import.";
        }
        catch (Exception ex)
        {
            _device = null;
            BtnDeviceConnect.Background = System.Windows.Media.Brushes.IndianRed;
            TxtDeviceStatus.Text = $"❌ Error: {ex.Message}";
            MessageBox.Show($"Connection error:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        BtnDeviceConnect.IsEnabled = true;
    }

    private void BtnDeviceImport_Click(object sender, RoutedEventArgs e)
    {
        if (_device == null)
        {
            MessageBox.Show("Not connected to device. Click Connect first.", "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var fromDate = DpFrom.SelectedDate ?? DateTime.Today.AddDays(-6);
        var toDate = DpTo.SelectedDate ?? DateTime.Today;

        TxtDeviceStatus.Text = "Fetching attendance logs...";
        try
        {
            var allLogs = _device.GetAttendanceLogs();
            var filtered = allLogs.Where(l => l.Timestamp.Date >= fromDate.Date && l.Timestamp.Date <= toDate.Date).ToList();

            foreach (var log in filtered)
            {
                if (_deviceUsers.TryGetValue(log.UserId, out var name))
                    log.UserName = name;
                else
                    log.UserName = $"Unknown ({log.UserId})";
            }

            DeviceLogsGrid.ItemsSource = filtered;
            TxtDeviceStatus.Text = $"✅ Imported {filtered.Count} records ({fromDate:dd-MMM} to {toDate:dd-MMM}). Total on device: {allLogs.Count}";

            if (filtered.Count > 0)
            {
                int employees = filtered.Select(l => l.UserId).Distinct().Count();
                int days2 = filtered.Select(l => l.Timestamp.Date).Distinct().Count();
                MessageBox.Show(
                    $"✅ Import successful!\n\n" +
                    $"• Records: {filtered.Count}\n" +
                    $"• Employees: {employees}\n" +
                    $"• Days: {days2}\n" +
                    $"• Period: {fromDate:dd-MMM-yyyy} to {toDate:dd-MMM-yyyy}\n\n" +
                    "Data is shown in the grid. You can now review it.",
                    "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"No records found between {fromDate:dd-MMM-yyyy} and {toDate:dd-MMM-yyyy}.\n\n" +
                    "Try expanding the date range.",
                    "No Records", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            TxtDeviceStatus.Text = $"❌ Error fetching: {ex.Message}";
            MessageBox.Show($"Error fetching logs:\n\n{ex.Message}", "Fetch Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

• '📈 Analytics' tab shows trends and forecasting
  from saved salary history.
═══════════════════════════════════════════";

        MessageBox.Show(help, "Help - How to Use", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadDefaultEmployeeDb()
    {
        foreach (var (name, rate, type, category) in DefaultData.Employees)
            _employeeDb.Add(new EmployeeEntry { Name = name, DailyRate = rate, Type = type, Category = category });
    }
}
