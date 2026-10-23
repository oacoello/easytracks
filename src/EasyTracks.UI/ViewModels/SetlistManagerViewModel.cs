using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyTracks.Domain.Models;

namespace EasyTracks_UI.ViewModels;

public partial class SetlistManagerViewModel : ObservableObject
{
    private readonly MainViewModel _mainVm;

    public ObservableCollection<Setlist> AllSetlists { get; } = new();

    [ObservableProperty]
    private Setlist? _currentSetlist;

    [ObservableProperty]
    private string _setlistName = "Domingo Mañana";

    [ObservableProperty]
    private DateTimeOffset _setlistDate = DateTimeOffset.Now;

    public ObservableCollection<SongItemViewModel> Songs => _mainVm.SetlistSongs;

    public SetlistManagerViewModel(MainViewModel mainVm)
    {
        _mainVm = mainVm;
    }

    public void LoadSavedSetlists(List<Setlist> saved)
    {
        AllSetlists.Clear();
        foreach (var s in saved)
        {
            AllSetlists.Add(s);
        }

        if (AllSetlists.Count > 0)
        {
            SelectSetlist(AllSetlists[0]);
        }
        else
        {
            CreateNewSetlist("Servicio Domingo");
        }
    }

    [RelayCommand]
    public void SelectSetlist(Setlist setlist)
    {
        CurrentSetlist = setlist;
        SetlistName = setlist.Name;
        SetlistDate = new DateTimeOffset(setlist.DateCreated);

        // Load songs corresponding to this setlist
        _mainVm.SetlistSongs.Clear();
        foreach (var item in setlist.Items)
        {
            var match = _mainVm.LocalLibrarySongs.FirstOrDefault(s => s.Id == item.SongId);
            if (match != null)
            {
                var vm = new SongItemViewModel(match, "#0EA5E9", "#6366F1")
                {
                    Transition = item.Transition
                };
                _mainVm.SetlistSongs.Add(vm);
            }
        }

        if (_mainVm.SetlistSongs.Count > 0)
        {
            _mainVm.SelectSong(_mainVm.SetlistSongs[0]);
        }
    }

    [RelayCommand]
    public void CreateNewSetlist(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) name = $"Setlist {DateTime.Now:dd MMM}";
        var newSetlist = new Setlist(name)
        {
            DateCreated = DateTime.Now
        };
        AllSetlists.Add(newSetlist);
        CurrentSetlist = newSetlist;
        SetlistName = name;
        SetlistDate = DateTimeOffset.Now;
        _mainVm.SetlistSongs.Clear();
        _mainVm.SaveData();
    }

    [RelayCommand]
    public void DeleteSetlist(Setlist setlist)
    {
        AllSetlists.Remove(setlist);
        if (CurrentSetlist == setlist)
        {
            if (AllSetlists.Count > 0)
            {
                SelectSetlist(AllSetlists[0]);
            }
            else
            {
                CreateNewSetlist("Nuevo Setlist");
            }
        }
        _mainVm.SaveData();
    }

    public void SyncCurrentSetlistItems()
    {
        if (CurrentSetlist == null) return;
        CurrentSetlist.Name = SetlistName;
        CurrentSetlist.DateCreated = SetlistDate.DateTime;
        CurrentSetlist.Items.Clear();
        foreach (var vm in _mainVm.SetlistSongs)
        {
            CurrentSetlist.Items.Add(new SetlistItem(vm.Model, vm.Transition));
        }
    }
}
