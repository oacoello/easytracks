using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EasyTracks_UI.ViewModels;

namespace EasyTracks_UI.Controls;

public sealed partial class SetlistCardControl : UserControl
{
    private static readonly SolidColorBrush SelectedBrush = new(Color.FromArgb(255, 16, 185, 129)); // #10B981 Emerald
    private static readonly SolidColorBrush NormalBrush = new(Color.FromArgb(255, 39, 39, 42));     // #27272A

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SongItemViewModel),
            typeof(SetlistCardControl),
            new PropertyMetadata(null));

    public SongItemViewModel? ViewModel
    {
        get => (SongItemViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler<SongItemViewModel>? CardClicked;

    public SetlistCardControl()
    {
        this.InitializeComponent();
    }

    private void OnCardClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            CardClicked?.Invoke(this, ViewModel);
        }
    }

    private SolidColorBrush GetBorderBrush(bool isSelected) => isSelected ? SelectedBrush : NormalBrush;

    private Thickness GetBorderThickness(bool isSelected) => isSelected ? new Thickness(2) : new Thickness(1);

    private SolidColorBrush GetCoverBrush(string hex)
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

    private string GetBpmText(double bpm) => $"{(int)bpm} BPM";
}
