using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyTracks.Domain.Models;

namespace EasyTracks_UI.ViewModels;

public partial class StemChannelViewModel : ObservableObject
{
    private readonly Action<Guid, float>? _onVolumeChanged;
    private readonly Action<Guid, float>? _onPanChanged;
    private readonly Action<Guid, bool>? _onMuteChanged;
    private readonly Action<Guid, bool>? _onSoloChanged;
    private readonly Action<Guid, int>? _onOutputBusChanged;

    public Guid Id { get; }
    public string Name { get; }
    public StemCategory Category { get; }
    public string CategoryColorHex { get; }
    public Microsoft.UI.Xaml.Media.SolidColorBrush CategoryBrush { get; }

    [ObservableProperty]
    private double _volume; // 0.0 to 1.0 (or up to 1.25)

    [ObservableProperty]
    private double _pan; // -1.0 to +1.0

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isSolo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputBusText))]
    private int _outputBusIndex;

    public string OutputBusText => FormatOutputBus(OutputBusIndex);

    public static string FormatOutputBus(int busIndex) => busIndex switch
    {
        0 => "Out 1-2",
        -1 => "In-Ears (L)",
        -2 => "FOH (R)",
        >= 1 and <= 16 => $"Mackie Ch {busIndex}",
        >= 101 and <= 115 => $"Ch {busIndex - 100}-{busIndex - 99} St",
        _ => "Out 1-2"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MeterHeight))]
    private double _peakLevel; // 0.0 to 1.0 for VU meter

    public double MeterHeight => Math.Clamp(PeakLevel * 115.0, 0.0, 115.0);

    public string PanText => Pan switch
    {
        < -0.05 => $"L {(int)Math.Abs(Pan * 100)}",
        > 0.05 => $"R {(int)(Pan * 100)}",
        _ => "C"
    };

    public string VolumeDbText => Volume <= 0.001 
        ? "-∞ dB" 
        : $"{20 * Math.Log10(Volume):F1} dB";

    public StemChannelViewModel(
        StemTrack stem,
        Action<Guid, float>? onVolumeChanged = null,
        Action<Guid, float>? onPanChanged = null,
        Action<Guid, bool>? onMuteChanged = null,
        Action<Guid, bool>? onSoloChanged = null,
        Action<Guid, int>? onOutputBusChanged = null)
    {
        Id = stem.Id;
        Name = stem.Name;
        Category = stem.Category;
        Volume = stem.Volume;
        Pan = stem.Pan;
        IsMuted = stem.IsMuted;
        IsSolo = stem.IsSolo;
        OutputBusIndex = stem.OutputBusIndex;
        CategoryColorHex = GetCategoryColor(stem.Category);
        CategoryBrush = GetCategoryBrush(stem.Category);

        _onVolumeChanged = onVolumeChanged;
        _onPanChanged = onPanChanged;
        _onMuteChanged = onMuteChanged;
        _onSoloChanged = onSoloChanged;
        _onOutputBusChanged = onOutputBusChanged;
    }

    partial void OnVolumeChanged(double value)
    {
        OnPropertyChanged(nameof(VolumeDbText));
        _onVolumeChanged?.Invoke(Id, (float)value);
    }

    partial void OnPanChanged(double value)
    {
        OnPropertyChanged(nameof(PanText));
        _onPanChanged?.Invoke(Id, (float)value);
    }

    partial void OnIsMutedChanged(bool value)
    {
        _onMuteChanged?.Invoke(Id, value);
    }

    partial void OnIsSoloChanged(bool value)
    {
        _onSoloChanged?.Invoke(Id, value);
    }

    partial void OnOutputBusIndexChanged(int value)
    {
        _onOutputBusChanged?.Invoke(Id, value);
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    [RelayCommand]
    private void ToggleSolo()
    {
        IsSolo = !IsSolo;
    }

    [RelayCommand]
    private void ResetFader()
    {
        Volume = 1.0;
        Pan = 0.0;
    }

    private static string GetCategoryColor(StemCategory category) => category switch
    {
        StemCategory.Click => "#06B6D4",         // Cyan
        StemCategory.Guide => "#F97316",         // Orange
        StemCategory.Drums => "#EAB308",         // Yellow / Gold
        StemCategory.Percussion => "#F59E0B",    // Amber
        StemCategory.Bass => "#A855F7",          // Purple
        StemCategory.AcousticGuitar => "#10B981",// Emerald
        StemCategory.ElectricGuitar => "#22C55E",// Green
        StemCategory.Piano => "#3B82F6",         // Blue
        StemCategory.Keys => "#6366F1",          // Indigo
        StemCategory.Synth => "#8B5CF6",         // Violet
        StemCategory.Strings => "#0284C7",       // Sky
        StemCategory.Brass => "#D97706",         // Bronze
        StemCategory.LeadVocal => "#EC4899",     // Pink
        StemCategory.BackingVocals => "#F43F5E", // Rose
        StemCategory.Choir => "#FB7185",         // Light Rose
        StemCategory.SoundEffects => "#64748B",  // Slate
        _ => "#94A3B8"                           // Neutral Gray
    };

    private static Microsoft.UI.Xaml.Media.SolidColorBrush GetCategoryBrush(StemCategory category)
    {
        var hex = GetCategoryColor(category);
        byte r = Convert.ToByte(hex.Substring(1, 2), 16);
        byte g = Convert.ToByte(hex.Substring(3, 2), 16);
        byte b = Convert.ToByte(hex.Substring(5, 2), 16);
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
    }
}
