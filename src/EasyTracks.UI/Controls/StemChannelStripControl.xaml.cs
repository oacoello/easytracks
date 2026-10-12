using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
}
