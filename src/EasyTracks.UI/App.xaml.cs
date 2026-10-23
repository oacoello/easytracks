using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace EasyTracks_UI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        this.UnhandledException += (s, e) =>
        {
            string log = $"[UnhandledException] {e.Message} \n {e.Exception?.ToString()} \n Inner: {e.Exception?.InnerException?.ToString()}";
            string path = @"C:\Users\oacoe\Documents\antigravity\vibrant-nobel\app_crash.log";
            System.IO.File.AppendAllText(path, log + "\n");
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            string log = $"[AppDomain Unhandled] {e.ExceptionObject}";
            string path = @"C:\Users\oacoe\Documents\antigravity\vibrant-nobel\app_crash.log";
            System.IO.File.AppendAllText(path, log + "\n");
        };
    }

    public static MainWindow? MainWindowInstance { get; private set; }
    public static ViewModels.MainViewModel? MainViewModelInstance { get; set; }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            System.IO.File.WriteAllText(@"C:\Users\oacoe\Documents\antigravity\vibrant-nobel\app_crash.log", "[App] OnLaunched starting...\n");
            _window = new MainWindow();
            MainWindowInstance = (MainWindow)_window;
            _window.Activate();
            System.IO.File.AppendAllText(@"C:\Users\oacoe\Documents\antigravity\vibrant-nobel\app_crash.log", "[App] Window activated!\n");
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(@"C:\Users\oacoe\Documents\antigravity\vibrant-nobel\app_crash.log", $"[OnLaunched Exception] {ex}\n");
            throw;
        }
    }
}
