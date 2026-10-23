using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EasyTracks.Domain.Models;

namespace EasyTracks_UI.ViewModels;

public partial class SongItemViewModel : ObservableObject
{
    public Song Model { get; }

    public Guid Id => Model.Id;
    public string Title => Model.Title;
    public string Artist => string.IsNullOrWhiteSpace(Model.Artist) ? "—" : Model.Artist;
    public string ArrangementDisplay => !string.IsNullOrWhiteSpace(Model.Album)
        ? Model.Album
        : (Model.ActiveArrangement != null && !string.IsNullOrWhiteSpace(Model.ActiveArrangement.Name) ? Model.ActiveArrangement.Name : "Original");
    public double Bpm => Model.PlaybackBpm;
    public string BpmDisplay => $"{Model.PlaybackBpm:0.#}";
    public string TimeSignatureDisplay => $"{(Model.TimeSignatureNumerator > 0 ? Model.TimeSignatureNumerator : 4)}/{(Model.TimeSignatureDenominator > 0 ? Model.TimeSignatureDenominator : 4)}";
    public string KeyDisplay => $"{Model.OriginalKey.ToDisplayString()}{(Model.OriginalScale == KeyScale.Minor ? "m" : "")}";
    public string DurationText => Model.Duration.ToString(@"mm\:ss");
    public string? CoverUrl => Model.CoverUrl;
    public bool HasCoverUrl => !string.IsNullOrEmpty(Model.CoverUrl);
    public Microsoft.UI.Xaml.Visibility CoverImageVisibility => HasCoverUrl ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility CoverIconVisibility => HasCoverUrl ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    public Microsoft.UI.Xaml.Media.ImageSource? CoverImageSource
    {
        get
        {
            if (string.IsNullOrEmpty(Model.CoverUrl)) return null;
            try
            {
                return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(Model.CoverUrl));
            }
            catch
            {
                return null;
            }
        }
    }

    public bool AreStemsMissing => Model.Stems.Count > 0 && Model.Stems.All(s => !string.IsNullOrEmpty(s.FilePath) && !File.Exists(s.FilePath));
    public Microsoft.UI.Xaml.Visibility StemsMissingVisibility => AreStemsMissing ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public string StemsMissingTooltip => $"Los archivos de audio no se encuentran en: {Model.SourceDirectory ?? "la ruta original"}";

    public bool IsMetadataPending => string.IsNullOrEmpty(Model.Artist) || Model.Artist == "Desconocido" || Model.Artist == "Unknown" || Model.Arrangements.Count <= 1;
    public Microsoft.UI.Xaml.Visibility MetadataPendingVisibility => IsMetadataPending ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Artist));
        OnPropertyChanged(nameof(ArrangementDisplay));
        OnPropertyChanged(nameof(Bpm));
        OnPropertyChanged(nameof(BpmDisplay));
        OnPropertyChanged(nameof(TimeSignatureDisplay));
        OnPropertyChanged(nameof(KeyDisplay));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(CoverUrl));
        OnPropertyChanged(nameof(HasCoverUrl));
        OnPropertyChanged(nameof(CoverImageVisibility));
        OnPropertyChanged(nameof(CoverIconVisibility));
        OnPropertyChanged(nameof(CoverImageSource));
        OnPropertyChanged(nameof(AreStemsMissing));
        OnPropertyChanged(nameof(StemsMissingVisibility));
        OnPropertyChanged(nameof(StemsMissingTooltip));
        OnPropertyChanged(nameof(IsMetadataPending));
        OnPropertyChanged(nameof(MetadataPendingVisibility));
    }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditVisibility))]
    private bool _isEditMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TransitionGlyph))]
    [NotifyPropertyChangedFor(nameof(TransitionTooltip))]
    [NotifyPropertyChangedFor(nameof(TransitionForeground))]
    [NotifyPropertyChangedFor(nameof(TransitionBorderBrush))]
    private TransitionType _transition = TransitionType.ManualStop;

    public string TransitionGlyph => Transition switch
    {
        TransitionType.ManualStop => "\uE71A", // Stop (square)
        TransitionType.Wait => "\uE769",       // Wait (pause)
        TransitionType.AutoPlay => "\uE768",   // Play direct
        TransitionType.Crossfade => "\uE8D6",  // Crossfade / Overlap
        TransitionType.Hold => "\uE77A",       // Hold (anchor/pad sustain)
        _ => "\uE71A"
    };

    public string TransitionTooltip => Transition switch
    {
        TransitionType.ManualStop => "Stop: Se detiene al terminar la canción",
        TransitionType.Wait => "Wait: Pasa a la siguiente canción en pausa",
        TransitionType.AutoPlay => "Auto-Play: Pasa a la siguiente y reproduce inmediatamente",
        TransitionType.Crossfade => "Crossfade: Fundido cruzado con la siguiente canción",
        TransitionType.Hold => "Hold: Pasa a la siguiente en pausa, sosteniendo Pad y Click",
        _ => "Transición"
    };

    public Microsoft.UI.Xaml.Media.SolidColorBrush TransitionForeground => Transition switch
    {
        TransitionType.ManualStop => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184)), // Slate 400
        TransitionType.Wait => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 158, 11)),       // Amber 500
        TransitionType.AutoPlay => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129)),   // Emerald 500
        TransitionType.Crossfade => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248)),  // Sky 400
        TransitionType.Hold => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 168, 85, 247)),      // Purple 500
        _ => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184))
    };

    public Microsoft.UI.Xaml.Media.SolidColorBrush TransitionBorderBrush => Transition switch
    {
        TransitionType.ManualStop => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(140, 148, 163, 184)),
        TransitionType.Wait => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(220, 245, 158, 11)),
        TransitionType.AutoPlay => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(220, 16, 185, 129)),
        TransitionType.Crossfade => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(220, 56, 189, 248)),
        TransitionType.Hold => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(220, 168, 85, 247)),
        _ => new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(140, 148, 163, 184))
    };

    public Microsoft.UI.Xaml.Visibility EditVisibility => IsEditMode ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public string CoverGradientStart { get; }
    public string CoverGradientEnd { get; }

    public Microsoft.UI.Xaml.Media.SolidColorBrush CoverBrush { get; }

    public SongItemViewModel(Song model, string gradientStart, string gradientEnd)
    {
        Model = model;
        CoverGradientStart = gradientStart;
        CoverGradientEnd = gradientEnd;

        byte r = 59, g = 130, b = 246;
        if (!string.IsNullOrEmpty(gradientStart) && gradientStart.StartsWith("#") && gradientStart.Length >= 7)
        {
            r = Convert.ToByte(gradientStart.Substring(1, 2), 16);
            g = Convert.ToByte(gradientStart.Substring(3, 2), 16);
            b = Convert.ToByte(gradientStart.Substring(5, 2), 16);
        }
        CoverBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
    }
}

