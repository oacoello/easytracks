using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyTracks.Core.Abstractions;
using EasyTracks.Domain.Models;

namespace EasyTracks_UI.ViewModels;

public partial class TransportViewModel : ObservableObject
{
    private readonly IPlaybackMixer _mixer;
    private readonly IPadPlayer? _padPlayer;

    [ObservableProperty]
    private string _songTitle = "No Song Loaded";

    [ObservableProperty]
    private string _artist = "";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _currentPositionSeconds;

    [ObservableProperty]
    private double _totalDurationSeconds = 1.0;

    [ObservableProperty]
    private string _currentTimeText = "00:00";

    [ObservableProperty]
    private string _totalDurationText = "00:00";

    [ObservableProperty]
    private double _bpm = 120.0;

    [ObservableProperty]
    private double _originalBpm = 120.0;

    [ObservableProperty]
    private MusicalKey _currentKey = MusicalKey.C;

    [ObservableProperty]
    private KeyScale _currentScale = KeyScale.Major;

    [ObservableProperty]
    private int _semitoneOffset = 0;

    [ObservableProperty]
    private double _masterVolume = 1.0;

    [ObservableProperty]
    private bool _isPadActive;

    [ObservableProperty]
    private string _currentSectionName = "Intro";

    public string KeyDisplay => $"{CurrentKey}{(CurrentScale == KeyScale.Minor ? "m" : "")} {(SemitoneOffset != 0 ? $"({(SemitoneOffset > 0 ? "+" : "")}{SemitoneOffset})" : "")}";

    public TransportViewModel(IPlaybackMixer mixer, IPadPlayer? padPlayer = null)
    {
        _mixer = mixer;
        _padPlayer = padPlayer;

        _mixer.StateChanged += (_, state) =>
        {
            IsPlaying = state == PlaybackEngineState.Playing;
        };

        _mixer.PositionChanged += (_, pos) =>
        {
            CurrentPositionSeconds = pos.TotalSeconds;
            CurrentTimeText = pos.ToString(@"mm\:ss");
        };
    }

    public void LoadSong(Song song)
    {
        SongTitle = song.Title;
        Artist = song.Artist;
        Bpm = song.PlaybackBpm;
        OriginalBpm = song.OriginalBpm;
        CurrentKey = song.OriginalKey;
        CurrentScale = song.OriginalScale;
        SemitoneOffset = song.KeySemitoneOffset;
        
        TotalDurationSeconds = _mixer.TotalDuration.TotalSeconds > 0 ? _mixer.TotalDuration.TotalSeconds : song.Duration.TotalSeconds;
        TotalDurationText = TimeSpan.FromSeconds(TotalDurationSeconds).ToString(@"mm\:ss");
        CurrentPositionSeconds = 0;
        CurrentTimeText = "00:00";
        OnPropertyChanged(nameof(KeyDisplay));
    }

    [RelayCommand]
    public void TogglePlayPause()
    {
        if (IsPlaying)
        {
            _mixer.Pause();
        }
        else
        {
            _mixer.Play();
        }
    }

    [RelayCommand]
    public void Stop()
    {
        _mixer.Stop();
    }

    [RelayCommand]
    public void SeekTo(double seconds)
    {
        _mixer.Seek(TimeSpan.FromSeconds(seconds));
    }

    [RelayCommand]
    public void IncreaseBpm()
    {
        Bpm = Math.Clamp(Bpm + 1, 40, 260);
        _mixer.SetTempo(Bpm, OriginalBpm);
    }

    [RelayCommand]
    public void DecreaseBpm()
    {
        Bpm = Math.Clamp(Bpm - 1, 40, 260);
        _mixer.SetTempo(Bpm, OriginalBpm);
    }

    [RelayCommand]
    public void PitchUp()
    {
        SemitoneOffset = Math.Clamp(SemitoneOffset + 1, -12, 12);
        _mixer.SetPitchSemitones(SemitoneOffset);
        OnPropertyChanged(nameof(KeyDisplay));
    }

    [RelayCommand]
    public void PitchDown()
    {
        SemitoneOffset = Math.Clamp(SemitoneOffset - 1, -12, 12);
        _mixer.SetPitchSemitones(SemitoneOffset);
        OnPropertyChanged(nameof(KeyDisplay));
    }

    [RelayCommand]
    public void TogglePad()
    {
        if (_padPlayer == null) return;

        if (IsPadActive)
        {
            _padPlayer.Stop(TimeSpan.FromSeconds(2));
            IsPadActive = false;
        }
        else
        {
            _padPlayer.PlayKey(CurrentKey, TimeSpan.FromSeconds(2));
            IsPadActive = true;
        }
    }

    partial void OnMasterVolumeChanged(double value)
    {
        _mixer.SetMasterVolume((float)value);
    }
}
