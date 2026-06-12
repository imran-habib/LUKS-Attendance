#nullable enable
using System.Windows;
using System.Windows.Input;

namespace LuksAttendance;

public partial class LoginWindow : Window
{
    public bool Authenticated { get; private set; }

    public LoginWindow()
    {
        InitializeComponent();
        TxtUsername.Focus();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e) => TryLogin();

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryLogin();
    }

    private void TryLogin()
    {
        if (TxtUsername.Text.Trim() == "admin" && TxtPassword.Password == "Admin1234")
        {
            Authenticated = true;
            Close();
        }
        else
        {
            TxtError.Text = "Invalid username or password.";
            TxtPassword.Clear();
            TxtPassword.Focus();
        }
    }
}