public partial class SectionWaveformViewModel : ObservableObject
{
    public SectionMarker Model { get; }
    private double _bpm = 120.0;
    private int _beatsPerBar = 4;
    private float[]? _rawPeaks;

    public string Name => Model.Name;
    public string ColorHex => Model.ColorHex;
    public double DurationSeconds => Math.Max(0.1, (Model.EndTime - Model.StartTime).TotalSeconds);

    [ObservableProperty]
    private double _sectionWidth = 200.0;

    public int BarCount
    {
        get
        {
            double secPerBar = GetSecondsPerBar(_bpm, _beatsPerBar);
            return Math.Max(1, (int)Math.Round(DurationSeconds / secPerBar));
        }
    }

    public int StartBarNumber
    {
        get
        {
            double secPerBar = GetSecondsPerBar(_bpm, _beatsPerBar);
            return Math.Max(1, (int)Math.Round(Model.StartTime.TotalSeconds / secPerBar) + 1);
        }
    }

    public string BarCountText => $"{BarCount} comp.";
    public string TimeRangeText => $"Compás {StartBarNumber} • {Model.StartTime:mm\\:ss} - {Model.EndTime:mm\\:ss} ({BarCount} comp.)";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoopBadgeVisibility))]
    [NotifyPropertyChangedFor(nameof(LoopBadgeText))]
    private bool _isLooping;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoopBadgeText))]
    private int _loopPassCount = 1;

    public Microsoft.UI.Xaml.Visibility LoopBadgeVisibility => IsLooping
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;

    public string LoopBadgeText => $"LOOP x{LoopPassCount}";

    public void ToggleLoop()
    {
        IsLooping = !IsLooping;
        if (IsLooping) LoopPassCount = 1;
    }

    public static double GetSecondsPerBar(double bpm, int beatsPerBar)
    {
        double safeBpm = bpm > 20 ? bpm : 120.0;
        int safeBeats = beatsPerBar > 0 ? beatsPerBar : 4;
        double secPerBeat = 60.0 / safeBpm;
        if (safeBeats == 6) secPerBeat /= 2.0;
        return secPerBeat * safeBeats;
    }

    public void RefreshMetrics(double bpm, int beatsPerBar)
    {
        _bpm = bpm > 20 ? bpm : 120.0;
        _beatsPerBar = beatsPerBar > 0 ? beatsPerBar : 4;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(DurationSeconds));
        OnPropertyChanged(nameof(BarCount));
        OnPropertyChanged(nameof(StartBarNumber));
        OnPropertyChanged(nameof(BarCountText));
        OnPropertyChanged(nameof(TimeRangeText));
    }

    public ObservableCollection<BarItem> WaveformBars { get; } = new();

    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush DownbeatBrush = new(Windows.UI.Color.FromArgb(255, 14, 165, 233));     // Sky 500 prominent bar line
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush NormalBarBrush = new(Windows.UI.Color.FromArgb(215, 100, 116, 139));   // Slate 500 sub-beat

    public SectionWaveformViewModel(
        SectionMarker model,
        int timeSigNumerator = 4,
        Random? rng = null,
        double bpm = 120.0,
        float[]? realPeaks = null,
        double initialWidth = 200.0)
    {
        Model = model;
        _bpm = bpm > 20 ? bpm : 120.0;
        _beatsPerBar = timeSigNumerator > 0 ? timeSigNumerator : 4;
        _rawPeaks = realPeaks;
        UpdateSectionWidthAndBars(initialWidth, realPeaks);
    }

    public void UpdateSectionWidthAndBars(double targetWidth, float[]? updatedPeaks = null)
    {
        if (updatedPeaks != null)
        {
            _rawPeaks = updatedPeaks;
        }

        double clampedWidth = Math.Max(72.0, targetWidth);
        SectionWidth = clampedWidth;

        // Each bar is 3px wide + 2px spacing = 5px per bar, minus 12px horizontal padding
        int visualBarCount = Math.Clamp((int)((clampedWidth - 14.0) / 5.0), 8, 600);
        int musicalBars = Math.Max(1, BarCount);

        WaveformBars.Clear();
        var rng = new Random(Model.Name.GetHashCode());
        double baseHeight = Model.Type switch
        {
            SectionType.Intro or SectionType.Outro => 0.45,
            SectionType.Verse => 0.60,
            SectionType.Chorus => 0.90,
            SectionType.Bridge => 0.80,
            _ => 0.60
        };

        for (int i = 0; i < visualBarCount; i++)
        {
            double normalized;
            if (_rawPeaks != null && _rawPeaks.Length > 0)
            {
                double exactIdx = ((double)i / visualBarCount) * (_rawPeaks.Length - 1);
                int idx0 = Math.Clamp((int)Math.Floor(exactIdx), 0, _rawPeaks.Length - 1);
                int idx1 = Math.Clamp(idx0 + 1, 0, _rawPeaks.Length - 1);
                double frac = exactIdx - idx0;
                double interp = (_rawPeaks[idx0] * (1.0 - frac)) + (_rawPeaks[idx1] * frac);
                normalized = Math.Clamp(interp, 0.14, 1.0);
            }
            else
            {
                double variance = (rng.NextDouble() * 0.36) - 0.18;
                normalized = Math.Clamp(baseHeight + variance, 0.18, 0.95);
            }

            double height = Math.Max(7, normalized * 54.0);
            // Highlight bar boundary ticks based on musical bars
            double barsPerVisualItem = (double)visualBarCount / Math.Max(1, musicalBars);
            bool isBarStart = barsPerVisualItem >= 2.0
                ? Math.Abs(i % barsPerVisualItem) < 1.0
                : (i % Math.Max(1, _beatsPerBar) == 0);

            var brush = isBarStart ? DownbeatBrush : NormalBarBrush;
            WaveformBars.Add(new BarItem(height, brush));
        }
    }
}

public partial class BarItem : ObservableObject
{
    [ObservableProperty]
    private double _height;

    [ObservableProperty]
    private Microsoft.UI.Xaml.Media.SolidColorBrush _barBrush;

    public BarItem(double height, Microsoft.UI.Xaml.Media.SolidColorBrush barBrush)
    {
        _height = height;
        _barBrush = barBrush;
    }
}
