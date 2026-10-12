using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyTracks.Core.Abstractions;
using EasyTracks.Core.Audio;
using EasyTracks.Domain.Models;
using EasyTracks.Domain.Services;

namespace EasyTracks_UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAudioEngine _audioEngine;
    private readonly IPlaybackMixer _mixer;
    private readonly IPadPlayer _padPlayer;
    private readonly StemCategoryDetector _categoryDetector;

    public TransportViewModel Transport { get; }
    public ObservableCollection<StemChannelViewModel> Channels { get; } = new();
    public ObservableCollection<SongItemViewModel> SetlistSongs { get; } = new();
    public ObservableCollection<SectionWaveformViewModel> ActiveSections { get; } = new();

    [ObservableProperty]
    private SongItemViewModel? _selectedSong;

    [ObservableProperty]
    private Song? _currentSong;

    public MainViewModel()
    {
        _categoryDetector = new StemCategoryDetector();
        
        _audioEngine = new BassAudioEngine();
        _audioEngine.Initialize();

        _mixer = new BassPlaybackMixer();
        _padPlayer = new BassPadPlayer();

        Transport = new TransportViewModel(_mixer, _padPlayer);

        InitializeSetlist();
    }

    [RelayCommand]
    public void SelectSong(SongItemViewModel songVm)
    {
        if (SelectedSong != null)
        {
            SelectedSong.IsSelected = false;
        }

        SelectedSong = songVm;
        songVm.IsSelected = true;

        LoadSong(songVm.Model);
    }

    public void LoadSong(Song song)
    {
        CurrentSong = song;
        Channels.Clear();

        foreach (var stem in song.Stems)
        {
            var channelVm = new StemChannelViewModel(
                stem,
                onVolumeChanged: (id, vol) => _mixer.SetStemVolume(id, vol),
                onPanChanged: (id, pan) => _mixer.SetStemPan(id, pan),
                onMuteChanged: (id, mute) => _mixer.SetStemMute(id, mute),
                onSoloChanged: (id, solo) => _mixer.SetStemSolo(id, solo)
            );
            Channels.Add(channelVm);
        }

        // Build waveform sections
        ActiveSections.Clear();
        var arrangement = song.ActiveArrangement;
        if (arrangement != null)
        {
            foreach (var section in arrangement.Sections)
            {
                ActiveSections.Add(new SectionWaveformViewModel(section));
            }
        }

        // Prepare mixer and transport without auto-playing
        _mixer.LoadSong(song);
        _mixer.Stop();
        Transport.LoadSong(song);
    }

    [RelayCommand]
    public void ClearAllMutes()
    {
        foreach (var ch in Channels)
        {
            ch.IsMuted = false;
        }
    }

    [RelayCommand]
    public void ClearAllSolos()
    {
        foreach (var ch in Channels)
        {
            ch.IsSolo = false;
        }
    }

    private void InitializeSetlist()
    {
        // 1. Way Maker
        var song1 = new Song("Way Maker", 68.0, MusicalKey.E, KeyScale.Major)
        {
            Artist = "Sinach / Leeland",
            Duration = TimeSpan.FromMinutes(4.5)
        };
        PopulateStems(song1, isRock: false);
        PopulateArrangement(song1);

        // 2. Graves Into Gardens
        var song2 = new Song("Graves Into Gardens", 70.0, MusicalKey.B, KeyScale.Major)
        {
            Artist = "Elevation Worship",
            Duration = TimeSpan.FromMinutes(5.2)
        };
        PopulateStems(song2, isRock: true);
        PopulateArrangement(song2);

        // 3. Goodness of God
        var song3 = new Song("Goodness of God", 63.0, MusicalKey.Ab, KeyScale.Major)
        {
            Artist = "Bethel / Jenn Johnson",
            Duration = TimeSpan.FromMinutes(4.8)
        };
        PopulateStems(song3, isRock: false);
        PopulateArrangement(song3);

        // 4. Firm Foundation
        var song4 = new Song("Firm Foundation", 75.0, MusicalKey.C, KeyScale.Major)
        {
            Artist = "Maverick City Music",
            Duration = TimeSpan.FromMinutes(4.2)
        };
        PopulateStems(song4, isRock: true);
        PopulateArrangement(song4);

        var vm1 = new SongItemViewModel(song1, "#0EA5E9", "#6366F1"); // Sky to Indigo
        var vm2 = new SongItemViewModel(song2, "#F97316", "#DC2626"); // Orange to Red
        var vm3 = new SongItemViewModel(song3, "#10B981", "#059669"); // Emerald to Teal
        var vm4 = new SongItemViewModel(song4, "#8B5CF6", "#EC4899"); // Purple to Pink

        SetlistSongs.Add(vm1);
        SetlistSongs.Add(vm2);
        SetlistSongs.Add(vm3);
        SetlistSongs.Add(vm4);

        // Select the first song by default
        SelectSong(vm1);
    }

    private void PopulateStems(Song song, bool isRock)
    {
        song.Stems.Add(new StemTrack("Click", "", StemCategory.Click) { Volume = 0.8f });
        song.Stems.Add(new StemTrack("Guide", "", StemCategory.Guide) { Volume = 0.85f });
        song.Stems.Add(new StemTrack("Drums", "", StemCategory.Drums) { Volume = 0.95f });
        song.Stems.Add(new StemTrack("Percussion", "", StemCategory.Percussion) { Volume = 0.7f });
        song.Stems.Add(new StemTrack("Bass", "", StemCategory.Bass) { Volume = 0.9f });
        song.Stems.Add(new StemTrack("Acoustic Gtr", "", StemCategory.AcousticGuitar) { Volume = 0.8f, Pan = -0.3f });
        song.Stems.Add(new StemTrack("Elec Gtr 1", "", StemCategory.ElectricGuitar) { Volume = 0.85f, Pan = -0.6f });
        if (isRock)
        {
            song.Stems.Add(new StemTrack("Elec Gtr 2", "", StemCategory.ElectricGuitar) { Volume = 0.85f, Pan = 0.6f });
            song.Stems.Add(new StemTrack("Lead Gtr", "", StemCategory.ElectricGuitar) { Volume = 0.9f, Pan = 0.2f });
        }
        song.Stems.Add(new StemTrack("Piano", "", StemCategory.Piano) { Volume = 0.8f, Pan = -0.1f });
        song.Stems.Add(new StemTrack("Keys & Pad", "", StemCategory.Keys) { Volume = 0.75f, Pan = 0.2f });
        song.Stems.Add(new StemTrack("Strings", "", StemCategory.Strings) { Volume = 0.7f });
        song.Stems.Add(new StemTrack("BGVs", "", StemCategory.BackingVocals) { Volume = 0.75f });
    }

    private void PopulateArrangement(Song song)
    {
        var arr = song.ActiveArrangement ?? new Arrangement("Default");
        arr.Sections.Clear();

        arr.Sections.Add(new SectionMarker("INTRO", SectionType.Intro, TimeSpan.Zero, TimeSpan.FromSeconds(20), "#3B82F6"));
        arr.Sections.Add(new SectionMarker("VERSE 1", SectionType.Verse, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(60), "#10B981"));
        arr.Sections.Add(new SectionMarker("CHORUS", SectionType.Chorus, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(105), "#F59E0B"));
        arr.Sections.Add(new SectionMarker("VERSE 2", SectionType.Verse, TimeSpan.FromSeconds(105), TimeSpan.FromSeconds(145), "#10B981"));
        arr.Sections.Add(new SectionMarker("CHORUS", SectionType.Chorus, TimeSpan.FromSeconds(145), TimeSpan.FromSeconds(190), "#F59E0B"));
        arr.Sections.Add(new SectionMarker("BRIDGE", SectionType.Bridge, TimeSpan.FromSeconds(190), TimeSpan.FromSeconds(240), "#8B5CF6"));
        arr.Sections.Add(new SectionMarker("CHORUS", SectionType.Chorus, TimeSpan.FromSeconds(240), TimeSpan.FromSeconds(285), "#F59E0B"));
        arr.Sections.Add(new SectionMarker("OUTRO", SectionType.Outro, TimeSpan.FromSeconds(285), TimeSpan.FromSeconds(310), "#64748B"));
    }
}
