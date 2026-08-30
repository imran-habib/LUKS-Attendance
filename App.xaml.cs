using System;
using System.Windows;

namespace LuksAttendance;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (s, args) =>
        {
            AppLogger.Log("UnhandledException", args.Exception);
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}\n\n" +
                "The application will continue running.\nPlease report this error.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // Prevent shutdown when login window closes
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Show login
        var login = new LoginWindow();
        login.ShowDialog();
        if (!login.Authenticated)
        {
            Shutdown();
            return;
        }

        // Try to load existing DB settings silently
        DatabaseService.LoadSettings();

        AppLogger.Log("Application started.");

        // Show main window, shutdown when it closes
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }

    public static void PromptDbLocation()
    {
        var result = MessageBox.Show(
            "LUKS Salary Software saves history for analytics & forecasting.\n\n" +
            "Select a folder to store the salary database.\n" +
            "(Tip: use a backed-up folder like OneDrive or USB)\n\n" +
            "Click OK to choose a folder, or Cancel to use the app folder.",
            "Database Location", MessageBoxButton.OKCancel, MessageBoxImage.Information);

        if (result == MessageBoxResult.OK)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select folder for LUKS Salary Database"
            };
            if (dlg.ShowDialog() == true)
            {
                DatabaseService.Configure(dlg.FolderName);
                return;
            }
        }
        DatabaseService.Configure(AppContext.BaseDirectory);
    }
}
