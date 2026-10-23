using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace EasyTracks_UI;

public sealed partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainWindow()
    {
        InitializeComponent();
        Title = "EasyTracks - Multitrack Playback";

        ApplyTitleBarTheme(ElementTheme.Dark);

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }

    public void ApplyTitleBarTheme(ElementTheme theme)
    {
        bool isDark = theme switch
        {
            ElementTheme.Dark => true,
            ElementTheme.Light => false,
            _ => Application.Current.RequestedTheme == ApplicationTheme.Dark
        };

        try
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (hwnd != IntPtr.Zero)
            {
                int useImmersiveDarkMode = isDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useImmersiveDarkMode, sizeof(int));
            }
        }
        catch
        {
            // Ignore DWM interop errors on unsupported OS builds
        }

        try
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = this.AppWindow.TitleBar;
                if (isDark)
                {
                    var bg = Color.FromArgb(255, 18, 18, 21);
                    var fg = Color.FromArgb(255, 244, 244, 245);
                    var hoverBg = Color.FromArgb(255, 39, 39, 42);
                    var inactiveFg = Color.FromArgb(255, 161, 161, 170);

                    titleBar.BackgroundColor = bg;
                    titleBar.ForegroundColor = fg;
                    titleBar.InactiveBackgroundColor = bg;
                    titleBar.InactiveForegroundColor = inactiveFg;
                    titleBar.ButtonBackgroundColor = bg;
                    titleBar.ButtonForegroundColor = fg;
                    titleBar.ButtonHoverBackgroundColor = hoverBg;
                    titleBar.ButtonHoverForegroundColor = Colors.White;
                    titleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 63, 63, 70);
                    titleBar.ButtonPressedForegroundColor = Colors.White;
                    titleBar.ButtonInactiveBackgroundColor = bg;
                    titleBar.ButtonInactiveForegroundColor = inactiveFg;
                }
                else
                {
                    var bg = Color.FromArgb(255, 248, 250, 252);
                    var fg = Color.FromArgb(255, 15, 23, 42);
                    var hoverBg = Color.FromArgb(255, 226, 232, 240);
                    var inactiveFg = Color.FromArgb(255, 100, 116, 139);

                    titleBar.BackgroundColor = bg;
                    titleBar.ForegroundColor = fg;
                    titleBar.InactiveBackgroundColor = bg;
                    titleBar.InactiveForegroundColor = inactiveFg;
                    titleBar.ButtonBackgroundColor = bg;
                    titleBar.ButtonForegroundColor = fg;
                    titleBar.ButtonHoverBackgroundColor = hoverBg;
                    titleBar.ButtonHoverForegroundColor = fg;
                    titleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 203, 213, 225);
                    titleBar.ButtonPressedForegroundColor = fg;
                    titleBar.ButtonInactiveBackgroundColor = bg;
                    titleBar.ButtonInactiveForegroundColor = inactiveFg;
                }
            }
        }
        catch
        {
            // Ignore TitleBar customization errors
        }
    }
}
