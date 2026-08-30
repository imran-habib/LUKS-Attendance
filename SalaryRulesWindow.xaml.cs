#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LuksAttendance;

public partial class SalaryRulesWindow : Window
{
    private HashSet<string> _exemptCategories;
    private HashSet<string> _exemptEmployees;
    private List<string> _allCategories = new();
    private List<string> _allEmployees = new();
    private bool _initialized;

    public SalaryRulesWindow(ObservableCollection<EmployeeEntry> employeeDb)
    {
        InitializeComponent();
        _exemptCategories = DatabaseService.LoadOtExemptCategories();
        _exemptEmployees = DatabaseService.LoadOtExemptEmployees();
        LoadAllNames(employeeDb);
        PopulateRules();
        RefreshExemptList();
        RefreshSearchResults();
        _initialized = true;
    }

    private void LoadAllNames(IEnumerable<EmployeeEntry> employeeDb)
    {
        _allCategories = employeeDb
            .Select(e => e.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Add common categories that might not be in the DB yet
        foreach (var cat in new[] { "Worker", "Carpenter", "Polishing", "Cushioning", "Helper",
            "Electrician", "Painter", "Welder", "Supervisor", "Driver" })
        {
            if (!_allCategories.Contains(cat, StringComparer.OrdinalIgnoreCase))
                _allCategories.Add(cat);
        }
        _allCategories.Sort(StringComparer.OrdinalIgnoreCase);

        _allEmployees = employeeDb
            .Select(e => e.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();
    }

    private void PopulateRules()
    {
        TxtRules.Text =
            $"═══ Salary Calculation Rules ═══\n\n" +
            $"Standard Work Day: {SalaryCalc.StandardWorkHours}h  |  Lunch: {SalaryCalc.LunchBreakMinutes}min (if OUT ≥ {SalaryCalc.LunchCutoffHour}:00)\n" +
            $"Rounding: {SalaryCalc.RoundingMinutes}min  |  Grace: ±{SalaryCalc.GraceWindowHours}h (exclusive)\n\n" +
            $"WEEKLY:  Salary = Days×Rate + NetHours×HourlyRate - Advance + Arrears\n" +
            $"OT-EXEMPT: Salary = Days×Rate - Advance + Arrears  (OT shown, not paid)\n" +
            $"MONTHLY: Salary = Days×Rate - Advance + Arrears  (no OT calc)";
    }

    // ═══ Exempt List Display ═══

    private void RefreshExemptList()
    {
        LstExempt.Items.Clear();

        foreach (var cat in _exemptCategories.OrderBy(c => c))
            LstExempt.Items.Add($"📁 [Category]  {cat}");

        foreach (var emp in _exemptEmployees.OrderBy(e => e))
            LstExempt.Items.Add($"👤 [Employee]  {emp}");

        if (LstExempt.Items.Count == 0)
            LstExempt.Items.Add("(none — all categories/employees get OT pay)");
    }

    // ═══ Type Dropdown Changed ═══

    private void CmbExemptType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        TxtSearch.Text = "";
        RefreshSearchResults();
    }

    // ═══ Live Search ═══

    private bool IsTypeCategory => CmbExemptType?.SelectedIndex == 0;

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        RefreshSearchResults();
    }

    private void RefreshSearchResults()
    {
        if (LstSearchResults == null || CmbExemptType == null) return;
        LstSearchResults.Items.Clear();

        var query = TxtSearch?.Text?.Trim().ToLower() ?? "";
        var source = IsTypeCategory ? _allCategories : _allEmployees;
        var alreadyExempt = IsTypeCategory ? (ICollection<string>)_exemptCategories : _exemptEmployees;

        var matches = source
            .Where(item => !alreadyExempt.Contains(item))
            .Where(item => string.IsNullOrEmpty(query) || item.ToLower().Contains(query))
            .Take(20)
            .ToList();

        foreach (var item in matches)
            LstSearchResults.Items.Add(item);

        if (matches.Count == 0 && !string.IsNullOrEmpty(query))
            LstSearchResults.Items.Add($"(no matching {(IsTypeCategory ? "categories" : "employees")})");
    }

    private void LstSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        if (LstSearchResults.SelectedItem is string selected && !selected.StartsWith("("))
        {
            TxtSearch.TextChanged -= TxtSearch_TextChanged;
            TxtSearch.Text = selected;
            TxtSearch.TextChanged += TxtSearch_TextChanged;
        }
    }

    // ═══ Add / Remove ═══

    private void BtnAddExempt_Click(object sender, RoutedEventArgs e)
    {
        var value = TxtSearch.Text?.Trim();
        if (string.IsNullOrEmpty(value) || value.StartsWith("("))
        {
            MessageBox.Show($"Please select a {(IsTypeCategory ? "category" : "employee")} from the list.", "Nothing Selected");
            return;
        }

        if (IsTypeCategory)
        {
            if (_exemptCategories.Contains(value))
            {
                TxtExemptStatus.Text = $"'{value}' is already OT-exempt.";
                return;
            }
            _exemptCategories.Add(value);
            DatabaseService.SaveOtExemptCategories(_exemptCategories);
            TxtExemptStatus.Text = $"✅ Category '{value}' → OT blocked.";
        }
        else
        {
            if (_exemptEmployees.Contains(value))
            {
                TxtExemptStatus.Text = $"'{value}' is already OT-exempt.";
                return;
            }
            _exemptEmployees.Add(value);
            DatabaseService.SaveOtExemptEmployees(_exemptEmployees);
            TxtExemptStatus.Text = $"✅ Employee '{value}' → OT blocked.";
        }

        TxtSearch.Text = "";
        RefreshExemptList();
        RefreshSearchResults();
    }

    private void BtnRemoveExempt_Click(object sender, RoutedEventArgs e)
    {
        if (LstExempt.SelectedItem is not string selected || selected.StartsWith("(")) return;

        if (selected.Contains("[Category]"))
        {
            var cat = selected.Split("  ", 2).Last().Trim();
            _exemptCategories.Remove(cat);
            DatabaseService.SaveOtExemptCategories(_exemptCategories);
            TxtExemptStatus.Text = $"✅ Category '{cat}' removed — OT will now apply.";
        }
        else if (selected.Contains("[Employee]"))
        {
            var emp = selected.Split("  ", 2).Last().Trim();
            _exemptEmployees.Remove(emp);
            DatabaseService.SaveOtExemptEmployees(_exemptEmployees);
            TxtExemptStatus.Text = $"✅ Employee '{emp}' removed — OT will now apply.";
        }

        RefreshExemptList();
        RefreshSearchResults();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
