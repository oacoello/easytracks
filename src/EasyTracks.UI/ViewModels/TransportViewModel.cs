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
    private Microsoft.UI.Xaml.Media.SolidColorBrush _coverBrush = new(Windows.UI.Color.FromArgb(255, 59, 130, 246));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCoverImage))]
    [NotifyPropertyChangedFor(nameof(CoverImageVisibility))]
    [NotifyPropertyChangedFor(nameof(CoverIconVisibility))]
    [NotifyPropertyChangedFor(nameof(CoverImageSource))]
    private string? _coverImageUri;

    public bool HasCoverImage => !string.IsNullOrEmpty(CoverImageUri);
    public Microsoft.UI.Xaml.Visibility CoverImageVisibility => HasCoverImage ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility CoverIconVisibility => HasCoverImage ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    public Microsoft.UI.Xaml.Media.ImageSource? CoverImageSource
    {
        get
        {
            if (string.IsNullOrEmpty(CoverImageUri)) return null;
            try
            {
                return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(CoverImageUri));
            }
            catch
            {
                return null;
            }
        }
    }

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
    private string _timeSignature = "4/4";

    [ObservableProperty]
    private int _timeSignatureNumerator = 4;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BarBeatDisplay))]
    private int _currentBar = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BarBeatDisplay))]
    [NotifyPropertyChangedFor(nameof(Beat1Brush))]
    [NotifyPropertyChangedFor(nameof(Beat2Brush))]
    [NotifyPropertyChangedFor(nameof(Beat3Brush))]
    [NotifyPropertyChangedFor(nameof(Beat4Brush))]
    [NotifyPropertyChangedFor(nameof(Beat1TextBrush))]
    [NotifyPropertyChangedFor(nameof(Beat2TextBrush))]
    [NotifyPropertyChangedFor(nameof(Beat3TextBrush))]
    [NotifyPropertyChangedFor(nameof(Beat4TextBrush))]
    private int _currentBeat = 1;

    [ObservableProperty]
    private bool _isStageView;

    public string BarBeatDisplay => $"COMPÁS {CurrentBar:D2}  •  TIEMPO {CurrentBeat}";

    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush DownbeatActiveBrush = new(Windows.UI.Color.FromArgb(255, 6, 182, 212));   // Cyan 500
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BeatActiveBrush = new(Windows.UI.Color.FromArgb(255, 16, 185, 129));     // Emerald 500
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BeatInactiveBrush = new(Windows.UI.Color.FromArgb(255, 30, 41, 59));     // Slate 800
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush ActiveTextBrush = new(Windows.UI.Color.FromArgb(255, 255, 255, 255));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush InactiveTextBrush = new(Windows.UI.Color.FromArgb(255, 100, 116, 139));

    public Microsoft.UI.Xaml.Media.SolidColorBrush Beat1Brush => IsPlaying && CurrentBeat == 1 ? DownbeatActiveBrush : BeatInactiveBrush;
    public Microsoft.UI.Xaml.Media.SolidColorBrush Beat2Brush => IsPlaying && CurrentBeat == 2 ? BeatActiveBrush : BeatInactiveBrush;
    public Microsoft.UI.Xaml.Media.SolidColorBrush Beat3Brush => IsPlaying && CurrentBeat == 3 ? BeatActiveBrush : BeatInactiveBrush;
    public Microsoft.UI.Xaml.Media.SolidColorBrush Beat4Brush => IsPlaying && CurrentBeat >= 4 ? BeatActiveBrush : BeatInactiveBrush;

    public Microsoft.UI.Xaml.Media.SolidColorBrush Beat1TextBrush => IsPlaying && CurrentBeat == 1 ? ActiveTextBrush : InactiveTextBrush;
    public Microsoft.UI.Xaml.Media.SolidColorBrush Beat2TextBrush => IsPlaying && CurrentBeat == 2 ? ActiveTextBrush : InactiveTextBrush;
    public Microsoft.UI.Xaml.Media.SolidColorBrush Beat3TextBrush => IsPlaying && CurrentBeat == 3 ? ActiveTextBrush : InactiveTextBrush;
    public Microsoft.UI.Xaml.Media.SolidColorBrush Beat4TextBrush => IsPlaying && CurrentBeat >= 4 ? ActiveTextBrush : InactiveTextBrush;

    [ObservableProperty]
    private bool _isFadingOut;

    [ObservableProperty]
    private bool _isLoopActive;

    [ObservableProperty]
    private string _currentSectionName = "Intro";

    [ObservableProperty]
    private string _loopStatusText = "";

    [ObservableProperty]
    private double _masterMeterHeight;

    [ObservableProperty]
    private double _padTransitionSeconds = 3.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PadVolumePercentText))]
    private double _padVolume = 0.75;

    public string PadVolumePercentText => $"{(int)Math.Round(PadVolume * 100)}%";

    private readonly EasyTracks.Core.Services.LibraryStorageService _storage = new();

    public string KeyDisplay => $"{CurrentKey.ToDisplayString()}{(CurrentScale == KeyScale.Minor ? "m" : "")}{(SemitoneOffset != 0 ? $" ({(SemitoneOffset > 0 ? "+" : "")}{SemitoneOffset})" : "")}";

    private readonly Action? _onNextSong;
    private readonly Action? _onPreviousSong;

    public TransportViewModel(IPlaybackMixer mixer, IPadPlayer? padPlayer = null, Action? onNextSong = null, Action? onPreviousSong = null)
    {
        _mixer = mixer;
        _padPlayer = padPlayer;
        _onNextSong = onNextSong;
        _onPreviousSong = onPreviousSong;

        double savedPadVol = _storage.LoadPadVolume();
        _padVolume = savedPadVol;
        if (_padPlayer != null)
        {
            _padPlayer.Volume = (float)savedPadVol;
        }

        _mixer.StateChanged += (_, state) =>
        {
            void ApplyState()
            {
                IsPlaying = state == PlaybackEngineState.Playing;
                if (_mixer.TotalDuration.TotalSeconds > 0 && Math.Abs(TotalDurationSeconds - _mixer.TotalDuration.TotalSeconds) > 0.5)
                {
                    TotalDurationSeconds = _mixer.TotalDuration.TotalSeconds;
                    TotalDurationText = _mixer.TotalDuration.ToString(@"mm\:ss");
                }
                OnPropertyChanged(nameof(Beat1Brush));
                OnPropertyChanged(nameof(Beat2Brush));
                OnPropertyChanged(nameof(Beat3Brush));
                OnPropertyChanged(nameof(Beat4Brush));
                OnPropertyChanged(nameof(Beat1TextBrush));
                OnPropertyChanged(nameof(Beat2TextBrush));
                OnPropertyChanged(nameof(Beat3TextBrush));
                OnPropertyChanged(nameof(Beat4TextBrush));
            }

            var dq = EasyTracks_UI.App.MainWindowInstance?.DispatcherQueue;
            if (dq != null && !dq.HasThreadAccess)
                dq.TryEnqueue(ApplyState);
            else
                ApplyState();
        };

        _mixer.PositionChanged += (_, pos) =>
        {
            void ApplyPosition()
            {
                if (_mixer.TotalDuration.TotalSeconds > 0 && Math.Abs(TotalDurationSeconds - _mixer.TotalDuration.TotalSeconds) > 0.5)
                {
                    TotalDurationSeconds = _mixer.TotalDuration.TotalSeconds;
                    TotalDurationText = _mixer.TotalDuration.ToString(@"mm\:ss");
                }
                CurrentPositionSeconds = pos.TotalSeconds;
                CurrentTimeText = pos.ToString(@"mm\:ss");
                UpdateBarAndBeat(pos.TotalSeconds);
            }

            var dq = EasyTracks_UI.App.MainWindowInstance?.DispatcherQueue;
            if (dq != null && !dq.HasThreadAccess)
                dq.TryEnqueue(ApplyPosition);
            else
                ApplyPosition();
        };
    }

    partial void OnPadVolumeChanged(double value)
    {
        double clamped = Math.Clamp(value, 0.0, 1.0);
        if (_padPlayer != null)
        {
            _padPlayer.Volume = (float)clamped;
        }
        _storage.SavePadVolume(clamped);
    }

    private void UpdateBarAndBeat(double positionSeconds)
    {
        double activeBpm = Bpm > 20 ? Bpm : 120.0;
        int beatsPerBar = TimeSignatureNumerator > 0 ? TimeSignatureNumerator : 4;
        double secondsPerBeat = 60.0 / activeBpm;
        if (beatsPerBar == 6) secondsPerBeat /= 2.0;

        int totalBeatsElapsed = Math.Max(0, (int)Math.Floor(positionSeconds / secondsPerBeat));
        CurrentBar = (totalBeatsElapsed / beatsPerBar) + 1;
        CurrentBeat = (totalBeatsElapsed % beatsPerBar) + 1;
    }

    [RelayCommand]
    public void NextSong() => _onNextSong?.Invoke();

    [RelayCommand]
    public void PreviousSong() => _onPreviousSong?.Invoke();

    public void LoadSong(Song song)
    {
        SongTitle = song.Title;
        Artist = song.Artist;
        Bpm = song.PlaybackBpm;
        OriginalBpm = song.OriginalBpm;
        CurrentKey = song.OriginalKey;
        CurrentScale = song.OriginalScale;
        PadKey = song.OriginalKey;
        SemitoneOffset = song.KeySemitoneOffset;
        TimeSignatureNumerator = song.TimeSignatureNumerator > 0 ? song.TimeSignatureNumerator : 4;
        int denom = song.TimeSignatureDenominator > 0 ? song.TimeSignatureDenominator : 4;
        TimeSignature = $"{TimeSignatureNumerator}/{denom}";
        
        TotalDurationSeconds = _mixer.TotalDuration.TotalSeconds > 0 ? _mixer.TotalDuration.TotalSeconds : song.Duration.TotalSeconds;
        TotalDurationText = TimeSpan.FromSeconds(TotalDurationSeconds).ToString(@"mm\:ss");
        CoverImageUri = song.CoverUrl;
        CurrentPositionSeconds = 0;
        CurrentTimeText = "00:00";
        CurrentBar = 1;
        CurrentBeat = 1;
        OnPropertyChanged(nameof(KeyDisplay));
        OnPropertyChanged(nameof(PadKeyDisplay));

        if (IsPadActive && _padPlayer != null)
        {
            _padPlayer.PlayKey(PadKey, TimeSpan.FromSeconds(PadTransitionSeconds));
        }
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
    public void Play()
    {
        if (!IsPlaying)
        {
            _mixer.Play();
        }
    }

    [RelayCommand]
    public void Pause()
    {
        if (IsPlaying)
        {
            _mixer.Pause();
        }
    }

    [RelayCommand]
    public void Stop()
    {
        _mixer.Stop();
        IsFadingOut = false;
        CurrentPositionSeconds = 0;
        CurrentTimeText = "00:00";
        CurrentBar = 1;
        CurrentBeat = 1;
    }

    [RelayCommand]
    public async Task FadeOut()
    {
        if (!IsPlaying) return;
        IsFadingOut = true;
        double startVol = MasterVolume;
        for (int i = 10; i >= 0; i--)
        {
            MasterVolume = (startVol * i) / 10.0;
            await Task.Delay(200);
        }
        Stop();
        MasterVolume = startVol;
        IsFadingOut = false;
    }

    [RelayCommand]
    public void ToggleLoop()
    {
        IsLoopActive = !IsLoopActive;
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

    [ObservableProperty]
    private MusicalKey _padKey = MusicalKey.C;

    public string PadKeyDisplay => PadKey.ToDisplayString();

    public IReadOnlyList<MusicalKey> AvailableKeys { get; } = new List<MusicalKey>
    {
        MusicalKey.C, MusicalKey.CSharp, MusicalKey.D, MusicalKey.DSharp,
        MusicalKey.E, MusicalKey.F, MusicalKey.FSharp, MusicalKey.G,
        MusicalKey.GSharp, MusicalKey.A, MusicalKey.ASharp, MusicalKey.B
    };

    partial void OnIsPadActiveChanged(bool value)
    {
        if (_padPlayer == null) return;
        if (value)
        {
            _padPlayer.PlayKey(PadKey, TimeSpan.FromSeconds(PadTransitionSeconds));
        }
        else
        {
            _padPlayer.Stop(TimeSpan.FromSeconds(PadTransitionSeconds));
        }
    }

    [RelayCommand]
    public void TogglePad()
    {
        IsPadActive = !IsPadActive;
    }

    [RelayCommand]
    public void SelectPadKey(MusicalKey key)
    {
        PadKey = key;
        OnPropertyChanged(nameof(PadKeyDisplay));

        if (IsPadActive && _padPlayer != null)
        {
            _padPlayer.PlayKey(PadKey, TimeSpan.FromSeconds(PadTransitionSeconds));
        }
    }

    [RelayCommand]
    public void PreviousPadKey()
    {
        int currentIndex = AvailableKeys.ToList().IndexOf(PadKey);
        int newIndex = (currentIndex - 1 + AvailableKeys.Count) % AvailableKeys.Count;
        SelectPadKey(AvailableKeys[newIndex]);
    }

    [RelayCommand]
    public void NextPadKey()
    {
        int currentIndex = AvailableKeys.ToList().IndexOf(PadKey);
        int newIndex = (currentIndex + 1) % AvailableKeys.Count;
        SelectPadKey(AvailableKeys[newIndex]);
    }

    partial void OnMasterVolumeChanged(double value)
    {
        _mixer.SetMasterVolume((float)value);
    }
}
