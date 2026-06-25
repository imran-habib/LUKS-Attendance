#nullable enable
using System;
using System.IO;
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
        var (user, pass) = LoadCredentials();
        if (TxtUsername.Text.Trim() == user && TxtPassword.Password == pass)
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

    private static (string user, string pass) LoadCredentials()
    {
        // Bug #10 fix: load from config file, fallback to defaults
        var configPath = System.IO.Path.Combine(AppContext.BaseDirectory, "luks_credentials.txt");
        if (System.IO.File.Exists(configPath))
        {
            var lines = System.IO.File.ReadAllLines(configPath);
            if (lines.Length >= 2)
                return (lines[0].Trim(), lines[1].Trim());
        }
        return ("admin", "Admin1234");
    }
}
