#nullable enable
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace LuksAttendance;

public static class AutoUpdater
{
    private const string CurrentVersion = "3.2.0";
    private const string RepoOwner = "imran-habib";
    private const string RepoName = "LUKS-Attendance";

    public static async Task CheckForUpdateAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "LUKS-Attendance-Updater");

            // Check latest GitHub Release
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var response = await http.GetStringAsync(url);
            var json = JsonDocument.Parse(response);

            var tagName = json.RootElement.GetProperty("tag_name").GetString() ?? "";
            var latestVersion = tagName.TrimStart('v');

            // Compare versions
            if (!Version.TryParse(latestVersion, out var latest)) return;
            if (!Version.TryParse(CurrentVersion, out var current)) return;
            if (latest <= current) return; // Already on latest or newer

            // Check skip file
            var skipFile = System.IO.Path.Combine(System.AppContext.BaseDirectory, ".last_update_skip");
            if (System.IO.File.Exists(skipFile) && System.IO.File.ReadAllText(skipFile).Trim() == latestVersion)
                return;

            // Find exe asset download URL
            string downloadUrl = "";
            var assets = json.RootElement.GetProperty("assets");
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                    break;
                }
            }
            if (string.IsNullOrEmpty(downloadUrl)) return;

            var result = MessageBox.Show(
                $"\ud83d\udd04 A new version is available!\n\n" +
                $"Current: v{CurrentVersion}\n" +
                $"Latest: v{latestVersion}\n\n" +
                "Would you like to download the update?",
                "Update Available",
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo { FileName = downloadUrl, UseShellExecute = true });
                MessageBox.Show(
                    $"Download started in your browser.\n\n" +
                    "After download:\n" +
                    "1. Close this application\n" +
                    "2. Replace the old .exe with the new one\n" +
                    "3. Run the new version",
                    "Download Started", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.IO.File.WriteAllText(skipFile, latestVersion);
            }
        }
        catch
        {
            // Silently fail — don't bother user if update check fails
        }
    }
}
