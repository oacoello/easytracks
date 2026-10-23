using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EasyTracks_UI.ViewModels;

namespace EasyTracks_UI.Controls;

public sealed partial class TransportBarControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(TransportViewModel),
            typeof(TransportBarControl),
            new PropertyMetadata(null));

    public TransportViewModel? ViewModel
    {
        get => (TransportViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public Services.LocalizationService Strings => Services.LocalizationService.Instance;

    public TransportBarControl()
    {
        this.InitializeComponent();
        this.ActualThemeChanged += (_, _) => SetActiveTab(_currentActiveTab);
    }

    public event EventHandler<string>? NavSelected;
    public event EventHandler<bool>? EditToggled;

    public bool IsEditMode { get; set; }
    private string _currentActiveTab = "Live";

    private static readonly SolidColorBrush ActiveGreen = new(Windows.UI.Color.FromArgb(255, 16, 185, 129)); // #10B981
    private static readonly SolidColorBrush AmberPause = new(Windows.UI.Color.FromArgb(255, 245, 158, 11));  // #F59E0B
    private static readonly SolidColorBrush ActivePurple = new(Windows.UI.Color.FromArgb(255, 168, 85, 247)); // #A855F7
    private static readonly SolidColorBrush InactiveBg = new(Windows.UI.Color.FromArgb(40, 113, 113, 122));
    private static readonly SolidColorBrush EditActiveBrush = new(Windows.UI.Color.FromArgb(255, 59, 130, 246));
    private static readonly SolidColorBrush TextWhite = new(Windows.UI.Color.FromArgb(255, 255, 255, 255));
    private static readonly SolidColorBrush TextMuted = new(Windows.UI.Color.FromArgb(255, 113, 113, 122));
    private static readonly SolidColorBrush TransparentBrush = new(Windows.UI.Color.FromArgb(0, 0, 0, 0));

    private static Windows.UI.Color GetSystemAccentColor()
    {
        try
        {
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var c = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            if (c.A > 0) return c;
        }
        catch { }

        if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var res) && res is Windows.UI.Color col)
        {
            return col;
        }
        return Windows.UI.Color.FromArgb(255, 2, 132, 199);
    }

    private static Windows.UI.Color ScaleColorBrightness(Windows.UI.Color c, double factor)
    {
        byte r = (byte)Math.Clamp((int)(c.R * factor), 0, 255);
        byte g = (byte)Math.Clamp((int)(c.G * factor), 0, 255);
        byte b = (byte)Math.Clamp((int)(c.B * factor), 0, 255);
        return Windows.UI.Color.FromArgb(c.A, r, g, b);
    }

    private void ApplyNavButtonVisuals(Button btn, bool isActive)
    {
        bool isLight = this.ActualTheme == ElementTheme.Light;
        var accentColor = GetSystemAccentColor();

        var normalBg = isActive
            ? new SolidColorBrush(accentColor)
            : TransparentBrush;

        var hoverBg = isActive
            ? new SolidColorBrush(ScaleColorBrightness(accentColor, 0.84))
            : new SolidColorBrush(isLight
                ? Windows.UI.Color.FromArgb(28, 0, 0, 0)
                : Windows.UI.Color.FromArgb(35, 255, 255, 255));

        var pressedBg = isActive
            ? new SolidColorBrush(ScaleColorBrightness(accentColor, 0.72))
            : new SolidColorBrush(isLight
                ? Windows.UI.Color.FromArgb(45, 0, 0, 0)
                : Windows.UI.Color.FromArgb(55, 255, 255, 255));

        var normalFg = isActive
            ? TextWhite
            : new SolidColorBrush(isLight
                ? Windows.UI.Color.FromArgb(255, 71, 85, 105)
                : Windows.UI.Color.FromArgb(255, 161, 161, 170));

        var hoverFg = isActive
            ? TextWhite
            : new SolidColorBrush(isLight
                ? Windows.UI.Color.FromArgb(255, 15, 23, 42)
                : Windows.UI.Color.FromArgb(255, 244, 244, 245));

        btn.Background = normalBg;
        btn.Foreground = normalFg;
        btn.Resources["ButtonBackground"] = normalBg;
        btn.Resources["ButtonBackgroundPointerOver"] = hoverBg;
        btn.Resources["ButtonBackgroundPressed"] = pressedBg;
        btn.Resources["ButtonForeground"] = normalFg;
        btn.Resources["ButtonForegroundPointerOver"] = hoverFg;
        btn.Resources["ButtonForegroundPressed"] = hoverFg;
        btn.Resources["ButtonBorderBrushPointerOver"] = TransparentBrush;
        btn.Resources["ButtonBorderBrushPressed"] = TransparentBrush;
    }

    private void ApplyEditButtonVisuals(bool isChecked)
    {
        if (EditModeToggleBtn == null) return;
        bool isLight = this.ActualTheme == ElementTheme.Light;
        var accentColor = GetSystemAccentColor();

        var normalBg = isChecked ? new SolidColorBrush(accentColor) : InactiveBg;
        var hoverBg = isChecked
            ? new SolidColorBrush(ScaleColorBrightness(accentColor, 0.84))
            : new SolidColorBrush(isLight ? Windows.UI.Color.FromArgb(45, 0, 0, 0) : Windows.UI.Color.FromArgb(65, 113, 113, 122));
        var normalFg = isChecked
            ? TextWhite
            : new SolidColorBrush(isLight ? Windows.UI.Color.FromArgb(255, 30, 41, 59) : Windows.UI.Color.FromArgb(255, 228, 228, 231));

        EditModeToggleBtn.Background = normalBg;
        EditModeToggleBtn.Foreground = normalFg;
        EditModeToggleBtn.Resources["ToggleButtonBackground"] = normalBg;
        EditModeToggleBtn.Resources["ToggleButtonBackgroundPointerOver"] = hoverBg;
        EditModeToggleBtn.Resources["ToggleButtonBackgroundPressed"] = hoverBg;
        EditModeToggleBtn.Resources["ToggleButtonBackgroundChecked"] = normalBg;
        EditModeToggleBtn.Resources["ToggleButtonBackgroundCheckedPointerOver"] = hoverBg;
        EditModeToggleBtn.Resources["ToggleButtonBackgroundCheckedPressed"] = new SolidColorBrush(ScaleColorBrightness(accentColor, 0.72));
        EditModeToggleBtn.Resources["ToggleButtonForeground"] = normalFg;
        EditModeToggleBtn.Resources["ToggleButtonForegroundPointerOver"] = normalFg;
        EditModeToggleBtn.Resources["ToggleButtonForegroundChecked"] = TextWhite;
        EditModeToggleBtn.Resources["ToggleButtonForegroundCheckedPointerOver"] = TextWhite;
    }

    public void SetActiveTab(string tab)
    {
        _currentActiveTab = tab;
        if (NavLiveBtn == null || NavSetlistBtn == null || NavSongsBtn == null || NavSettingsBtn == null) return;

        ApplyNavButtonVisuals(NavLiveBtn, tab == "Live");
        ApplyNavButtonVisuals(NavSetlistBtn, tab == "Setlist");
        ApplyNavButtonVisuals(NavSongsBtn, tab == "Songs");
        ApplyNavButtonVisuals(NavSettingsBtn, tab == "Settings");

        if (EditModeToggleBtn != null)
        {
            EditModeToggleBtn.Visibility = tab == "Live" ? Visibility.Visible : Visibility.Collapsed;
            if (tab != "Live" && IsEditMode)
            {
                IsEditMode = false;
                EditModeToggleBtn.IsChecked = false;
                EditToggled?.Invoke(this, false);
            }
            ApplyEditButtonVisuals(IsEditMode);
        }
    }

    private void OnNavLiveClicked(object sender, RoutedEventArgs e)
    {
        SetActiveTab("Live");
        NavSelected?.Invoke(this, "Live");
    }

    private void OnNavSetlistClicked(object sender, RoutedEventArgs e)
    {
        SetActiveTab("Setlist");
        NavSelected?.Invoke(this, "Setlist");
    }

    private void OnNavSongsClicked(object sender, RoutedEventArgs e)
    {
        SetActiveTab("Songs");
        NavSelected?.Invoke(this, "Songs");
    }

    private void OnNavSettingsClicked(object sender, RoutedEventArgs e)
    {
        SetActiveTab("Settings");
        NavSelected?.Invoke(this, "Settings");
    }

    private void OnEditToggled(object sender, RoutedEventArgs e)
    {
        IsEditMode = EditModeToggleBtn.IsChecked == true;
        ApplyEditButtonVisuals(IsEditMode);
        EditToggled?.Invoke(this, IsEditMode);
    }

    private void OnStageViewToggled(object sender, RoutedEventArgs e)
    {
        bool isFullScreen = StageViewToggleBtn.IsChecked == true;
        if (ViewModel != null) ViewModel.IsStageView = isFullScreen;

        var accentColor = GetSystemAccentColor();
        StageViewIcon.Glyph = isFullScreen ? "\uE73F" : "\uE740";
        StageViewToggleBtn.Background = isFullScreen ? new SolidColorBrush(accentColor) : InactiveBg;
        StageViewToggleBtn.Foreground = isFullScreen ? TextWhite : new SolidColorBrush(accentColor);

        try
        {
            var appWindow = App.MainWindowInstance?.AppWindow;
            if (appWindow != null)
            {
                appWindow.SetPresenter(isFullScreen
                    ? Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen
                    : Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped);
            }
        }
        catch
        {
            // Ignore window presenter errors
        }
    }

    private SolidColorBrush GetPlayPauseBrush(bool isPlaying) => isPlaying ? AmberPause : ActiveGreen;
    private string GetPlayPauseGlyph(bool isPlaying) => isPlaying ? "\uE769" : "\uE768";
    private SolidColorBrush GetPadBrush(bool isPadActive) => isPadActive ? ActivePurple : InactiveBg;
    private SolidColorBrush GetLoopForeground(bool isLoopActive) => isLoopActive ? ActiveGreen : TextMuted;
    private SolidColorBrush GetEditButtonBrush(bool isEditMode) => isEditMode ? EditActiveBrush : InactiveBg;
}
