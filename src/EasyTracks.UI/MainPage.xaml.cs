using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace EasyTracks_UI;

/// <summary>
/// The main content page displayed inside the application window.
/// Add your UI logic, event handlers, and data binding here.
/// </summary>
public sealed partial class MainPage : Page
{
    public ViewModels.MainViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
    }

    private void OnSetlistCardClicked(object sender, ViewModels.SongItemViewModel song)
    {
        ViewModel.SelectSong(song);
    }
}
