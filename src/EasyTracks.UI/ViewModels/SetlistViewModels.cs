using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EasyTracks.Domain.Models;

namespace EasyTracks_UI.ViewModels;

public partial class SongItemViewModel : ObservableObject
{
    public Song Model { get; }

    public Guid Id => Model.Id;
    public string Title => Model.Title;
    public string Artist => Model.Artist;
    public double Bpm => Model.PlaybackBpm;
    public string KeyDisplay => $"{Model.OriginalKey}{(Model.OriginalScale == KeyScale.Minor ? "m" : "")}";
    public string DurationText => Model.Duration.ToString(@"mm\:ss");

    [ObservableProperty]
    private bool _isSelected;

    public string CoverGradientStart { get; }
    public string CoverGradientEnd { get; }

    public SongItemViewModel(Song model, string gradientStart, string gradientEnd)
    {
        Model = model;
        CoverGradientStart = gradientStart;
        CoverGradientEnd = gradientEnd;
    }
}

public partial class SectionWaveformViewModel : ObservableObject
{
    public SectionMarker Model { get; }

    public string Name => Model.Name;
    public string ColorHex => Model.ColorHex;
    public double DurationSeconds => (Model.EndTime - Model.StartTime).TotalSeconds;

    [ObservableProperty]
    private bool _isActive;

    // Simulated waveform bar heights (normalized 0.15 to 1.0)
    public ObservableCollection<double> WaveformBars { get; } = new();

    public SectionWaveformViewModel(SectionMarker model, Random? rng = null)
    {
        Model = model;
        rng ??= new Random(model.Name.GetHashCode());

        // Generate 16-24 bars per section based on type
        int barCount = 18;
        double baseHeight = model.Type switch
        {
            SectionType.Intro or SectionType.Outro => 0.45,
            SectionType.Verse => 0.60,
            SectionType.Chorus => 0.95,
            SectionType.Bridge => 0.85,
            _ => 0.55
        };

        for (int i = 0; i < barCount; i++)
        {
            double variance = (rng.NextDouble() * 0.4) - 0.2;
            double height = Math.Clamp(baseHeight + variance, 0.15, 1.0);
            WaveformBars.Add(height);
        }
    }
}
