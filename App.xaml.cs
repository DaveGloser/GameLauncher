using System.Configuration;
using System.Data;
using System.Windows;
using System.Threading.Tasks;

namespace GameLauncher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = (Exception)e.ExceptionObject;
        MessageBox.Show("Whoops: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        Environment.Exit(1);
    }
}
