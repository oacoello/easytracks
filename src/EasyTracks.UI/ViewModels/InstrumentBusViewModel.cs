using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyTracks.Domain.Models;

namespace EasyTracks_UI.ViewModels;

public enum InstrumentBusType
{
    All,
    Drums,
    Guitars,
    Keys,
    Vocals,
    Other
}

public partial class InstrumentBusViewModel : ObservableObject
{
    private readonly Action<InstrumentBusType, bool>? _onMuteChanged;
    private readonly Action<InstrumentBusType, int>? _onOutputBusChanged;

    public InstrumentBusType BusType { get; }
    public string Name { get; }
    public string ColorHex { get; }
    public Microsoft.UI.Xaml.Media.SolidColorBrush BusBrush { get; }

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private int _stemCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputBusText))]
    private int _outputBusIndex;

    public string OutputBusText => StemChannelViewModel.FormatOutputBus(OutputBusIndex);

    public InstrumentBusViewModel(
        InstrumentBusType busType,
        string name,
        string colorHex,
        Action<InstrumentBusType, bool>? onMuteChanged = null,
        Action<InstrumentBusType, int>? onOutputBusChanged = null)
    {
        BusType = busType;
        Name = name;
        ColorHex = colorHex;
        _onMuteChanged = onMuteChanged;
        _onOutputBusChanged = onOutputBusChanged;

        byte r = 59, g = 130, b = 246;
        if (!string.IsNullOrEmpty(colorHex) && colorHex.StartsWith("#") && colorHex.Length >= 7)
        {
            r = Convert.ToByte(colorHex.Substring(1, 2), 16);
            g = Convert.ToByte(colorHex.Substring(3, 2), 16);
            b = Convert.ToByte(colorHex.Substring(5, 2), 16);
        }
        BusBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
    }

    partial void OnIsMutedChanged(bool value)
    {
        _onMuteChanged?.Invoke(BusType, value);
    }

    partial void OnOutputBusIndexChanged(int value)
    {
        _onOutputBusChanged?.Invoke(BusType, value);
    }

    [RelayCommand]
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
    }
}
