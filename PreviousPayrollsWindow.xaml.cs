#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LuksAttendance;

public class PayrollListItem
{
    public string WeekStart { get; set; } = "";
    public string WeekEnd { get; set; } = "";
    public int TotalEmployees { get; set; }
    public double TotalPayout { get; set; }
    public double TotalOtHours { get; set; }
    public double TotalDedHours { get; set; }
    public string TotalPayoutFormatted => $"Rs {TotalPayout:N0}";
    public string TotalOtHoursFormatted => $"{TotalOtHours:F1}h";
    public string TotalDedHoursFormatted => $"{TotalDedHours:F1}h";
}

public partial class PreviousPayrollsWindow : Window
{
    public List<SalaryRow>? SelectedPeriodRows { get; private set; }
    public string? SelectedPeriodLabel { get; private set; }

    public PreviousPayrollsWindow()
    {
        InitializeComponent();
        LoadData();
    }

    private void LoadData()
    {
        var summaries = DatabaseService.GetWeeklySummaries();
        var items = summaries.Select(s => new PayrollListItem
        {
            WeekStart = s.WeekStart,
            WeekEnd = s.WeekEnd,
            TotalEmployees = s.TotalEmployees,
            TotalPayout = s.TotalPayout,
            TotalOtHours = s.TotalOtHours,
            TotalDedHours = s.TotalDedHours
        }).OrderByDescending(x => x.WeekStart).ToList();

        PayrollGrid.ItemsSource = items;
        TxtInfo.Text = $"{items.Count} payroll periods saved";
    }

    private void BtnViewDetails_Click(object sender, RoutedEventArgs e)
    {
        if (PayrollGrid.SelectedItem is not PayrollListItem item) return;

        var rows = DatabaseService.GetPeriodRecords(item.WeekStart, item.WeekEnd);
        if (rows.Count == 0)
        {
            MessageBox.Show("No detailed records found for this period.", "Empty");
            return;
        }

        SelectedPeriodRows = rows;
        SelectedPeriodLabel = $"{item.WeekStart} to {item.WeekEnd}";
        DialogResult = true;
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
