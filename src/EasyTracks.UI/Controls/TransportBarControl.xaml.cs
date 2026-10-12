using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    public TransportBarControl()
    {
        this.InitializeComponent();
    }
}
