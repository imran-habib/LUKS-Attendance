#nullable enable
using System.Windows;
using System.Windows.Controls;

namespace LuksAttendance;

public partial class AddEmployeeDialog : Window
{
    public EmployeeEntry NewEmployee { get; private set; } = new();
    public bool Confirmed { get; private set; }

    public AddEmployeeDialog()
    {
        InitializeComponent();
        TxtName.Focus();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Please enter a name.", "Missing Name");
            return;
        }

        if (!int.TryParse(TxtRate.Text.Trim(), out int rate) || rate <= 0)
        {
            MessageBox.Show("Please enter a valid daily rate (must be a positive number).", "Invalid Rate");
            return;
        }

        var category = CmbCategory.Text.Trim();
        if (string.IsNullOrEmpty(category))
        {
            MessageBox.Show("Please select or enter a category.", "Missing Category");
            return;
        }

        var type = (CmbType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "weekly";

        NewEmployee = new EmployeeEntry
        {
            Name = name,
            DailyRate = rate,
            Category = category,
            Type = type
        };

        Confirmed = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
