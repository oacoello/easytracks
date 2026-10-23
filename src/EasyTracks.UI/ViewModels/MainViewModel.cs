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
    private readonly MultitrackAudioService _importer;
    private readonly System.Timers.Timer _meterTimer;
    private readonly EasyTracks.Core.Services.LibraryStorageService _storage = new();

    public TransportViewModel Transport { get; }
    public ObservableCollection<StemChannelViewModel> Channels { get; } = new();
    public ObservableCollection<SongItemViewModel> SetlistSongs { get; } = new();
    public ObservableCollection<SectionWaveformViewModel> ActiveSections { get; } = new();
    public ObservableCollection<InstrumentBusViewModel> InstrumentBuses { get; } = new();

    public SetlistManagerViewModel SetlistManager { get; }

    [ObservableProperty]
    private SongItemViewModel? _selectedSong;

    [ObservableProperty]
    private Song? _currentSong;

    [ObservableProperty]
    private string _currentTab = "Live"; // "Live", "Setlist", "Songs", "Settings"

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArrangementEditorVisibility))]
    private bool _isArrangementEditMode;

    public Microsoft.UI.Xaml.Visibility ArrangementEditorVisibility => IsArrangementEditMode
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSectionNameText))]
    [NotifyPropertyChangedFor(nameof(SelectedSectionBarsText))]
    private SectionWaveformViewModel? _selectedSectionForEdit;

    public string SelectedSectionNameText => SelectedSectionForEdit?.Name ?? "Ninguna sección seleccionada";
    public string SelectedSectionBarsText => SelectedSectionForEdit?.TimeRangeText ?? "Haz clic en una sección para editar";

    [ObservableProperty]
    private bool _isSnapToGridEnabled = true;

    // Hardware Audio & Mackie DL16S Routing
    public ObservableCollection<AudioDeviceInfo> AudioDevices { get; } = new();

    [ObservableProperty]
    private AudioDeviceInfo? _selectedAudioDevice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoutingModeDisplayText))]
    [NotifyPropertyChangedFor(nameof(IsMultichannelMode))]
    private AudioRoutingMode _routingMode = AudioRoutingMode.Multichannel16Ch;

    public bool IsMultichannelMode => RoutingMode == AudioRoutingMode.Multichannel16Ch;

    public string RoutingModeDisplayText => RoutingMode == AudioRoutingMode.Multichannel16Ch
        ? "Modo 16 Canales USB Directos (Mackie DL16S)"
        : "Modo 2 Canales (Estéreo / Split In-Ears L + FOH R)";

    [ObservableProperty]
    private string _mackieStatusText = "Modo Pre-Configuración 16-Ch listo (Conecta Mackie DL16S por USB para enlace directo)";

    [ObservableProperty]
    private Microsoft.UI.Xaml.Media.SolidColorBrush _mackieStatusBrush = new(Windows.UI.Color.FromArgb(255, 56, 189, 248));

    private DateTime _lastLoopSeekTime = DateTime.MinValue;

    // Waveform Zoom Presets (x1 = 1.0 fits window width, x2 = 2.0x, x3 = 3.0x)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsZoom1x))]
    [NotifyPropertyChangedFor(nameof(IsZoom2x))]
    [NotifyPropertyChangedFor(nameof(IsZoom3x))]
    private double _waveformZoomFactor = 1.0;

    public bool IsZoom1x => Math.Abs(WaveformZoomFactor - 1.0) < 0.05;
    public bool IsZoom2x => Math.Abs(WaveformZoomFactor - 2.0) < 0.05;
    public bool IsZoom3x => Math.Abs(WaveformZoomFactor - 3.0) < 0.05;

    private double _waveformViewportWidth = 1100.0;

    public void SetWaveformViewportWidth(double viewportWidth)
    {
        if (viewportWidth > 200 && Math.Abs(_waveformViewportWidth - viewportWidth) > 2.0)
        {
            _waveformViewportWidth = viewportWidth;
            UpdateSectionWidthsForCurrentZoom();
        }
    }

    [RelayCommand]
    public void SetZoom1x()
    {
        WaveformZoomFactor = 1.0;
        UpdateSectionWidthsForCurrentZoom();
    }

    [RelayCommand]
    public void SetZoom2x()
    {
        WaveformZoomFactor = 2.0;
        UpdateSectionWidthsForCurrentZoom();
    }

    [RelayCommand]
    public void SetZoom3x()
    {
        WaveformZoomFactor = 3.0;
        UpdateSectionWidthsForCurrentZoom();
    }

    public void UpdateSectionWidthsForCurrentZoom()
    {
        if (ActiveSections.Count == 0) return;

        double baseWidth = Math.Max(400.0, _waveformViewportWidth - 8.0);
        double totalTargetWidth = baseWidth * WaveformZoomFactor;
        double totalDuration = ActiveSections.Sum(s => Math.Max(0.5, s.DurationSeconds));
        if (totalDuration <= 0.1) totalDuration = 180.0;

        foreach (var sec in ActiveSections)
        {
            double ratio = Math.Max(0.5, sec.DurationSeconds) / totalDuration;
            double secWidth = ActiveSections.Count == 1
                ? totalTargetWidth
                : Math.Max(76.0, ratio * totalTargetWidth);
            sec.UpdateSectionWidthAndBars(secWidth);
        }
    }

    public Services.LocalizationService Strings => Services.LocalizationService.Instance;

    public ObservableCollection<Song> LocalLibrarySongs { get; } = new();
    public ObservableCollection<SongItemViewModel> AllLibrarySongItems { get; } = new();
    public ObservableCollection<SongItemViewModel> FilteredLibrarySongs { get; } = new();

    [ObservableProperty]
    private string _songSearchQuery = "";

    partial void OnSongSearchQueryChanged(string value)
    {
        RefreshFilteredSongs();
    }

    public void RefreshFilteredSongs()
    {
        string q = (SongSearchQuery ?? "").Trim();
        FilteredLibrarySongs.Clear();

        foreach (var item in AllLibrarySongItems)
        {
            if (string.IsNullOrEmpty(q) ||
                item.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                item.Artist.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                item.ArrangementDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                item.KeyDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                item.BpmDisplay.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                item.TimeSignatureDisplay.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                FilteredLibrarySongs.Add(item);
            }
        }
    }

    public string DedicatedTracksDirectory { get; private set; } = "";
    public string DedicatedPadsDirectory { get; private set; } = "";

    public MainViewModel()
    {
        _categoryDetector = new StemCategoryDetector();
        _importer = new MultitrackAudioService();
        
        _audioEngine = new BassAudioEngine();
        _audioEngine.Initialize();
        _audioEngine.MackieConsoleConnected += OnMackieConsoleConnected;

        _mixer = new BassPlaybackMixer();
        _mixer.TrackEnded += OnTrackEnded;
        _mixer.PositionChanged += OnMixerPositionChanged;
        _padPlayer = new BassPadPlayer();

        Transport = new TransportViewModel(_mixer, _padPlayer, onNextSong: SelectNextSong, onPreviousSong: SelectPreviousSong);
        SetlistManager = new SetlistManagerViewModel(this);

        InitializeBuses();
        RefreshAudioDevices();

        _meterTimer = new System.Timers.Timer(33); // ~30 fps VU meter update
        _meterTimer.Elapsed += (_, _) => UpdateVUMeters();
        _meterTimer.Start();

        InitializeSetlist();
    }

    private void OnMackieConsoleConnected(object? sender, AudioDeviceInfo mackieDevice)
    {
        var dispatcher = App.MainWindowInstance?.DispatcherQueue;
        dispatcher?.TryEnqueue(() =>
        {
            RefreshAudioDevices();
            var match = AudioDevices.FirstOrDefault(d => d.Id == mackieDevice.Id);
            if (match != null)
            {
                SelectedAudioDevice = match;
                RoutingMode = AudioRoutingMode.Multichannel16Ch;
            }
        });
    }

    partial void OnSelectedAudioDeviceChanged(AudioDeviceInfo? value)
    {
        if (value == null) return;
        _audioEngine.SetOutputDevice(value.Id);

        if (value.IsMackieConsole && value.Id >= 0)
        {
            MackieStatusText = $"Conectada en vivo: {value.Name} (16 Canales USB Activos)";
            MackieStatusBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129));
            RoutingMode = AudioRoutingMode.Multichannel16Ch;
        }
        else if (value.IsMackieConsole && value.Id < 0)
        {
            MackieStatusText = "Pre-Configuración Mackie DL16S (16-Ch listos • Audio monitoreado en salida de Windows)";
            MackieStatusBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 56, 189, 248));
        }
        else
        {
            MackieStatusText = $"Salida Estéreo Activa: {value.Name} (Usa Split L/R o Estéreo 2-Ch)";
            MackieStatusBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 148, 163, 184));
        }
    }

    [RelayCommand]
    public void RefreshAudioDevices()
    {
        var devices = _audioEngine.GetOutputDevices();
        AudioDevices.Clear();
        foreach (var d in devices)
        {
            AudioDevices.Add(d);
        }

        // Prefer real Mackie DL16S if plugged in, otherwise default device
        var realMackie = AudioDevices.FirstOrDefault(d => d.IsMackieConsole && d.Id >= 0);
        SelectedAudioDevice = realMackie
            ?? AudioDevices.FirstOrDefault(d => d.IsDefault)
            ?? AudioDevices.FirstOrDefault();
    }

    [RelayCommand]
    public void ApplyMackie16ChPreset()
    {
        RoutingMode = AudioRoutingMode.Multichannel16Ch;
        var mackieDev = AudioDevices.FirstOrDefault(d => d.IsMackieConsole);
        if (mackieDev != null) SelectedAudioDevice = mackieDev;

        foreach (var ch in Channels)
        {
            ch.OutputBusIndex = StemCategoryDetector.GetDefaultMackieOutputBus(ch.Category);
        }
        SaveData();
    }

    [RelayCommand]
    public void ApplySplit2ChPreset()
    {
        RoutingMode = AudioRoutingMode.Stereo2Ch;
        foreach (var ch in Channels)
        {
            if (ch.Category == StemCategory.Click || ch.Category == StemCategory.Guide)
            {
                ch.OutputBusIndex = -1; // In-Ears (Left)
                ch.Pan = -1.0;
            }
            else
            {
                ch.OutputBusIndex = -2; // FOH (Right)
                ch.Pan = 1.0;
            }
        }
        SaveData();
    }

    [RelayCommand]
    public void ApplyStereoMasterPreset()
    {
        RoutingMode = AudioRoutingMode.Stereo2Ch;
        foreach (var ch in Channels)
        {
            ch.OutputBusIndex = 0; // Out 1-2 Stereo
            ch.Pan = 0.0;
        }
        SaveData();
    }

    private void OnMixerPositionChanged(object? sender, TimeSpan pos)
    {
        var dispatcher = App.MainWindowInstance?.DispatcherQueue;
        if (dispatcher == null) return;

        dispatcher.TryEnqueue(() =>
        {
            if (ActiveSections.Count == 0) return;

            double curSec = pos.TotalSeconds;

            // 1. Check active section loop
            var loopingSection = ActiveSections.FirstOrDefault(s => s.IsLooping);
            if (loopingSection != null && Transport.IsPlaying)
            {
                double loopEnd = loopingSection.Model.EndTime.TotalSeconds;
                double loopStart = loopingSection.Model.StartTime.TotalSeconds;
                if (curSec >= loopEnd - 0.06 && (DateTime.UtcNow - _lastLoopSeekTime).TotalMilliseconds > 180)
                {
                    _lastLoopSeekTime = DateTime.UtcNow;
                    loopingSection.LoopPassCount++;
                    _mixer.Seek(TimeSpan.FromSeconds(loopStart));
                    Transport.LoopStatusText = $"LOOP: {loopingSection.Name} (x{loopingSection.LoopPassCount})";
                    return;
                }
            }
            else if (loopingSection == null && !string.IsNullOrEmpty(Transport.LoopStatusText))
            {
                Transport.LoopStatusText = "";
            }

            // 2. Highlight current active section
            SectionWaveformViewModel? currentActive = null;
            foreach (var sec in ActiveSections)
            {
                bool active = curSec >= sec.Model.StartTime.TotalSeconds && curSec < sec.Model.EndTime.TotalSeconds;
                sec.IsActive = active;
                if (active) currentActive = sec;
            }

            if (currentActive != null && Transport.CurrentSectionName != currentActive.Name)
            {
                Transport.CurrentSectionName = currentActive.Name;
            }
        });
    }

    public void SelectNextSong()
    {
        if (SelectedSong == null || SetlistSongs.Count == 0) return;
        int idx = SetlistSongs.IndexOf(SelectedSong);
        if (idx >= 0 && idx < SetlistSongs.Count - 1)
        {
            SelectSong(SetlistSongs[idx + 1]);
        }
    }

    public void SelectPreviousSong()
    {
        if (SelectedSong == null || SetlistSongs.Count == 0) return;
        int idx = SetlistSongs.IndexOf(SelectedSong);
        if (idx > 0)
        {
            SelectSong(SetlistSongs[idx - 1]);
        }
    }

    private void OnTrackEnded(object? sender, EventArgs e)
    {
        var dispatcher = App.MainWindowInstance?.DispatcherQueue;
        if (dispatcher == null) return;

        dispatcher.TryEnqueue(() =>
        {
            if (SelectedSong == null) return;

            var transition = SelectedSong.Transition;
            switch (transition)
            {
                case TransitionType.ManualStop:
                    Transport.Stop();
                    break;

                case TransitionType.Wait:
                    SelectNextSong();
                    Transport.Stop();
                    break;

                case TransitionType.AutoPlay:
                case TransitionType.Crossfade:
                    SelectNextSong();
                    Transport.Play();
                    break;

                case TransitionType.Hold:
                    SelectNextSong();
                    Transport.Stop();
                    if (!Transport.IsPadActive)
                    {
                        Transport.TogglePad();
                    }
                    else if (SelectedSong != null)
                    {
                        Transport.SelectPadKey(SelectedSong.Model.OriginalKey);
                    }
                    break;
            }
        });
    }

    public void SaveData()
    {
        try
        {
            SetlistManager.SyncCurrentSetlistItems();
            _storage.SaveLibrarySongs(LocalLibrarySongs);
            _storage.SaveSetlists(SetlistManager.AllSetlists);
        }
        catch { }
    }

    private void InitializeBuses()
    {
        InstrumentBuses.Add(new InstrumentBusViewModel(InstrumentBusType.Drums, "DRUMS & PERC", "#EAB308", OnBusMuteChanged, OnBusOutputChanged));
        InstrumentBuses.Add(new InstrumentBusViewModel(InstrumentBusType.Guitars, "GUITARS", "#22C55E", OnBusMuteChanged, OnBusOutputChanged));
        InstrumentBuses.Add(new InstrumentBusViewModel(InstrumentBusType.Keys, "KEYS & SYNTHS", "#6366F1", OnBusMuteChanged, OnBusOutputChanged));
        InstrumentBuses.Add(new InstrumentBusViewModel(InstrumentBusType.Vocals, "VOCALS", "#EC4899", OnBusMuteChanged, OnBusOutputChanged));
    }

    private void OnBusMuteChanged(InstrumentBusType busType, bool isMuted)
    {
        foreach (var channel in Channels)
        {
            if (MatchesBus(channel.Category, busType))
            {
                channel.IsMuted = isMuted;
            }
        }
    }

    private void OnBusOutputChanged(InstrumentBusType busType, int outputBusIndex)
    {
        foreach (var channel in Channels)
        {
            if (MatchesBus(channel.Category, busType))
            {
                channel.OutputBusIndex = outputBusIndex;
            }
        }
        SaveData();
    }

    private static bool MatchesBus(StemCategory cat, InstrumentBusType bus) => bus switch
    {
        InstrumentBusType.Drums => cat == StemCategory.Drums || cat == StemCategory.Percussion,
        InstrumentBusType.Guitars => cat == StemCategory.ElectricGuitar || cat == StemCategory.AcousticGuitar || cat == StemCategory.Bass,
        InstrumentBusType.Keys => cat == StemCategory.Piano || cat == StemCategory.Keys || cat == StemCategory.Synth || cat == StemCategory.Strings,
        InstrumentBusType.Vocals => cat == StemCategory.LeadVocal || cat == StemCategory.BackingVocals || cat == StemCategory.Choir,
        _ => false
    };

    private void RefreshBusCounts()
    {
        foreach (var bus in InstrumentBuses)
        {
            bus.StemCount = Channels.Count(c => MatchesBus(c.Category, bus.BusType));
        }
    }

    private void UpdateVUMeters()
    {
        var dispatcher = App.MainWindowInstance?.DispatcherQueue;
        if (dispatcher == null) return;

        dispatcher.TryEnqueue(() =>
        {
            if (Transport.IsPlaying)
            {
                foreach (var ch in Channels)
                {
                    float level = 0f;
                    if (!ch.IsMuted && ch.Volume > 0.001)
                    {
                        level = _mixer.GetStemPeakLevel(ch.Id);
                    }

                    // Fast attack, smooth decay on 100% real audio data
                    if (level > ch.PeakLevel)
                    {
                        ch.PeakLevel = level;
                    }
                    else
                    {
                        ch.PeakLevel = Math.Max(0.0, ch.PeakLevel * 0.85);
                    }
                }

                var master = _mixer.GetMasterPeakLevel();
                double masterPeak = Math.Max(master.Left, master.Right);
                double targetMasterHeight = masterPeak * 115.0;

                if (targetMasterHeight > Transport.MasterMeterHeight)
                {
                    Transport.MasterMeterHeight = Math.Clamp(targetMasterHeight, 0.0, 115.0);
                }
                else
                {
                    Transport.MasterMeterHeight = Math.Max(0.0, Transport.MasterMeterHeight * 0.85);
                }
            }
            else
            {
                foreach (var ch in Channels)
                {
                    if (ch.PeakLevel > 0.01) ch.PeakLevel *= 0.7;
                    else ch.PeakLevel = 0;
                }

                if (Transport.MasterMeterHeight > 0.5)
                {
                    Transport.MasterMeterHeight *= 0.7;
                }
                else
                {
                    Transport.MasterMeterHeight = 0;
                }
            }
        });
    }

    partial void OnIsEditModeChanged(bool value)
    {
        foreach (var song in SetlistSongs)
        {
            song.IsEditMode = value;
        }
    }

    [RelayCommand]
    public void ToggleArrangementEditMode()
    {
        IsArrangementEditMode = !IsArrangementEditMode;
        if (IsArrangementEditMode && SelectedSectionForEdit == null && ActiveSections.Count > 0)
        {
            SelectedSectionForEdit = ActiveSections[0];
        }
    }

    public void SelectSectionForEdit(SectionWaveformViewModel sectionVm)
    {
        SelectedSectionForEdit = sectionVm;
        OnPropertyChanged(nameof(SelectedSectionNameText));
        OnPropertyChanged(nameof(SelectedSectionBarsText));
    }

    public void ToggleExclusiveSectionLoop(SectionWaveformViewModel targetSection)
    {
        bool newLoopState = !targetSection.IsLooping;
        foreach (var sec in ActiveSections)
        {
            sec.IsLooping = false;
        }
        targetSection.IsLooping = newLoopState;
        if (newLoopState)
        {
            targetSection.LoopPassCount = 1;
            Transport.IsLoopActive = true;
            Transport.LoopStatusText = $"LOOP: {targetSection.Name} (x1)";
        }
        else
        {
            Transport.IsLoopActive = false;
            Transport.LoopStatusText = "";
        }
    }

    public (int BarNumber, double SnappedTimeSeconds) GetSnappedBarAtTime(double targetSeconds)
    {
        if (CurrentSong == null) return (1, 0.0);
        double bpm = CurrentSong.PlaybackBpm > 20 ? CurrentSong.PlaybackBpm : 120.0;
        int beatsPerBar = CurrentSong.TimeSignatureNumerator > 0 ? CurrentSong.TimeSignatureNumerator : 4;
        double secPerBar = SectionWaveformViewModel.GetSecondsPerBar(bpm, beatsPerBar);

        int barIndex = Math.Max(0, (int)Math.Round(targetSeconds / secPerBar));
        double snappedSec = barIndex * secPerBar;
        return (barIndex + 1, snappedSec);
    }

    public void SplitSectionAtTime(SectionWaveformViewModel targetSection, double splitTimeSeconds, string newSectionName)
    {
        if (CurrentSong == null || ActiveSections.Count == 0) return;
        int idx = ActiveSections.IndexOf(targetSection);
        if (idx < 0) return;

        double bpm = CurrentSong.PlaybackBpm > 20 ? CurrentSong.PlaybackBpm : 120.0;
        int beatsPerBar = CurrentSong.TimeSignatureNumerator > 0 ? CurrentSong.TimeSignatureNumerator : 4;
        double secPerBar = SectionWaveformViewModel.GetSecondsPerBar(bpm, beatsPerBar);

        double snappedSplit = IsSnapToGridEnabled
            ? Math.Round(splitTimeSeconds / secPerBar) * secPerBar
            : splitTimeSeconds;

        double minStart = targetSection.Model.StartTime.TotalSeconds + Math.Min(secPerBar, 0.5);
        double maxEnd = targetSection.Model.EndTime.TotalSeconds - Math.Min(secPerBar, 0.5);
        if (snappedSplit <= minStart || snappedSplit >= maxEnd) return;

        var originalEnd = targetSection.Model.EndTime;
        var splitPoint = TimeSpan.FromSeconds(snappedSplit);

        targetSection.Model.EndTime = splitPoint;
        targetSection.RefreshMetrics(bpm, beatsPerBar);

        string cleanName = string.IsNullOrWhiteSpace(newSectionName) ? "SECCIÓN" : newSectionName.Trim().ToUpperInvariant();
        var (secType, colorHex) = InferSectionTypeAndColor(cleanName);

        var newMarker = new SectionMarker(cleanName, secType, splitPoint, originalEnd, colorHex);
        var arrangement = CurrentSong.ActiveArrangement;
        if (arrangement != null && idx + 1 <= arrangement.Sections.Count)
        {
            arrangement.Sections.Insert(idx + 1, newMarker);
        }

        RebuildActiveSectionsWithPeaks(CurrentSong);
        if (idx + 1 < ActiveSections.Count)
        {
            SelectSectionForEdit(ActiveSections[idx + 1]);
        }
        SaveData();
    }

    [RelayCommand]
    public void ResetToSingleUnsectionedWaveform()
    {
        if (CurrentSong == null) return;
        double totalSecs = CurrentSong.Duration.TotalSeconds > 5 ? CurrentSong.Duration.TotalSeconds : _mixer.TotalDuration.TotalSeconds;
        if (totalSecs <= 5) totalSecs = 180.0;

        var arrangement = CurrentSong.ActiveArrangement;
        if (arrangement == null)
        {
            arrangement = new Arrangement("Original");
            CurrentSong.Arrangements.Add(arrangement);
            CurrentSong.ActiveArrangementId = arrangement.Id;
        }
        arrangement.Sections.Clear();
        arrangement.Sections.Add(new SectionMarker("SECUENCIA", SectionType.Custom, TimeSpan.Zero, TimeSpan.FromSeconds(totalSecs), "#0284C7"));

        RebuildActiveSectionsWithPeaks(CurrentSong);
        SaveData();
    }

    [RelayCommand]
    public void ExpandSelectedSectionBar() => AdjustSectionBars(SelectedSectionForEdit, 1);

    [RelayCommand]
    public void ShrinkSelectedSectionBar() => AdjustSectionBars(SelectedSectionForEdit, -1);

    public void AdjustSectionBars(SectionWaveformViewModel? sectionVm, int deltaBars)
    {
        if (sectionVm == null || CurrentSong == null) return;

        int idx = ActiveSections.IndexOf(sectionVm);
        if (idx < 0) return;

        double bpm = CurrentSong.PlaybackBpm > 20 ? CurrentSong.PlaybackBpm : 120.0;
        int beatsPerBar = CurrentSong.TimeSignatureNumerator > 0 ? CurrentSong.TimeSignatureNumerator : 4;
        double secPerBar = SectionWaveformViewModel.GetSecondsPerBar(bpm, beatsPerBar);

        double deltaSeconds = deltaBars * secPerBar;
        double newDuration = Math.Max(secPerBar, sectionVm.DurationSeconds + deltaSeconds);

        // Snap to bar grid if enabled
        if (IsSnapToGridEnabled)
        {
            int snappedBars = Math.Max(1, (int)Math.Round(newDuration / secPerBar));
            newDuration = snappedBars * secPerBar;
        }

        sectionVm.Model.EndTime = sectionVm.Model.StartTime + TimeSpan.FromSeconds(newDuration);
        sectionVm.RefreshMetrics(bpm, beatsPerBar);

        // Shift subsequent sections to maintain contiguous timeline
        for (int i = idx + 1; i < ActiveSections.Count; i++)
        {
            var prevEnd = ActiveSections[i - 1].Model.EndTime;
            var dur = ActiveSections[i].DurationSeconds;
            ActiveSections[i].Model.StartTime = prevEnd;
            ActiveSections[i].Model.EndTime = prevEnd + TimeSpan.FromSeconds(dur);
            ActiveSections[i].RefreshMetrics(bpm, beatsPerBar);
        }

        RebuildActiveSectionsWithPeaks(CurrentSong, idx);
        OnPropertyChanged(nameof(SelectedSectionBarsText));
        SaveData();
    }

    public void RenameSection(SectionWaveformViewModel? sectionVm, string newName)
    {
        if (sectionVm == null || string.IsNullOrWhiteSpace(newName) || CurrentSong == null) return;

        sectionVm.Model.Name = newName.Trim().ToUpperInvariant();
        (sectionVm.Model.Type, sectionVm.Model.ColorHex) = InferSectionTypeAndColor(sectionVm.Model.Name);

        int beatsPerBar = CurrentSong.TimeSignatureNumerator > 0 ? CurrentSong.TimeSignatureNumerator : 4;
        sectionVm.RefreshMetrics(CurrentSong.PlaybackBpm, beatsPerBar);
        OnPropertyChanged(nameof(SelectedSectionNameText));
        OnPropertyChanged(nameof(SelectedSectionBarsText));
        SaveData();
    }

    [RelayCommand]
    public void SplitSelectedSection()
    {
        if (CurrentSong == null || ActiveSections.Count == 0) return;
        var target = SelectedSectionForEdit ?? ActiveSections.FirstOrDefault(s => s.IsActive) ?? ActiveSections[0];
        double midTime = target.Model.StartTime.TotalSeconds + (target.DurationSeconds / 2.0);
        SplitSectionAtTime(target, midTime, $"{target.Name} B");
    }

    [RelayCommand]
    public void AddSectionAfterSelected()
    {
        if (CurrentSong == null) return;
        if (ActiveSections.Count == 1)
        {
            SplitSelectedSection();
            return;
        }

        double bpm = CurrentSong.PlaybackBpm > 20 ? CurrentSong.PlaybackBpm : 120.0;
        int beatsPerBar = CurrentSong.TimeSignatureNumerator > 0 ? CurrentSong.TimeSignatureNumerator : 4;
        double fourBarsSec = SectionWaveformViewModel.GetSecondsPerBar(bpm, beatsPerBar) * 4;

        var lastEnd = ActiveSections.Count > 0 ? ActiveSections[^1].Model.EndTime : TimeSpan.Zero;
        var newMarker = new SectionMarker(
            "CORO",
            SectionType.Chorus,
            lastEnd,
            lastEnd + TimeSpan.FromSeconds(fourBarsSec),
            "#F59E0B");

        CurrentSong.ActiveArrangement?.Sections.Add(newMarker);
        RebuildActiveSectionsWithPeaks(CurrentSong, ActiveSections.Count);
        SaveData();
    }

    [RelayCommand]
    public void DeleteSelectedSection()
    {
        if (CurrentSong == null || SelectedSectionForEdit == null || ActiveSections.Count <= 1) return;
        var toRemove = SelectedSectionForEdit;
        int idx = ActiveSections.IndexOf(toRemove);
        if (idx < 0) return;

        var removedEnd = toRemove.Model.EndTime;
        var removedStart = toRemove.Model.StartTime;
        CurrentSong.ActiveArrangement?.Sections.Remove(toRemove.Model);

        // Merge the removed time span into the previous (or next) section so total song duration never shrinks!
        var arrangement = CurrentSong.ActiveArrangement;
        if (arrangement != null && arrangement.Sections.Count > 0)
        {
            if (idx > 0 && idx - 1 < arrangement.Sections.Count)
            {
                arrangement.Sections[idx - 1].EndTime = removedEnd;
            }
            else
            {
                arrangement.Sections[0].StartTime = removedStart;
            }
        }

        RebuildActiveSectionsWithPeaks(CurrentSong, Math.Clamp(idx - 1, 0, (arrangement?.Sections.Count ?? 1) - 1));
        SaveData();
    }

    private void RebuildActiveSectionsWithPeaks(Song song, int selectedIndex = 0)
    {
        var arrangement = song.ActiveArrangement;
        if (arrangement == null || arrangement.Sections.Count == 0) return;

        int timeSigNum = song.TimeSignatureNumerator > 0 ? song.TimeSignatureNumerator : 4;
        double bpm = song.PlaybackBpm > 20 ? song.PlaybackBpm : 120.0;

        var realPeaksPerSection = _importer.ExtractSectionPeaksForSong(song, arrangement.Sections, 120);
        ActiveSections.Clear();

        double baseWidth = Math.Max(400.0, _waveformViewportWidth - 8.0);
        double totalTargetWidth = baseWidth * WaveformZoomFactor;
        double totalDuration = arrangement.Sections.Sum(s => Math.Max(0.5, (s.EndTime - s.StartTime).TotalSeconds));
        if (totalDuration <= 0.1) totalDuration = Math.Max(10.0, song.Duration.TotalSeconds);

        for (int i = 0; i < arrangement.Sections.Count; i++)
        {
            var section = arrangement.Sections[i];
            float[]? peaks = (realPeaksPerSection != null && i < realPeaksPerSection.Count) ? realPeaksPerSection[i] : null;
            double secDur = Math.Max(0.5, (section.EndTime - section.StartTime).TotalSeconds);
            double secWidth = arrangement.Sections.Count == 1
                ? totalTargetWidth
                : Math.Max(76.0, (secDur / totalDuration) * totalTargetWidth);

            ActiveSections.Add(new SectionWaveformViewModel(section, timeSigNum, null, bpm, peaks, secWidth));
        }

        if (ActiveSections.Count > 0)
        {
            SelectSectionForEdit(ActiveSections[Math.Clamp(selectedIndex, 0, ActiveSections.Count - 1)]);
        }
    }

    private static (SectionType Type, string ColorHex) InferSectionTypeAndColor(string name)
    {
        if (name.Contains("INTRO", StringComparison.OrdinalIgnoreCase)) return (SectionType.Intro, "#3B82F6");
        if (name.Contains("VERS", StringComparison.OrdinalIgnoreCase)) return (SectionType.Verse, "#10B981");
        if (name.Contains("CORO", StringComparison.OrdinalIgnoreCase) || name.Contains("CHORUS", StringComparison.OrdinalIgnoreCase)) return (SectionType.Chorus, "#F59E0B");
        if (name.Contains("PUENTE", StringComparison.OrdinalIgnoreCase) || name.Contains("BRIDGE", StringComparison.OrdinalIgnoreCase)) return (SectionType.Bridge, "#8B5CF6");
        if (name.Contains("INST", StringComparison.OrdinalIgnoreCase) || name.Contains("SOLO", StringComparison.OrdinalIgnoreCase)) return (SectionType.Instrumental, "#06B6D4");
        if (name.Contains("OUTRO", StringComparison.OrdinalIgnoreCase) || name.Contains("FINAL", StringComparison.OrdinalIgnoreCase)) return (SectionType.Outro, "#64748B");
        return (SectionType.Custom, "#0284C7");
    }

    [RelayCommand]
    public void SelectSong(SongItemViewModel songVm)
    {
        // Prevent changing songs while EDIT mode is actively engaged
        if (IsEditMode) return;

        if (SelectedSong != null)
        {
            SelectedSong.IsSelected = false;
        }

        SelectedSong = songVm;
        songVm.IsSelected = true;

        Transport.CoverBrush = songVm.CoverBrush;
        LoadSong(songVm.Model);
    }

    public void LoadSong(Song song)
    {
        CurrentSong = song;

        // 1. Ensure native Click & Guide stems and accurate categories exist first
        ClickAndCueService.EnsureClickAndGuideStems(song);

        // 2. Apply default Mackie channel assignments (Click & Guide -> Ch 7, AG -> Ch 9, Bass -> Ch 10, Piano -> Ch 11-12 Stereo)
        // if stems still had unconfigured (0) or legacy (1, 2 for Click/Guide) buses
        foreach (var stem in song.Stems)
        {
            if (stem.Category == StemCategory.Click || stem.Category == StemCategory.Guide)
            {
                if (stem.OutputBusIndex == 0 || stem.OutputBusIndex == 1 || stem.OutputBusIndex == 2)
                {
                    stem.OutputBusIndex = 7;
                }
            }
            else if (stem.OutputBusIndex == 0)
            {
                stem.OutputBusIndex = StemCategoryDetector.GetDefaultMackieOutputBus(stem.Category);
            }
        }

        // 3. Order stems in mixer:
        //    First by Bus Order (0=Click, 1=Guide, 2=Drums&Perc, 3=Guitars, 4=Keys, 5=Everything else)
        //    Then alphabetically by Name!
        var orderedStems = song.Stems
            .OrderBy(s => StemCategoryDetector.GetMixerBusOrder(s.Category))
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        song.Stems.Clear();
        foreach (var s in orderedStems)
        {
            song.Stems.Add(s);
        }

        // 4. Build waveform sections (newly imported songs or legacy auto-generated arrangements start unsectioned)
        var arrangement = song.ActiveArrangement;
        bool wasLegacyAutoGenerated = arrangement != null &&
            (arrangement.Name == "Standard Live" || arrangement.Name == "Default Arrangement");

        if (arrangement == null || arrangement.Sections.Count == 0 || wasLegacyAutoGenerated)
        {
            var singleArr = new Arrangement("Original");
            double totalSecs = song.Duration.TotalSeconds > 10 ? song.Duration.TotalSeconds : 180.0;
            singleArr.Sections.Add(new SectionMarker("SECUENCIA", SectionType.Custom, TimeSpan.Zero, TimeSpan.FromSeconds(totalSecs), "#0284C7"));

            song.Arrangements.Clear();
            song.Arrangements.Add(singleArr);
            song.ActiveArrangementId = singleArr.Id;
        }

        RebuildActiveSectionsWithPeaks(song, 0);

        // 5. Build ordered Channel strips for Mixer UI
        Channels.Clear();
        foreach (var stem in song.Stems)
        {
            var channelVm = new StemChannelViewModel(
                stem,
                onVolumeChanged: (id, vol) => _mixer.SetStemVolume(id, vol),
                onPanChanged: (id, pan) => _mixer.SetStemPan(id, pan),
                onMuteChanged: (id, mute) => _mixer.SetStemMute(id, mute),
                onSoloChanged: (id, solo) => _mixer.SetStemSolo(id, solo),
                onOutputBusChanged: (id, busIdx) =>
                {
                    _mixer.SetStemOutputBus(id, busIdx);
                    SaveData();
                }
            );
            Channels.Add(channelVm);
        }

        RefreshBusCounts();

        // 6. Prepare mixer and transport without auto-playing
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
        foreach (var bus in InstrumentBuses)
        {
            bus.IsMuted = false;
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

    [RelayCommand]
    public void MoveSongUp(SongItemViewModel item)
    {
        int index = SetlistSongs.IndexOf(item);
        if (index > 0)
        {
            SetlistSongs.Move(index, index - 1);
        }
    }

    [RelayCommand]
    public void MoveSongDown(SongItemViewModel item)
    {
        int index = SetlistSongs.IndexOf(item);
        if (index >= 0 && index < SetlistSongs.Count - 1)
        {
            SetlistSongs.Move(index, index + 1);
        }
    }

    [RelayCommand]
    public void RemoveSongFromSetlist(SongItemViewModel item)
    {
        SetlistSongs.Remove(item);
        if (SelectedSong == item && SetlistSongs.Count > 0)
        {
            SelectSong(SetlistSongs[0]);
        }
        SaveData();
    }

    public void RemoveSongFromLibrary(SongItemViewModel item)
    {
        SetlistSongs.Remove(item);
        AllLibrarySongItems.Remove(item);
        FilteredLibrarySongs.Remove(item);
        LocalLibrarySongs.Remove(item.Model);
        if (SelectedSong == item && SetlistSongs.Count > 0)
        {
            SelectSong(SetlistSongs[0]);
        }
        SaveData();
    }

    [RelayCommand]
    public void AddNewSong(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) title = "New Sequence";
        var newSong = new Song(title, 120.0, MusicalKey.C, KeyScale.Major)
        {
            Artist = "Custom Track",
            Duration = TimeSpan.FromMinutes(4.0)
        };
        PopulateStems(newSong, isRock: false);
        PopulateArrangement(newSong);
        var newVm = new SongItemViewModel(newSong, "#3B82F6", "#1D4ED8")
        {
            IsEditMode = this.IsEditMode
        };
        SetlistSongs.Add(newVm);
        AllLibrarySongItems.Add(newVm);
        RefreshFilteredSongs();
        SelectSong(newVm);
    }

    public SongItemViewModel ImportMultitrackFolder(string folderPath)
    {
        var song = _importer.ImportFromDirectory(folderPath);
        LocalLibrarySongs.Add(song);
        var songVm = new SongItemViewModel(song, "#3B82F6", "#1D4ED8")
        {
            IsEditMode = this.IsEditMode
        };
        SetlistSongs.Add(songVm);
        AllLibrarySongItems.Add(songVm);
        RefreshFilteredSongs();
        SelectSong(songVm);
        SaveData();
        return songVm;
    }

    [RelayCommand]
    public void AddDemoSongWithRealAudio()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string demoFolder = Path.Combine(appData, "EasyTracks", "DemoTracks", "WayMakerDemo");
        var realSong = _importer.CreateSyntheticDemoSong(demoFolder);
        var vm = new SongItemViewModel(realSong, "#0EA5E9", "#6366F1")
        {
            IsEditMode = this.IsEditMode
        };
        SetlistSongs.Add(vm);
        AllLibrarySongItems.Add(vm);
        RefreshFilteredSongs();
        SelectSong(vm);
    }

    private void InitializeSetlist()
    {
        string userProfileMusic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music");
        string myMusic = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        string baseMusic = Directory.Exists(Path.Combine(userProfileMusic, "EasyTracks")) ? userProfileMusic : myMusic;

        DedicatedTracksDirectory = Path.Combine(baseMusic, "EasyTracks", "Tracks");
        DedicatedPadsDirectory = Path.Combine(baseMusic, "EasyTracks", "Pads");

        try
        {
            if (!Directory.Exists(DedicatedTracksDirectory))
            {
                Directory.CreateDirectory(DedicatedTracksDirectory);
            }

            if (!Directory.Exists(DedicatedPadsDirectory))
            {
                Directory.CreateDirectory(DedicatedPadsDirectory);
            }

            // Load dedicated pads directory into pad player
            _padPlayer.LoadPadDirectory(DedicatedPadsDirectory);

            // 1. Load previously saved songs from JSON disk storage
            var savedSongs = _storage.LoadLibrarySongs();
            foreach (var song in savedSongs)
            {
                LocalLibrarySongs.Add(song);
            }

            // Auto-detect and import all song subfolders from the dedicated tracks library
            var foundFolders = Directory.GetDirectories(DedicatedTracksDirectory).ToList();

            foreach (var dir in foundFolders)
            {
                try
                {
                    // If not already in library, import it
                    var existing = LocalLibrarySongs.FirstOrDefault(s => string.Equals(s.SourceDirectory, dir, StringComparison.OrdinalIgnoreCase) ||
                                                                        string.Equals(s.Title, Path.GetFileName(dir), StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        var song = _importer.ImportFromDirectory(dir);
                        LocalLibrarySongs.Add(song);
                    }
                }
                catch { }
            }

            // Save refreshed library
            _storage.SaveLibrarySongs(LocalLibrarySongs);

            // Build AllLibrarySongItems & FilteredLibrarySongs
            AllLibrarySongItems.Clear();
            foreach (var s in LocalLibrarySongs)
            {
                AllLibrarySongItems.Add(new SongItemViewModel(s, "#0EA5E9", "#6366F1"));
            }
            RefreshFilteredSongs();

            // 2. Load and restore saved setlists
            var savedSetlists = _storage.LoadSetlists();
            SetlistManager.LoadSavedSetlists(savedSetlists);

            // If setlist has songs, ensure the first is selected
            if (SetlistSongs.Count == 0 && LocalLibrarySongs.Count > 0)
            {
                foreach (var s in LocalLibrarySongs)
                {
                    var vm = new SongItemViewModel(s, "#0EA5E9", "#6366F1");
                    SetlistSongs.Add(vm);
                }
            }

            if (SetlistSongs.Count > 0)
            {
                SelectSong(SetlistSongs[0]);
            }
        }
        catch { }
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
