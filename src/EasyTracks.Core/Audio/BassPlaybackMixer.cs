using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using EasyTracks.Core.Abstractions;
using EasyTracks.Domain.Models;

namespace EasyTracks.Core.Audio;

public class BassPlaybackMixer : IPlaybackMixer
{
    private int _mixerHandle;
    private int _tempoHandle;
    private readonly Dictionary<Guid, int> _stemChannels = new();
    private readonly Dictionary<Guid, StemTrack> _stemModels = new();
    private readonly System.Timers.Timer _positionTimer;
    private PlaybackEngineState _state = PlaybackEngineState.Stopped;
    private bool _disposed;

    public PlaybackEngineState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                StateChanged?.Invoke(this, _state);
            }
        }
    }

    public TimeSpan CurrentPosition
    {
        get
        {
            if (_tempoHandle == 0) return TimeSpan.Zero;
            long posBytes = Bass.ChannelGetPosition(_tempoHandle, PositionFlags.Bytes);
            if (posBytes < 0) return TimeSpan.Zero;
            double seconds = Bass.ChannelBytes2Seconds(_tempoHandle, posBytes);
            return TimeSpan.FromSeconds(Math.Max(0, seconds));
        }
    }

    public TimeSpan TotalDuration
    {
        get
        {
            if (_tempoHandle == 0) return TimeSpan.Zero;
            long lenBytes = Bass.ChannelGetLength(_tempoHandle, PositionFlags.Bytes);
            if (lenBytes < 0) return TimeSpan.Zero;
            double seconds = Bass.ChannelBytes2Seconds(_tempoHandle, lenBytes);
            return TimeSpan.FromSeconds(Math.Max(0, seconds));
        }
    }

    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<PlaybackEngineState>? StateChanged;

    public BassPlaybackMixer()
    {
        _positionTimer = new System.Timers.Timer(50); // 20 Hz UI update
        _positionTimer.Elapsed += (_, _) =>
        {
            if (_state == PlaybackEngineState.Playing)
            {
                PositionChanged?.Invoke(this, CurrentPosition);
            }
        };
    }

    public void LoadSong(Song song)
    {
        Stop();
        FreeHandles();

        try
        {
            // 1. Create 32-bit float mixer stream (decoding)
            _mixerHandle = BassMix.CreateMixerStream(44100, 2, BassFlags.Decode | BassFlags.Float);
            if (_mixerHandle != 0)
            {
                // 2. Wrap mixer in Tempo stream for pitch & tempo stretching
                _tempoHandle = BassFx.TempoCreate(_mixerHandle, BassFlags.Default);
            }

            // 3. Load each stem track
            _stemChannels.Clear();
            _stemModels.Clear();

            foreach (var stem in song.Stems)
            {
                if (!File.Exists(stem.FilePath)) continue;

                // Load stem as decoding channel to feed mixer
                int stemHandle = Bass.CreateStream(stem.FilePath, 0, 0, BassFlags.Decode | BassFlags.Float);
                if (stemHandle != 0 && _mixerHandle != 0)
                {
                    // Add channel to mixer
                    if (BassMix.MixerAddChannel(_mixerHandle, stemHandle, BassFlags.MixerChanDownMix))
                    {
                        _stemChannels[stem.Id] = stemHandle;
                        _stemModels[stem.Id] = stem;
                        ApplyStemMixSettings(stem.Id);
                    }
                }
            }

            // Sync initial tempo and pitch
            SetTempo(song.PlaybackBpm, song.OriginalBpm);
            SetPitchSemitones(song.KeySemitoneOffset);
        }
        catch (DllNotFoundException)
        {
            // Audio driver running without native libraries
        }
    }

    public void Play()
    {
        if (_tempoHandle == 0) return;

        if (Bass.ChannelPlay(_tempoHandle, false))
        {
            State = PlaybackEngineState.Playing;
            _positionTimer.Start();
        }
    }

    public void Pause()
    {
        if (_tempoHandle == 0) return;

        if (Bass.ChannelPause(_tempoHandle))
        {
            State = PlaybackEngineState.Paused;
            _positionTimer.Stop();
        }
    }

    public void Stop()
    {
        if (_tempoHandle == 0) return;

        Bass.ChannelStop(_tempoHandle);
        Bass.ChannelSetPosition(_tempoHandle, 0);
        _positionTimer.Stop();
        State = PlaybackEngineState.Stopped;
        PositionChanged?.Invoke(this, TimeSpan.Zero);
    }

    public void Seek(TimeSpan position)
    {
        if (_tempoHandle == 0) return;

        long bytePos = Bass.ChannelSeconds2Bytes(_tempoHandle, position.TotalSeconds);
        Bass.ChannelSetPosition(_tempoHandle, bytePos);
        PositionChanged?.Invoke(this, position);
    }

    public void SetTempo(double playbackBpm, double originalBpm)
    {
        if (_tempoHandle == 0 || originalBpm <= 0) return;

        // BASS_ATTRIB_TEMPO is percentage: 0% = normal, +10% = 1.1x speed, -10% = 0.9x speed
        float tempoPercent = (float)(((playbackBpm / originalBpm) - 1.0) * 100.0);
        Bass.ChannelSetAttribute(_tempoHandle, ChannelAttribute.Tempo, tempoPercent);
    }

    public void SetPitchSemitones(int semitones)
    {
        if (_tempoHandle == 0) return;

        // BASS_ATTRIB_TEMPO_PITCH is semitones (-60 to +60)
        Bass.ChannelSetAttribute(_tempoHandle, ChannelAttribute.Pitch, semitones);
    }

    public void SetStemVolume(Guid stemId, float volume)
    {
        if (_stemModels.TryGetValue(stemId, out var stem))
        {
            stem.Volume = Math.Clamp(volume, 0f, 2f);
            ApplyStemMixSettings(stemId);
        }
    }

    public void SetStemPan(Guid stemId, float pan)
    {
        if (_stemModels.TryGetValue(stemId, out var stem))
        {
            stem.Pan = Math.Clamp(pan, -1f, 1f);
            ApplyStemMixSettings(stemId);
        }
    }

    public void SetStemMute(Guid stemId, bool isMuted)
    {
        if (_stemModels.TryGetValue(stemId, out var stem))
        {
            stem.IsMuted = isMuted;
            UpdateAllStemMixSettings();
        }
    }

    public void SetStemSolo(Guid stemId, bool isSolo)
    {
        if (_stemModels.TryGetValue(stemId, out var stem))
        {
            stem.IsSolo = isSolo;
            UpdateAllStemMixSettings();
        }
    }

    public void SetMasterVolume(float volume)
    {
        if (_tempoHandle != 0)
        {
            Bass.ChannelSetAttribute(_tempoHandle, ChannelAttribute.Volume, Math.Clamp(volume, 0f, 1f));
        }
    }

    private void ApplyStemMixSettings(Guid stemId)
    {
        if (!_stemChannels.TryGetValue(stemId, out int channel) ||
            !_stemModels.TryGetValue(stemId, out var stem))
        {
            return;
        }

        bool hasAnySolo = _stemModels.Values.Any(s => s.IsSolo);
        bool shouldBeAudible = hasAnySolo ? stem.IsSolo : !stem.IsMuted;

        float effectiveVolume = shouldBeAudible ? stem.Volume : 0.0f;
        Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, effectiveVolume);
        Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, stem.Pan);
    }

    private void UpdateAllStemMixSettings()
    {
        foreach (var stemId in _stemChannels.Keys)
        {
            ApplyStemMixSettings(stemId);
        }
    }

    private void FreeHandles()
    {
        if (_tempoHandle != 0)
        {
            Bass.StreamFree(_tempoHandle);
            _tempoHandle = 0;
        }

        if (_mixerHandle != 0)
        {
            Bass.StreamFree(_mixerHandle);
            _mixerHandle = 0;
        }

        foreach (var channel in _stemChannels.Values)
        {
            Bass.StreamFree(channel);
        }
        _stemChannels.Clear();
        _stemModels.Clear();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _positionTimer.Dispose();
            FreeHandles();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
