#nullable enable
using System.Windows;

namespace LuksAttendance;

public partial class SalaryRulesWindow : Window
{
    public SalaryRulesWindow() => InitializeComponent();
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
