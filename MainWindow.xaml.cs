#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Text.RegularExpressions;

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
        LoadEmployeeDb();
        _ = AutoUpdater.CheckForUpdateAsync();
    }

    /// <summary>
    /// Load employee DB from SQLite if available, otherwise fall back to hardcoded defaults.
    /// </summary>
    private void LoadEmployeeDb()
    {
        if (DatabaseService.IsConfigured)
        {
            var saved = DatabaseService.LoadEmployees();
            if (saved.Count > 0)
            {
                foreach (var emp in saved)
                    _employeeDb.Add(emp);
                AppLogger.Log($"Loaded {saved.Count} employees from database.");
                return;
            }
        }

        // Fall back to hardcoded defaults (first run or no DB)
        foreach (var (name, rate, type, category, isGrace) in DefaultData.Employees)
            _employeeDb.Add(new EmployeeEntry { Name = name, DailyRate = rate, Type = type, Category = category, IsGrace = isGrace });
        AppLogger.Log($"Loaded {_employeeDb.Count} employees from defaults (first run).");
    }

    /// <summary>Save employee DB to SQLite (call after any changes).</summary>
    private void SaveEmployeeDb()
    {
        try
        {
            if (!DatabaseService.IsConfigured)
            {
                App.PromptDbLocation();
                if (!DatabaseService.IsConfigured) return;
            }
            DatabaseService.SaveEmployees(_employeeDb);
        }
        catch (Exception ex)
        {
            AppLogger.Log("SaveEmployeeDb", ex);
        }
    }

    private string GetCurrentMode() => BtnMonthly.IsChecked == true ? "monthly" : "weekly";

    private void BtnRunPayroll_Click(object sender, RoutedEventArgs e)
    {
        var wizard = new PayrollWizardWindow(_employeeDb) { Owner = this };
        if (wizard.ShowDialog() == true && wizard.Completed)
        {
            _attendanceRows.Clear();
            foreach (var row in wizard.ResultAttendanceRows)
                _attendanceRows.Add(row);
            _salaryRows.Clear();
            foreach (var row in wizard.ResultSalaryRows)
                _salaryRows.Add(row);

            BtnRecalc.IsEnabled = true;
            BtnExportExcel.IsEnabled = true;
            BtnExportPdf.IsEnabled = true;
            BtnPrint.IsEnabled = true;
            MainTabs.SelectedIndex = 1;
            AutoSaveToDb();
            StatusText.Text = $"Payroll completed: {_salaryRows.Count} employees, Rs {_salaryRows.Sum(r => r.NetSalary):N0} total.";
        }
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

            // Build lookup of employee types
            var empTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var empGrace = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var emp in _employeeDb)
            {
                if (!string.IsNullOrWhiteSpace(emp.Name))
                {
                    empTypes[emp.Name.ToLower()] = emp.Type;
                    empGrace[emp.Name.ToLower()] = emp.IsGrace;
                }
            }

            // Monthly employees: just count presence (any punch = 1 day), no OT logic
            var monthlyPresence = new HashSet<string>();
            foreach (var issue in issues)
            {
                if (empTypes.TryGetValue(issue.Name.ToLower(), out var iType) && iType == "excluded")
                    continue;
                if (empTypes.TryGetValue(issue.Name.ToLower(), out var mType2) && mType2 == "monthly")
                {
                    monthlyPresence.Add(issue.Name.ToLower() + "|" + issue.DayLabel);
                    continue;
                }
                _issueRows.Add(issue);
            }

            foreach (var rec in records)
            {
                if (empTypes.TryGetValue(rec.Name.ToLower(), out var rType) && rType == "excluded")
                    continue;
                if (empTypes.TryGetValue(rec.Name.ToLower(), out var mType3) && mType3 == "monthly")
                {
                    monthlyPresence.Add(rec.Name.ToLower() + "|" + rec.DayLabel);
                    continue;
                }
                bool grace = empGrace.TryGetValue(rec.Name.ToLower(), out var g) && g;
                _attendanceRows.Add(SalaryCalc.BuildAttendanceRow(rec, grace));
            }

            // Add monthly presence as simple attendance markers
            foreach (var key in monthlyPresence)
            {
                var parts = key.Split('|', 2);
                _attendanceRows.Add(new AttendanceRow { Name = parts[0], Day = parts[1], Worked = "present", Status = "monthly" });
            }

            CalculateSalary();
            ShowImportSummary();
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
            AppLogger.Log("BtnLoad", ex);
            MessageBox.Show($"Error reading file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnPrevious_Click(object sender, RoutedEventArgs e)
    {
        if (!DatabaseService.IsConfigured)
        {
            // Fallback: open file like before
            var dlg = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                Title = "Select Previous Salary Sheet"
            };
            if (dlg.ShowDialog() != true) return;
            _previousFilePath = dlg.FileName;
            try
            {
                var (db, carryOver) = FileReader.LoadPreviousOutput(_previousFilePath);
                if (db.Count > 0)
                {
                    _employeeDb.Clear();
                    foreach (var entry in db) _employeeDb.Add(entry);
                    SaveEmployeeDb(); // Persist loaded employees
                }
                StatusText.Text = $"Loaded previous data: {db.Count} employees.";
            }
            catch (Exception ex)
            {
                AppLogger.Log("BtnPrevious.LoadFile", ex);
                MessageBox.Show($"Error loading previous file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return;
        }

        var browser = new PreviousPayrollsWindow { Owner = this };
        if (browser.ShowDialog() == true && browser.SelectedPeriodRows != null)
        {
            _salaryRows.Clear();
            foreach (var row in browser.SelectedPeriodRows)
                _salaryRows.Add(row);
            MainTabs.SelectedIndex = 1; // Salary tab
            StatusText.Text = $"Viewing previous: {browser.SelectedPeriodLabel} ({browser.SelectedPeriodRows.Count} employees)";
        }
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        var selected = IssuesGrid.SelectedItems.Cast<IssueRow>().ToList();
        if (selected.Count == 0) return;
        foreach (var issue in selected)
            _issueRows.Remove(issue);
        CalculateSalary();
        StatusText.Text = $"Skipped {selected.Count}. {_issueRows.Count} issues remaining.";
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

        // Look up grace flag for this employee
        bool grace = _employeeDb.Any(emp => emp.Name.Equals(dlg.SelectedName, StringComparison.OrdinalIgnoreCase) && emp.IsGrace);

        int added = 0;
        foreach (var day in dlg.SelectedDays)
        {
            // Prevent duplicate employee+day entries
            if (_attendanceRows.Any(r => r.Name.Equals(dlg.SelectedName, StringComparison.OrdinalIgnoreCase) && r.Day == day))
                continue;
            var rec = new PunchRecord
            {
                Name = dlg.SelectedName,
                DayLabel = day,
                InTime = dlg.InTime,
                OutTime = dlg.OutTime,
                Status = "manual"
            };
            _attendanceRows.Add(SalaryCalc.BuildAttendanceRow(rec, grace));
            added++;
        }

        if (added == 0)
        {
            MessageBox.Show("All selected days already have attendance entries for this employee.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        CalculateSalary();
        StatusText.Text = $"Added {added} manual entries for {dlg.SelectedName}.";
    }

    /// <summary>
    /// Single salary calculation method — delegates to SalaryService for shared logic.
    /// </summary>
    private void CalculateSalary()
    {
        _salaryRows.Clear();
        var mode = GetCurrentMode();
        var results = SalaryService.ComputeSalary(_attendanceRows, _employeeDb, mode);
        foreach (var row in results)
            _salaryRows.Add(row);
    }

    private void AutoSaveToDb()
    {
        try
        {
            if (_salaryRows.Count == 0) return;
            if (!DatabaseService.IsConfigured)
            {
                App.PromptDbLocation();
                if (!DatabaseService.IsConfigured) return;
            }
            var range = GetActualDateRange();
            var parts = range.Split(" to ");
            string weekStart = parts.Length > 0 ? parts[0].Trim() : range;
            string weekEnd = parts.Length > 1 ? parts[1].Trim() : weekStart;
            DatabaseService.SaveWeek(weekStart, weekEnd, _salaryRows);
        }
        catch (Exception ex)
        {
            AppLogger.Log("AutoSaveToDb", ex);
        }
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
            catch (Exception ex)
            {
                AppLogger.Log($"BtnBatchImport[{file}]", ex);
            }
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
                int year = DateTime.Today.Year;
                if (_data?.Duration != null)
                {
                    var ym = Regex.Match(_data.Duration, @"(\d{4})");
                    if (ym.Success) year = int.Parse(ym.Groups[1].Value);
                }
                dt = new DateTime(year, dt.Month, dt.Day);
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
    private List<AttendanceLog> _deviceLogs = new();

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
                var diagLog = string.Join("\n", _device.DiagLog);
                _device = null;
                BtnDeviceConnect.Background = System.Windows.Media.Brushes.IndianRed;
                BtnDeviceConnect.Content = "🔌 Connect (Failed)";
                TxtDeviceStatus.Text = "❌ Connection failed.";
                MessageBox.Show(
                    $"Could not connect to device at {ip}:{port}\n\nDiagnostic Log:\n" + diagLog,
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
            AppLogger.Log("BtnDeviceConnect", ex);
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

        if (fromDate > toDate)
        {
            MessageBox.Show("'From' date must be before 'To' date.", "Invalid Date Range", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

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

            _deviceLogs = filtered;
            DeviceLogsGrid.ItemsSource = filtered;
            BtnDeviceToSalary.IsEnabled = filtered.Count > 0;
            TxtDeviceStatus.Text = $"✅ {filtered.Count} records ({fromDate:dd-MMM} to {toDate:dd-MMM}). Total: {allLogs.Count}";

            if (filtered.Count > 0)
            {
                int employees = filtered.Select(l => l.UserId).Distinct().Count();
                int days2 = filtered.Select(l => l.Timestamp.Date).Distinct().Count();
                MessageBox.Show(
                    $"✅ Data ready!\n\n• Records: {filtered.Count}\n• Employees: {employees}\n• Days: {days2}",
                    "Data Ready", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            TxtDeviceStatus.Text = $"❌ Error: {ex.Message}";
            AppLogger.Log("BtnDeviceImport", ex);
        }
    }

    private void BtnDeviceToSalary_Click(object sender, RoutedEventArgs e)
    {
        if (_deviceLogs.Count == 0)
        {
            MessageBox.Show("No imported data. Import from device first.", "No Data");
            return;
        }

        // Build grace lookup
        var empGrace = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var emp in _employeeDb)
            if (!string.IsNullOrWhiteSpace(emp.Name))
                empGrace[emp.Name.ToLower()] = emp.IsGrace;

        // Group logs by user+date, pair first and last punch as IN/OUT
        var grouped = _deviceLogs
            .GroupBy(l => new { l.UserName, Date = l.Timestamp.Date })
            .OrderBy(g => g.Key.Date)
            .ThenBy(g => g.Key.UserName);

        _attendanceRows.Clear();
        _issueRows.Clear();

        string[] dayNames = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        foreach (var grp in grouped)
        {
            var punches = grp.OrderBy(l => l.Timestamp).ToList();
            string dayLabel = $"{grp.Key.Date:dd-MMM} ({dayNames[(int)grp.Key.Date.DayOfWeek]})";
            string name = grp.Key.UserName;
            string inTime = punches.First().Timestamp.ToString("HH:mm");
            bool grace = empGrace.TryGetValue(name.ToLower(), out var g) && g;

            if (punches.Count >= 2)
            {
                string outTime = punches.Last().Timestamp.ToString("HH:mm");
                var rec = new PunchRecord { Name = name, DayLabel = dayLabel, InTime = inTime, OutTime = outTime, Status = "device" };
                _attendanceRows.Add(SalaryCalc.BuildAttendanceRow(rec, grace));
            }
            else
            {
                _issueRows.Add(new IssueRow { Name = name, DayLabel = dayLabel, Type = "Missing OUT", InTime = inTime, OutTime = "?" });
            }
        }

        CalculateSalary();
        BtnRecalc.IsEnabled = true;
        BtnExportExcel.IsEnabled = true;
        BtnExportPdf.IsEnabled = true;
        BtnPrint.IsEnabled = true;

        if (_issueRows.Count > 0)
            IssuesTab.IsSelected = true;
        else
            MainTabs.SelectedIndex = 1; // Salary tab

        StatusText.Text = $"Device → Salary: {_attendanceRows.Count} records, {_salaryRows.Count} employees calculated. {_issueRows.Count} issues.";
    }

    private void BtnPeriodToggle_Click(object sender, RoutedEventArgs e)
    {
        // Toggle mutual exclusion
        if (sender == BtnWeekly)
        {
            BtnWeekly.IsChecked = true;
            BtnMonthly.IsChecked = false;
            BtnWeekly.Background = System.Windows.Media.Brushes.LightGreen;
            BtnWeekly.FontWeight = FontWeights.Bold;
            BtnMonthly.Background = System.Windows.Media.Brushes.LightGray;
            BtnMonthly.FontWeight = FontWeights.Normal;
        }
        else
        {
            BtnMonthly.IsChecked = true;
            BtnWeekly.IsChecked = false;
            BtnMonthly.Background = System.Windows.Media.Brushes.LightGreen;
            BtnMonthly.FontWeight = FontWeights.Bold;
            BtnWeekly.Background = System.Windows.Media.Brushes.LightGray;
            BtnWeekly.FontWeight = FontWeights.Normal;
        }

        if (_data == null)
        {
            // After device import _data is null — just re-filter existing rows
            if (_attendanceRows != null && _attendanceRows.Count > 0)
            {
                CalculateSalary();
                StatusText.Text = $"Showing {GetCurrentMode()} employees: {_salaryRows.Count} in salary.";
            }
            return;
        }

        // Reload with new filter
        var (records, issues) = PunchParser.Parse(_data);
        _attendanceRows.Clear();
        _issueRows.Clear();

        var empTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var empGrace = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var emp in _employeeDb)
        {
            if (!string.IsNullOrWhiteSpace(emp.Name))
            {
                empTypes[emp.Name.ToLower()] = emp.Type;
                empGrace[emp.Name.ToLower()] = emp.IsGrace;
            }
        }

        var monthlyPresence = new HashSet<string>();
        foreach (var issue in issues)
        {
            if (empTypes.TryGetValue(issue.Name.ToLower(), out var iType) && iType == "excluded")
                continue;
            if (empTypes.TryGetValue(issue.Name.ToLower(), out var mType2) && mType2 == "monthly")
            {
                monthlyPresence.Add(issue.Name.ToLower() + "|" + issue.DayLabel);
                continue;
            }
            _issueRows.Add(issue);
        }

        foreach (var rec in records)
        {
            if (empTypes.TryGetValue(rec.Name.ToLower(), out var rType) && rType == "excluded")
                continue;
            if (empTypes.TryGetValue(rec.Name.ToLower(), out var mType3) && mType3 == "monthly")
            {
                monthlyPresence.Add(rec.Name.ToLower() + "|" + rec.DayLabel);
                continue;
            }
            bool grace = empGrace.TryGetValue(rec.Name.ToLower(), out var g) && g;
            _attendanceRows.Add(SalaryCalc.BuildAttendanceRow(rec, grace));
        }

        foreach (var key in monthlyPresence)
        {
            var parts = key.Split('|', 2);
            _attendanceRows.Add(new AttendanceRow { Name = parts[0], Day = parts[1], Worked = "present", Status = "monthly" });
        }

        CalculateSalary();
        StatusText.Text = $"Showing {GetCurrentMode()} employees: {_attendanceRows.Count} records, {_salaryRows.Count} in salary.";
    }

    private void BtnAutoResolve_Click(object sender, RoutedEventArgs e)
    {
        if (_issueRows.Count == 0) return;

        int filled = 0;
        foreach (var issue in _issueRows)
        {
            if (string.IsNullOrEmpty(issue.InTime) || !TimeHelper.IsValidTime(issue.InTime))
                continue;

            var inParts = issue.InTime.Split(':');
            int h = int.Parse(inParts[0]) + SalaryCalc.DefaultShiftHours; // Configurable, not hardcoded 9
            int m = int.Parse(inParts[1]);
            if (h >= 24) h -= 24;
            issue.OutTime = $"{h:D2}:{m:D2}";
            filled++;
        }
        StatusText.Text = $"Pre-filled {filled} OUT times (IN + {SalaryCalc.DefaultShiftHours}h). Edit any that need fixing, then click Resolve All.";
    }

    private void BtnResolveAll_Click(object sender, RoutedEventArgs e)
    {
        if (_issueRows.Count == 0) return;

        var result = MessageBox.Show(
            $"Resolve all {_issueRows.Count} issues with the OUT times shown?\n\nMake sure you have reviewed and fixed any incorrect times.",
            "Confirm Resolve All", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        // Build grace lookup
        var empGrace = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var emp in _employeeDb)
            if (!string.IsNullOrWhiteSpace(emp.Name))
                empGrace[emp.Name.ToLower()] = emp.IsGrace;

        int resolved = 0;
        var toResolve = _issueRows.ToList();
        foreach (var issue in toResolve)
        {
            string outTime = issue.OutTime?.Trim() ?? "";
            if (!TimeHelper.IsValidTime(outTime)) continue;

            bool grace = empGrace.TryGetValue(issue.Name.ToLower(), out var g) && g;
            var rec = new PunchRecord
            {
                Name = issue.Name, DayLabel = issue.DayLabel,
                InTime = issue.InTime, OutTime = outTime,
                Status = "hr_resolved"
            };
            _attendanceRows.Add(SalaryCalc.BuildAttendanceRow(rec, grace));
            _issueRows.Remove(issue);
            resolved++;
        }
        CalculateSalary();
        StatusText.Text = $"Resolved {resolved} issues. {_issueRows.Count} remaining (invalid times skipped).";
    }

    private void BtnAddEmployee_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddEmployeeDialog() { Owner = this };
        dlg.ShowDialog();
        if (dlg.Confirmed)
        {
            _employeeDb.Add(dlg.NewEmployee);
            SaveEmployeeDb(); // Persist to SQLite
            StatusText.Text = $"Added employee: {dlg.NewEmployee.Name} (Rs {dlg.NewEmployee.DailyRate}/day, {dlg.NewEmployee.Category})";
        }
    }

    private void ShowImportSummary()
    {
        int employees = _attendanceRows.Select(r => r.Name).Distinct().Count();
        int days = _attendanceRows.Select(r => r.Day).Distinct().Count();
        int issues = _issueRows.Count;
        int shortShifts = _attendanceRows.Count(r => !string.IsNullOrEmpty(r.Deduction) && r.Deduction != "00:00");

        TxtImportSummary.Text = $"✅ Loaded {employees} employees · {days} days · {issues} missing punches · {shortShifts} short shifts";
        ImportSummaryBanner.Visibility = Visibility.Visible;
    }

    private void BtnViewIssues_Click(object sender, RoutedEventArgs e)
    {
        IssuesTab.IsSelected = true;
    }

    private void BtnRules_Click(object sender, RoutedEventArgs e)
    {
        new SalaryRulesWindow(_employeeDb) { Owner = this }.ShowDialog();
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
  You can edit rates here. Changes are SAVED
  automatically to the database.

• '📁 Load Previous' loads last week's salary file
  to carry over the Employee DB rates.

• '📈 Analytics' tab shows trends and forecasting
  from saved salary history.
═══════════════════════════════════════════";

        MessageBox.Show(help, "Help - How to Use", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
