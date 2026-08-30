#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
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
        MigrateCredentialsIfNeeded();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e) => TryLogin();

    private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) TryLogin();
    }

    private void TryLogin()
    {
        var (storedUser, storedHash, storedSalt) = LoadHashedCredentials();
        var inputUser = TxtUsername.Text.Trim();
        var inputPass = TxtPassword.Password;

        if (inputUser == storedUser && VerifyPassword(inputPass, storedHash, storedSalt))
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

    // ═══ Credential Storage (PBKDF2 hashed) ═══

    private static readonly string CredPath = Path.Combine(AppContext.BaseDirectory, "luks_credentials.dat");
    private static readonly string LegacyCredPath = Path.Combine(AppContext.BaseDirectory, "luks_credentials.txt");

    /// <summary>
    /// Migrate plain-text credentials to hashed format on first run.
    /// </summary>
    private static void MigrateCredentialsIfNeeded()
    {
        if (File.Exists(CredPath)) return; // Already migrated

        string user = "admin";
        string pass = "Admin1234";

        // Read from legacy plain-text file if it exists
        if (File.Exists(LegacyCredPath))
        {
            try
            {
                var lines = File.ReadAllLines(LegacyCredPath);
                if (lines.Length >= 2)
                {
                    user = lines[0].Trim();
                    pass = lines[1].Trim();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Log("MigrateCredentials", ex);
            }
        }

        // Hash and save
        SaveHashedCredentials(user, pass);

        // Remove legacy plain-text file
        try
        {
            if (File.Exists(LegacyCredPath))
            {
                File.Delete(LegacyCredPath);
                AppLogger.Log("Migrated plain-text credentials to hashed format and deleted luks_credentials.txt");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("DeleteLegacyCreds", ex);
        }
    }

    private static void SaveHashedCredentials(string username, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashPassword(password, salt);

        // Format: username\nbase64(salt)\nbase64(hash)
        File.WriteAllText(CredPath, $"{username}\n{Convert.ToBase64String(salt)}\n{Convert.ToBase64String(hash)}");
    }

    private static (string user, byte[] hash, byte[] salt) LoadHashedCredentials()
    {
        try
        {
            if (File.Exists(CredPath))
            {
                var lines = File.ReadAllLines(CredPath);
                if (lines.Length >= 3)
                {
                    return (
                        lines[0].Trim(),
                        Convert.FromBase64String(lines[2].Trim()),
                        Convert.FromBase64String(lines[1].Trim())
                    );
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("LoadHashedCredentials", ex);
        }

        // Fallback: create default hashed credentials
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashPassword("Admin1234", salt);
        return ("admin", hash, salt);
    }

    private static byte[] HashPassword(string password, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32);
    }

    private static bool VerifyPassword(string password, byte[] storedHash, byte[] salt)
    {
        var hash = HashPassword(password, salt);
        return CryptographicOperations.FixedTimeEquals(hash, storedHash);
    }
}
