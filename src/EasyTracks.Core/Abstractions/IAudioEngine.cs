using EasyTracks.Domain.Models;

namespace EasyTracks.Core.Abstractions;

public enum PlaybackEngineState
{
    Stopped,
    Playing,
    Paused
}

public record AudioDeviceInfo(int Id, string Name, bool IsDefault, int MaxChannels);

public interface IAudioEngine : IDisposable
{
    bool IsInitialized { get; }
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
    
    // Master controls
    void SetMasterVolume(float volume);
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
