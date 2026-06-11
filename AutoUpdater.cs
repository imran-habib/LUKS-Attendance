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
    private const string CurrentVersion = "3.0.1";
    private const string RepoOwner = "imran-habib";
    private const string RepoName = "LUKS-Attendance";
    private const string ArtifactName = "LUKS-Attendance";

    private static string GetCurrentBuildSha()
    {
        // Read from embedded file generated at build time
        try
        {
            var shaFile = System.IO.Path.Combine(AppContext.BaseDirectory, "build_sha.txt");
            if (System.IO.File.Exists(shaFile))
                return System.IO.File.ReadAllText(shaFile).Trim();
        }
        catch { }
        return "";
    }

    // Check GitHub Actions for latest successful build
    public static async Task CheckForUpdateAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "LUKS-Attendance-Updater");

            // Get latest successful workflow run
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/actions/runs?status=success&per_page=1";
            var response = await http.GetStringAsync(url);
            var json = JsonDocument.Parse(response);
            var runs = json.RootElement.GetProperty("workflow_runs");

            if (runs.GetArrayLength() == 0) return;

            var latestRun = runs[0];
            var runId = latestRun.GetProperty("id").GetInt64();
            var headSha = latestRun.GetProperty("head_sha").GetString() ?? "";
            var shortSha = headSha.Length >= 7 ? headSha[..7] : headSha;
            var createdAt = latestRun.GetProperty("created_at").GetString() ?? "";

            // Check if this is newer than current version by comparing commit
            var currentSha = GetCurrentBuildSha();

            if (shortSha == currentSha) return; // Already on this build

            // Also check the "skip" file
            var lastCheckFile = System.IO.Path.Combine(AppContext.BaseDirectory, ".last_update_sha");
            string lastSha = "";
            if (System.IO.File.Exists(lastCheckFile))
                lastSha = System.IO.File.ReadAllText(lastCheckFile).Trim();

            if (shortSha == lastSha) return; // Already dismissed this version

            // New version available
            var result = MessageBox.Show(
                $"🔄 A new version is available!\n\n" +
                $"Current: v{CurrentVersion}\n" +
                $"Latest build: {shortSha} ({createdAt})\n\n" +
                $"Would you like to download the update?\n" +
                $"(File will be saved as LUKS-Attendance-{shortSha}.zip)",
                "Update Available",
                MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                // Open the artifacts download page (requires GitHub login)
                // For public access, use the direct download URL
                var downloadUrl = $"https://nightly.link/{RepoOwner}/{RepoName}/actions/runs/{runId}/{ArtifactName}.zip";
                Process.Start(new ProcessStartInfo
                {
                    FileName = downloadUrl,
                    UseShellExecute = true
                });

                // Save SHA so we don't prompt again
                var lastCheckFile2 = System.IO.Path.Combine(AppContext.BaseDirectory, ".last_update_sha");
                System.IO.File.WriteAllText(lastCheckFile2, shortSha);

                MessageBox.Show(
                    $"Download started in your browser.\n\n" +
                    $"File: LUKS-Attendance-{shortSha}.zip\n\n" +
                    "After download:\n" +
                    "1. Close this application\n" +
                    "2. Extract the new .exe\n" +
                    "3. Replace the old .exe with the new one\n" +
                    "4. Run the new version",
                    "Download Started", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Save SHA to not prompt again for this version
                var lastCheckFile3 = System.IO.Path.Combine(AppContext.BaseDirectory, ".last_update_sha");
                System.IO.File.WriteAllText(lastCheckFile3, shortSha);
            }
        }
        catch
        {
            // Silently fail - don't bother user if update check fails
        }
    }
}
