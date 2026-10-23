using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EasyTracks_UI.ViewModels;

namespace EasyTracks_UI.Controls;

public sealed partial class StemChannelStripControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(StemChannelViewModel),
            typeof(StemChannelStripControl),
            new PropertyMetadata(null));

    public StemChannelViewModel? ViewModel
    {
        get => (StemChannelViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public StemChannelStripControl()
    {
        this.InitializeComponent();
    }

    private static readonly SolidColorBrush ActiveRed = new(Windows.UI.Color.FromArgb(255, 239, 68, 68));   // #EF4444
    private static readonly SolidColorBrush ActiveGold = new(Windows.UI.Color.FromArgb(255, 234, 179, 8));   // #EAB308
    private static readonly SolidColorBrush InactiveBg = new(Windows.UI.Color.FromArgb(50, 113, 113, 122));  // Semi-transparent neutral zinc
    private static readonly SolidColorBrush ActiveText = new(Windows.UI.Color.FromArgb(255, 255, 255, 255));
    private static readonly SolidColorBrush InactiveText = new(Windows.UI.Color.FromArgb(255, 113, 113, 122));

    private SolidColorBrush GetMuteBrush(bool isMuted) => isMuted ? ActiveRed : InactiveBg;
    private SolidColorBrush GetSoloBrush(bool isSolo) => isSolo ? ActiveGold : InactiveBg;
    private SolidColorBrush GetMuteSoloForeground(bool isActive) => isActive ? ActiveText : InactiveText;

    private void OnVolumeSliderDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.Volume = 1.0;
            e.Handled = true;
        }
    }

    private void OnPanSliderDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.Pan = 0.0;
            e.Handled = true;
        }
    }

    private void OnOutputBusSelectorClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null || sender is not FrameworkElement btn) return;

        var flyout = new MenuFlyout();

        void AddOption(MenuFlyoutSubItem? parent, string label, int busIdx)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = label,
                IsChecked = ViewModel.OutputBusIndex == busIdx
            };
            item.Click += (_, _) => ViewModel.OutputBusIndex = busIdx;
            if (parent != null) parent.Items.Add(item);
            else flyout.Items.Add(item);
        }

        // Direct Mackie DL16S Mono Channels 1..16
        for (int ch = 1; ch <= 16; ch++)
        {
            string hint = ch switch
            {
                7 => " (Click / Guide)",
                9 => " (AG)",
                10 => " (Bass)",
                _ => ""
            };
            AddOption(null, $"🎛️ Mackie Canal {ch}{hint}", ch);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        // Mackie DL16S Stereo Pairs (with 11-12 Piano highlighted)
        var pairsMenu = new MenuFlyoutSubItem { Text = "🎛️ Mackie Pares Estéreo (1-2 a 15-16)" };
        for (int ch = 1; ch <= 15; ch += 2)
        {
            string pairHint = ch == 11 ? " (Piano Estéreo)" : "";
            AddOption(pairsMenu, $"Mackie Canal {ch}-{ch + 1} Estéreo{pairHint}", 100 + ch);
        }
        flyout.Items.Add(pairsMenu);

        // Also put 11-12 Stereo directly in top-level for 1-click access
        AddOption(null, "🎹 Mackie Canal 11-12 Estéreo (Piano)", 111);

        flyout.Items.Add(new MenuFlyoutSeparator());

        // 2-Ch Stereo / Split options
        AddOption(null, "🔊 Out 1-2 (Estéreo Master)", 0);
        AddOption(null, "🎧 In-Ears L (Click / Guía Split)", -1);
        AddOption(null, "🎚️ FOH R (Stems Mono Split)", -2);

        flyout.ShowAt(btn);
    }
}
