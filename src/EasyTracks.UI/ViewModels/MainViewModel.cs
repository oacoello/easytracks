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

    [ObservableProperty]
    private Song? _currentSong;

    [ObservableProperty]
    private bool _isMuteAllActive;

    [ObservableProperty]
    private bool _isSoloClearActive;

    public MainViewModel()
    {
        _categoryDetector = new StemCategoryDetector();
        
        // In real app / DI container, these are injected as singletons
        _audioEngine = new BassAudioEngine();
        _audioEngine.Initialize();

        _mixer = new BassPlaybackMixer();
        _padPlayer = new BassPadPlayer();

        Transport = new TransportViewModel(_mixer, _padPlayer);

        LoadMockDemoSession();
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

        _mixer.LoadSong(song);
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

    private void LoadMockDemoSession()
    {
        var demoSong = new Song("Way Maker", 68.0, MusicalKey.E, KeyScale.Major)
        {
            Artist = "Sinach / Leeland",
            Duration = TimeSpan.FromMinutes(4.5)
        };

        // Add typical live worship stems
        demoSong.Stems.Add(new StemTrack("Click", "", StemCategory.Click) { Volume = 0.8f });
        demoSong.Stems.Add(new StemTrack("Guide", "", StemCategory.Guide) { Volume = 0.85f });
        demoSong.Stems.Add(new StemTrack("Drums", "", StemCategory.Drums) { Volume = 0.95f });
        demoSong.Stems.Add(new StemTrack("Percussion", "", StemCategory.Percussion) { Volume = 0.7f });
        demoSong.Stems.Add(new StemTrack("Bass", "", StemCategory.Bass) { Volume = 0.9f });
        demoSong.Stems.Add(new StemTrack("Acoustic Gtr", "", StemCategory.AcousticGuitar) { Volume = 0.8f, Pan = -0.3f });
        demoSong.Stems.Add(new StemTrack("Elec Gtr 1", "", StemCategory.ElectricGuitar) { Volume = 0.85f, Pan = -0.6f });
        demoSong.Stems.Add(new StemTrack("Elec Gtr 2", "", StemCategory.ElectricGuitar) { Volume = 0.85f, Pan = 0.6f });
        demoSong.Stems.Add(new StemTrack("Piano", "", StemCategory.Piano) { Volume = 0.8f, Pan = -0.1f });
        demoSong.Stems.Add(new StemTrack("Keys & Pad", "", StemCategory.Keys) { Volume = 0.75f, Pan = 0.2f });
        demoSong.Stems.Add(new StemTrack("Strings", "", StemCategory.Strings) { Volume = 0.7f });
        demoSong.Stems.Add(new StemTrack("BGVs", "", StemCategory.BackingVocals) { Volume = 0.75f });

        LoadSong(demoSong);
    }
}
