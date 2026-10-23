using EasyTracks.Domain.Models;

namespace EasyTracks.Core.Abstractions;

public enum PlaybackEngineState
{
    Stopped,
    Playing,
    Paused
}

public record AudioDeviceInfo(int Id, string Name, bool IsDefault, int MaxChannels, bool IsMackieConsole = false)
{
    public string DisplayName => IsMackieConsole
        ? $"🎛️ {Name} (Consola 16-Ch USB)"
        : $"{Name}{(IsDefault ? " [Predeterminado]" : "")}";
}

public interface IAudioEngine : IDisposable
{
    bool IsInitialized { get; }
    int CurrentDeviceIndex { get; }
    event EventHandler<AudioDeviceInfo>? MackieConsoleConnected;

    void Initialize(int sampleRate = 44100, int deviceIndex = -1);
    IReadOnlyList<AudioDeviceInfo> GetOutputDevices();
    void SetOutputDevice(int deviceIndex);
}

public interface IPlaybackMixer : IDisposable
{
    PlaybackEngineState State { get; }
    TimeSpan CurrentPosition { get; }
    TimeSpan TotalDuration { get; }

    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler<PlaybackEngineState>? StateChanged;
    event EventHandler? TrackEnded;

    void LoadSong(Song song);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);
    
    // Live tempo & pitch shifting (BASS_FX tempo stream)
    void SetTempo(double playbackBpm, double originalBpm);
    void SetPitchSemitones(int semitones);

    // Channel controls
    void SetStemVolume(Guid stemId, float volume);
    void SetStemPan(Guid stemId, float pan);
    void SetStemMute(Guid stemId, bool isMuted);
    void SetStemSolo(Guid stemId, bool isSolo);
    void SetStemOutputBus(Guid stemId, int outputBusIndex);
    
    // Master controls
    void SetMasterVolume(float volume);

    // VU Meters
    float GetStemPeakLevel(Guid stemId);
    (float Left, float Right) GetMasterPeakLevel();
}

public interface IPadPlayer : IDisposable
{
    bool IsRunning { get; }
    MusicalKey CurrentKey { get; }
    float Volume { get; set; }
    
    void LoadPadDirectory(string directoryPath);
    void PlayKey(MusicalKey key, TimeSpan crossfadeDuration);
    void Stop(TimeSpan fadeOutDuration);
}
