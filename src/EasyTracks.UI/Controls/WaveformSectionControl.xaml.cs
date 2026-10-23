using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EasyTracks_UI.ViewModels;

namespace EasyTracks_UI.Controls;

public sealed partial class WaveformSectionControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SectionWaveformViewModel),
            typeof(WaveformSectionControl),
            new PropertyMetadata(null));

    public SectionWaveformViewModel? ViewModel
    {
        get => (SectionWaveformViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler<double>? SeekRequested;
    public event EventHandler<SectionWaveformViewModel>? SectionSelected;

    public WaveformSectionControl()
    {
        this.InitializeComponent();
        this.PointerPressed += OnWaveformPointerPressed;
    }

    private double _lastClickSeconds;

    private void OnWaveformPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (ViewModel == null) return;
        SectionSelected?.Invoke(this, ViewModel);

        var point = e.GetCurrentPoint(this);
        double ratio = Math.Clamp(point.Position.X / Math.Max(1.0, this.ActualWidth), 0.0, 1.0);
        double targetSecs = ViewModel.Model.StartTime.TotalSeconds + (ratio * ViewModel.DurationSeconds);
        _lastClickSeconds = targetSecs;

        var mainVm = App.MainViewModelInstance;
        if (mainVm != null)
        {
            mainVm.SelectSectionForEdit(ViewModel);

            // In Arrangement Edit Mode, left-clicking a bar on the waveform opens the Prime/Playback bar-snap section creator
            if (mainVm.IsArrangementEditMode && point.Properties.IsLeftButtonPressed)
            {
                ShowSectionEditFlyout(this, point.Position, targetSecs);
                e.Handled = true;
                return;
            }
        }

        SeekRequested?.Invoke(this, targetSecs);
    }

    private static readonly SolidColorBrush LoopActiveBrush = new(Windows.UI.Color.FromArgb(255, 16, 185, 129));     // Emerald #10B981
    private static readonly SolidColorBrush LoopInactiveBrush = new(Windows.UI.Color.FromArgb(40, 113, 113, 122));   // Subtle neutral
    private static readonly SolidColorBrush ActiveBgBrush = new(Windows.UI.Color.FromArgb(45, 56, 189, 248));        // Subtle cyan glow
    private static readonly SolidColorBrush InactiveBgBrush = new(Windows.UI.Color.FromArgb(18, 113, 113, 122));     // Subtle adaptive tint
    private static readonly SolidColorBrush ActiveBorderBrush = new(Windows.UI.Color.FromArgb(255, 56, 189, 248));   // #38BDF8
    private static readonly SolidColorBrush DefaultBorderBrush = new(Windows.UI.Color.FromArgb(55, 113, 113, 122));  // Subtle adaptive divider
    private static readonly SolidColorBrush TextWhite = new(Windows.UI.Color.FromArgb(255, 255, 255, 255));
    private static readonly SolidColorBrush TextMuted = new(Windows.UI.Color.FromArgb(255, 113, 113, 122));

    private void OnLoopClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        var mainVm = App.MainViewModelInstance;
        if (mainVm != null)
        {
            mainVm.ToggleExclusiveSectionLoop(ViewModel);
        }
        else
        {
            ViewModel.ToggleLoop();
        }
    }

    private void OnSectionBadgeClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null || sender is not FrameworkElement anchor) return;
        App.MainViewModelInstance?.SelectSectionForEdit(ViewModel);
        ShowSectionEditFlyout(anchor, null, null);
    }

    private void OnSectionRightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (ViewModel == null || sender is not FrameworkElement anchor) return;
        var pos = e.GetPosition(this);
        double ratio = Math.Clamp(pos.X / Math.Max(1.0, this.ActualWidth), 0.0, 1.0);
        double clickedSecs = ViewModel.Model.StartTime.TotalSeconds + (ratio * ViewModel.DurationSeconds);
        _lastClickSeconds = clickedSecs;

        App.MainViewModelInstance?.SelectSectionForEdit(ViewModel);
        ShowSectionEditFlyout(this, pos, clickedSecs);
    }

    private void ShowSectionEditFlyout(FrameworkElement anchor, Windows.Foundation.Point? position, double? clickedTimeSeconds)
    {
        if (ViewModel == null) return;
        var mainVm = App.MainViewModelInstance;
        if (mainVm == null) return;

        var flyout = new MenuFlyout();
        string[] presets = ["INTRO", "VERSO 1", "VERSO 2", "PRE-CORO", "CORO", "PUENTE", "INSTRUMENTAL", "ESPONTÁNEO", "OUTRO"];

        // 0. Playback/Prime Style: Insert/Split Section snapped to the exact Bar where user clicked
        double splitTargetSec = clickedTimeSeconds ?? _lastClickSeconds;
        var (snappedBar, snappedSec) = mainVm.GetSnappedBarAtTime(splitTargetSec);
        if (snappedSec > ViewModel.Model.StartTime.TotalSeconds + 0.5 && snappedSec < ViewModel.Model.EndTime.TotalSeconds - 0.5)
        {
            var splitHereSub = new MenuFlyoutSubItem
            {
                Text = $"📍 Insertar Sección en Compás {snappedBar} ({TimeSpan.FromSeconds(snappedSec):mm\\:ss})"
            };
            foreach (var preset in presets)
            {
                var item = new MenuFlyoutItem { Text = $"➕ {preset} (desde Compás {snappedBar})" };
                item.Click += (_, _) => mainVm.SplitSectionAtTime(ViewModel, snappedSec, preset);
                splitHereSub.Items.Add(item);
            }
            flyout.Items.Add(splitHereSub);
            flyout.Items.Add(new MenuFlyoutSeparator());
        }

        // 1. Quick Rename submenu
        var renameSub = new MenuFlyoutSubItem { Text = $"✏️ Renombrar '{ViewModel.Name}'" };
        foreach (var preset in presets)
        {
            var item = new MenuFlyoutItem { Text = preset };
            item.Click += (_, _) => mainVm.RenameSection(ViewModel, preset);
            renameSub.Items.Add(item);
        }
        flyout.Items.Add(renameSub);

        // 2. Snap-to-Grid Bar Adjustments
        var barsSub = new MenuFlyoutSubItem { Text = $"📐 Duración ({ViewModel.BarCount} compases • Snap)" };
        void AddBarDelta(string label, int delta)
        {
            var item = new MenuFlyoutItem { Text = label };
            item.Click += (_, _) => mainVm.AdjustSectionBars(ViewModel, delta);
            barsSub.Items.Add(item);
        }
        AddBarDelta("➕ +1 Compás", 1);
        AddBarDelta("➕ +2 Compases", 2);
        AddBarDelta("➕ +4 Compases", 4);
        barsSub.Items.Add(new MenuFlyoutSeparator());
        AddBarDelta("➖ -1 Compás", -1);
        AddBarDelta("➖ -2 Compases", -2);
        flyout.Items.Add(barsSub);

        flyout.Items.Add(new MenuFlyoutSeparator());

        // 3. Split / Loop / Delete / Reset
        var splitItem = new MenuFlyoutItem { Text = "✂️ Dividir Sección a la Mitad (Snap)" };
        splitItem.Click += (_, _) =>
        {
            mainVm.SelectSectionForEdit(ViewModel);
            mainVm.SplitSelectedSection();
        };
        flyout.Items.Add(splitItem);

        var loopItem = new MenuFlyoutItem { Text = ViewModel.IsLooping ? "⏹️ Desactivar Bucle de Sección" : "🔁 Activar Bucle en Vivo (Loop)" };
        loopItem.Click += (_, _) => mainVm.ToggleExclusiveSectionLoop(ViewModel);
        flyout.Items.Add(loopItem);

        var deleteItem = new MenuFlyoutItem { Text = "🗑️ Eliminar Marcador de Sección" };
        deleteItem.Click += (_, _) =>
        {
            mainVm.SelectSectionForEdit(ViewModel);
            mainVm.DeleteSelectedSection();
        };
        flyout.Items.Add(deleteItem);

        var clearAllItem = new MenuFlyoutItem { Text = "🧹 Dejar Canción Sin Secciones (Reiniciar estructura)" };
        clearAllItem.Click += (_, _) => mainVm.ResetToSingleUnsectionedWaveform();
        flyout.Items.Add(clearAllItem);

        if (position.HasValue)
        {
            flyout.ShowAt(anchor, position.Value);
        }
        else
        {
            flyout.ShowAt(anchor);
        }
    }

    private SolidColorBrush GetLoopBrush(bool isLooping) => isLooping ? LoopActiveBrush : LoopInactiveBrush;
    private SolidColorBrush GetLoopForeground(bool isLooping) => isLooping ? TextWhite : TextMuted;
    private SolidColorBrush GetSectionBackgroundBrush(bool isActive) => isActive ? ActiveBgBrush : InactiveBgBrush;
    private SolidColorBrush GetSectionBorderBrush(bool isActive, bool isLooping) => isLooping ? LoopActiveBrush : (isActive ? ActiveBorderBrush : DefaultBorderBrush);
    private Thickness GetSectionBorderThickness(bool isActive, bool isLooping) => (isActive || isLooping) ? new Thickness(2) : new Thickness(0, 0, 1, 0);

    private SolidColorBrush GetSectionBrush(string hex)
    {
        if (string.IsNullOrEmpty(hex) || !hex.StartsWith("#") || hex.Length < 7)
        {
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 59, 130, 246));
        }

        byte r = Convert.ToByte(hex.Substring(1, 2), 16);
        byte g = Convert.ToByte(hex.Substring(3, 2), 16);
        byte b = Convert.ToByte(hex.Substring(5, 2), 16);
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
    }
}
