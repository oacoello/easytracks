using Microsoft.UI.Xaml;

namespace EasyTracks_UI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "EasyTracks - Multitrack Playback";

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }
}
