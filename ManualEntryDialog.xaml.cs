#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LuksAttendance;

public partial class ManualEntryDialog : Window
{
    private readonly List<string> _employeeNames;
    private readonly List<string> _availableDays;
    private readonly bool _hasExistingDays;

    public string SelectedName { get; private set; } = "";
    public List<string> SelectedDays { get; private set; } = new();
    public string InTime { get; private set; } = "08:00";
    public string OutTime { get; private set; } = "17:00";
    public bool Confirmed { get; private set; }

    public ManualEntryDialog(List<string> employeeNames, List<string> availableDays)
    {
        InitializeComponent();
        _employeeNames = employeeNames;
        _availableDays = availableDays;
        _hasExistingDays = availableDays.Count > 0;

        if (_hasExistingDays)
        {
            // Show day list from loaded attendance
            foreach (var day in _availableDays)
                LstDates.Items.Add(day);
            LstDates.SelectAll();
            PnlDateRange.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Show date pickers for manual range
            LstDates.Visibility = Visibility.Collapsed;
            DpManualFrom.SelectedDate = DateTime.Today.AddDays(-6);
            DpManualTo.SelectedDate = DateTime.Today;
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = TxtSearch.Text.Trim().ToLower();
        LstSuggestions.Items.Clear();

        if (string.IsNullOrEmpty(query))
        {
            LstSuggestions.Visibility = Visibility.Collapsed;
            return;
        }

        var matches = _employeeNames
            .Where(n => n.ToLower().Contains(query))
            .Take(5)
            .ToList();

        if (matches.Count > 0)
        {
            foreach (var m in matches)
                LstSuggestions.Items.Add(m);
            LstSuggestions.Visibility = Visibility.Visible;
        }
        else
        {
            LstSuggestions.Visibility = Visibility.Collapsed;
        }
    }

    private void LstSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstSuggestions.SelectedItem is string name)
        {
            TxtSearch.TextChanged -= TxtSearch_TextChanged; // prevent re-trigger
            TxtSearch.Text = name;
            TxtSearch.TextChanged += TxtSearch_TextChanged;
            LstSuggestions.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        SelectedName = TxtSearch.Text.Trim();
        if (string.IsNullOrEmpty(SelectedName))
        {
            MessageBox.Show("Please enter or select an employee name.", "Missing Name");
            return;
        }

        if (_hasExistingDays)
        {
            SelectedDays = LstDates.SelectedItems.Cast<string>().ToList();
        }
        else
        {
            // Generate days from date pickers
            var from = DpManualFrom.SelectedDate ?? DateTime.Today.AddDays(-6);
            var to = DpManualTo.SelectedDate ?? DateTime.Today;
            SelectedDays = new List<string>();
            for (var dt = from; dt <= to; dt = dt.AddDays(1))
                SelectedDays.Add(dt.ToString("dd-MMM (ddd)"));
        }

        if (SelectedDays.Count == 0)
        {
            MessageBox.Show("Please select at least one day.", "No Days Selected");
            return;
        }

        InTime = TxtInTime.Text.Trim();
        OutTime = TxtOutTime.Text.Trim();

        if (!TimeHelper.IsValidTime(InTime) || !TimeHelper.IsValidTime(OutTime))
        {
            MessageBox.Show("Please enter valid times (HH:MM format).", "Invalid Time");
            return;
        }

        Confirmed = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
