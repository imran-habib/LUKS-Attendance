using System.Windows;

namespace LuksAttendance;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}\n\n" +
                "The application will continue running.\nPlease report this error.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; // Prevent crash
        };
    }
}
