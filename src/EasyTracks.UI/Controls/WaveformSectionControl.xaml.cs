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

    public WaveformSectionControl()
    {
        this.InitializeComponent();
    }

    private SolidColorBrush GetSectionBrush(string hex)
    {
        if (string.IsNullOrEmpty(hex) || !hex.StartsWith("#") || hex.Length < 7)
        {
            return new SolidColorBrush(Color.FromArgb(255, 59, 130, 246));
        }

        byte r = Convert.ToByte(hex.Substring(1, 2), 16);
        byte g = Convert.ToByte(hex.Substring(3, 2), 16);
        byte b = Convert.ToByte(hex.Substring(5, 2), 16);
        return new SolidColorBrush(Color.FromArgb(255, r, g, b));
    }

    private double GetBarHeight(double normalizedValue)
    {
        // Max height in container is ~36px, minimum 4px
        return Math.Max(4, normalizedValue * 36);
    }
}
