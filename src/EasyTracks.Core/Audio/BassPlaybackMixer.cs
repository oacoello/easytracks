using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using EasyTracks.Core.Abstractions;
using EasyTracks.Domain.Models;

namespace EasyTracks.Core.Audio;

public class BassPlaybackMixer : IPlaybackMixer
{
    private int _masterMixerHandle;
    private int _musicMixerHandle;
    private int _musicTempoHandle;
    private int _utilityMixerHandle;
    private int _utilityTempoHandle;
    private int _referenceStemHandle;
    private double _cachedTotalDurationSeconds;
    private double _lastSeekSeconds;

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
            if (_referenceStemHandle == 0 || _masterMixerHandle == 0)
            {
                return TimeSpan.FromSeconds(Math.Max(0, _lastSeekSeconds));
            }

            long posBytes = BassMix.ChannelGetPosition(_referenceStemHandle, PositionFlags.Bytes);
            if (posBytes < 0)
            {
                posBytes = Bass.ChannelGetPosition(_referenceStemHandle, PositionFlags.Bytes);
            }
            if (posBytes < 0) return TimeSpan.FromSeconds(Math.Max(0, _lastSeekSeconds));

            double seconds = Bass.ChannelBytes2Seconds(_referenceStemHandle, posBytes);
            if (seconds < 0) return TimeSpan.FromSeconds(Math.Max(0, _lastSeekSeconds));
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public TimeSpan TotalDuration => TimeSpan.FromSeconds(Math.Max(0, _cachedTotalDurationSeconds));

    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<PlaybackEngineState>? StateChanged;
    public event EventHandler? TrackEnded;

    public BassPlaybackMixer()
    {
        _positionTimer = new System.Timers.Timer(40); // 25 Hz smooth UI & loop check
        _positionTimer.Elapsed += (_, _) =>
        {
            if (_state == PlaybackEngineState.Playing)
            {
                var cur = CurrentPosition;
                var total = TotalDuration;
                PositionChanged?.Invoke(this, cur);

                if (total > TimeSpan.FromSeconds(1) &&
                    (cur >= total - TimeSpan.FromMilliseconds(150) ||
                     (_masterMixerHandle != 0 && Bass.ChannelIsActive(_masterMixerHandle) == PlaybackState.Stopped)))
                {
                    Stop();
                    TrackEnded?.Invoke(this, EventArgs.Empty);
                }
            }
        };
    }

    private static bool IsClickOrGuideStem(StemTrack stem)
    {
        if (stem.Category == StemCategory.Click || stem.Category == StemCategory.Guide)
            return true;

        if (string.IsNullOrWhiteSpace(stem.Name))
            return false;

        string n = stem.Name.Trim();
        return n.Contains("Click", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Guide", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Guía", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Guia", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Metronom", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Metrónom", StringComparison.OrdinalIgnoreCase)
            || n.Equals("Cue", StringComparison.OrdinalIgnoreCase)
            || n.Equals("Cues", StringComparison.OrdinalIgnoreCase);
    }

    public void LoadSong(Song song)
    {
        Stop();
        FreeHandles();

        try
        {
            // 1. Sub-mixer for Musical Stems (receives BOTH Tempo AND Pitch shift)
            _musicMixerHandle = BassMix.CreateMixerStream(44100, 2, BassFlags.Decode | BassFlags.Float | BassFlags.MixerNonStop);
            if (_musicMixerHandle != 0)
            {
                _musicTempoHandle = BassFx.TempoCreate(_musicMixerHandle, BassFlags.Decode | BassFlags.Float);
            }

            // 2. Sub-mixer for Click & Guide Stems (receives Tempo ONLY, Pitch is ALWAYS 0!)
            _utilityMixerHandle = BassMix.CreateMixerStream(44100, 2, BassFlags.Decode | BassFlags.Float | BassFlags.MixerNonStop);
            if (_utilityMixerHandle != 0)
            {
                _utilityTempoHandle = BassFx.TempoCreate(_utilityMixerHandle, BassFlags.Decode | BassFlags.Float);
            }

            // 3. Master output mixer combining both tempo-locked chains in sample sync
            _masterMixerHandle = BassMix.CreateMixerStream(44100, 2, BassFlags.Float | BassFlags.MixerNonStop);
            if (_masterMixerHandle != 0)
            {
                if (_musicTempoHandle != 0)
                {
                    BassMix.MixerAddChannel(_masterMixerHandle, _musicTempoHandle, BassFlags.Default);
                }
                if (_utilityTempoHandle != 0)
                {
                    BassMix.MixerAddChannel(_masterMixerHandle, _utilityTempoHandle, BassFlags.Default);
                }
            }

            // 4. Load each stem track into its respective sub-mixer
            _stemChannels.Clear();
            _stemModels.Clear();
            _referenceStemHandle = 0;
            _cachedTotalDurationSeconds = song.Duration.TotalSeconds;
            _lastSeekSeconds = 0;

            foreach (var stem in song.Stems)
            {
                if (!File.Exists(stem.FilePath)) continue;

                int stemHandle = Bass.CreateStream(stem.FilePath, 0, 0, BassFlags.Decode | BassFlags.Float);
                if (stemHandle == 0) continue;

                int targetSubMixer = IsClickOrGuideStem(stem) ? _utilityMixerHandle : _musicMixerHandle;
                if (targetSubMixer != 0 &&
                    BassMix.MixerAddChannel(targetSubMixer, stemHandle, BassFlags.MixerChanDownMix | BassFlags.MixerChanBuffer))
                {
                    _stemChannels[stem.Id] = stemHandle;
                    _stemModels[stem.Id] = stem;

                    long lenBytes = Bass.ChannelGetLength(stemHandle, PositionFlags.Bytes);
                    if (lenBytes > 0)
                    {
                        double secs = Bass.ChannelBytes2Seconds(stemHandle, lenBytes);
                        if (secs >= _cachedTotalDurationSeconds || _referenceStemHandle == 0)
                        {
                            _cachedTotalDurationSeconds = Math.Max(_cachedTotalDurationSeconds, secs);
                            _referenceStemHandle = stemHandle;
                        }
                    }
                    else if (_referenceStemHandle == 0)
                    {
                        _referenceStemHandle = stemHandle;
                    }

                    ApplyStemMixSettings(stem.Id);
                }
                else
                {
                    Bass.StreamFree(stemHandle);
                }
            }

            // Sync initial tempo and pitch
            if (song.PlaybackBpm <= 20) song.PlaybackBpm = song.OriginalBpm > 20 ? song.OriginalBpm : 120.0;
            if (song.OriginalBpm <= 20) song.OriginalBpm = song.PlaybackBpm;

            SetTempo(song.PlaybackBpm, song.OriginalBpm);
            SetPitchSemitones(song.KeySemitoneOffset);
            SeekInternal(0, notify: false);
        }
        catch (DllNotFoundException)
        {
            // Audio driver running without native libraries
        }
    }

    public void Play()
    {
        if (_masterMixerHandle == 0) return;

        // If stopped at the beginning, ensure buffers and stems are cleanly at _lastSeekSeconds
        if (_state == PlaybackEngineState.Stopped)
        {
            SeekInternal(_lastSeekSeconds, notify: false);
        }

        if (Bass.ChannelPlay(_masterMixerHandle, false))
        {
            State = PlaybackEngineState.Playing;
            _positionTimer.Start();
        }
    }

    public void Pause()
    {
        if (_masterMixerHandle == 0) return;

        if (Bass.ChannelPause(_masterMixerHandle))
        {
            _lastSeekSeconds = CurrentPosition.TotalSeconds;
            State = PlaybackEngineState.Paused;
            _positionTimer.Stop();
        }
    }

    public void Stop()
    {
        _positionTimer.Stop();

        if (_masterMixerHandle != 0)
        {
            Bass.ChannelStop(_masterMixerHandle);
        }

        // Always rewind all stems to 00:00 when Stop is pressed
        SeekInternal(0.0, notify: false);
        State = PlaybackEngineState.Stopped;
        PositionChanged?.Invoke(this, TimeSpan.Zero);
    }

    public void Seek(TimeSpan position)
    {
        SeekInternal(position.TotalSeconds, notify: true);
    }

    private void SeekInternal(double targetSeconds, bool notify)
    {
        double clampedSeconds = Math.Max(0.0, targetSeconds);
        if (_cachedTotalDurationSeconds > 0)
        {
            clampedSeconds = Math.Min(clampedSeconds, Math.Max(0.0, _cachedTotalDurationSeconds - 0.05));
        }

        _lastSeekSeconds = clampedSeconds;

        foreach (var channel in _stemChannels.Values)
        {
            if (channel == 0) continue;
            long maxBytes = Bass.ChannelGetLength(channel, PositionFlags.Bytes);
            long bytePos = Bass.ChannelSeconds2Bytes(channel, clampedSeconds);
            if (maxBytes > 0 && bytePos >= maxBytes)
            {
                bytePos = Math.Max(0, maxBytes - 4);
            }
            if (bytePos >= 0)
            {
                BassMix.ChannelSetPosition(channel, bytePos, PositionFlags.Bytes);
            }
        }

        // Flush mixer & tempo stream buffers so seek takes effect immediately without audio tail or stutter
        if (_musicMixerHandle != 0) Bass.ChannelSetPosition(_musicMixerHandle, 0, PositionFlags.Bytes);
        if (_utilityMixerHandle != 0) Bass.ChannelSetPosition(_utilityMixerHandle, 0, PositionFlags.Bytes);
        if (_musicTempoHandle != 0) Bass.ChannelSetPosition(_musicTempoHandle, 0, PositionFlags.Bytes);
        if (_utilityTempoHandle != 0) Bass.ChannelSetPosition(_utilityTempoHandle, 0, PositionFlags.Bytes);
        if (_masterMixerHandle != 0) Bass.ChannelSetPosition(_masterMixerHandle, 0, PositionFlags.Bytes);

        if (notify)
        {
            PositionChanged?.Invoke(this, TimeSpan.FromSeconds(clampedSeconds));
        }
    }

    public void SetTempo(double playbackBpm, double originalBpm)
    {
        if (originalBpm <= 0 || playbackBpm <= 0) return;

        // BASS_ATTRIB_TEMPO is percentage: 0% = normal, +10% = 1.1x speed, -10% = 0.9x speed
        float tempoPercent = (float)Math.Clamp(((playbackBpm / originalBpm) - 1.0) * 100.0, -50.0, 100.0);

        if (_musicTempoHandle != 0)
        {
            Bass.ChannelSetAttribute(_musicTempoHandle, ChannelAttribute.Tempo, tempoPercent);
        }
        if (_utilityTempoHandle != 0)
        {
            Bass.ChannelSetAttribute(_utilityTempoHandle, ChannelAttribute.Tempo, tempoPercent);
        }
    }

    public void SetPitchSemitones(int semitones)
    {
        // Apply pitch ONLY to musical stems; Click & Guide (_utilityTempoHandle) remain at 0 semitones!
        if (_musicTempoHandle != 0)
        {
            Bass.ChannelSetAttribute(_musicTempoHandle, ChannelAttribute.Pitch, semitones);
        }
        if (_utilityTempoHandle != 0)
        {
            Bass.ChannelSetAttribute(_utilityTempoHandle, ChannelAttribute.Pitch, 0f);
        }
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

    public void SetStemOutputBus(Guid stemId, int outputBusIndex)
    {
        if (_stemModels.TryGetValue(stemId, out var stem))
        {
            stem.OutputBusIndex = outputBusIndex;
            ApplyStemMixSettings(stemId);
        }
    }

    public void SetMasterVolume(float volume)
    {
        if (_masterMixerHandle != 0)
        {
            Bass.ChannelSetAttribute(_masterMixerHandle, ChannelAttribute.Volume, Math.Clamp(volume, 0f, 1f));
        }
    }

    public float GetStemPeakLevel(Guid stemId)
    {
        if (_state != PlaybackEngineState.Playing) return 0f;
        if (_stemChannels.TryGetValue(stemId, out int channel) && channel != 0)
        {
            int level = BassMix.ChannelGetLevel(channel);
            if (level < 0) return 0f;
            int left = level & 0xFFFF;
            int right = (level >> 16) & 0xFFFF;
            return Math.Clamp(Math.Max(left, right) / 32768f, 0f, 1f);
        }
        return 0f;
    }

    public (float Left, float Right) GetMasterPeakLevel()
    {
        if (_state != PlaybackEngineState.Playing || _masterMixerHandle == 0) return (0f, 0f);
        int level = Bass.ChannelGetLevel(_masterMixerHandle);
        if (level < 0) return (0f, 0f);
        float left = Math.Clamp((level & 0xFFFF) / 32768f, 0f, 1f);
        float right = Math.Clamp(((level >> 16) & 0xFFFF) / 32768f, 0f, 1f);
        return (left, right);
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

        // Routing logic:
        //  0 = Stereo Out 1-2 (uses stem.Pan)
        // -1 = 2-Ch Split Left (In-Ears: Click + Guide)
        // -2 = 2-Ch Split Right (FOH Mono Stems)
        //  1..16 = Mackie DL16S USB Direct Channels (odd -> L bus pair, even -> R bus pair on stereo fallback)
        //  101..115 = Mackie DL16S Stereo Pairs (1-2, 3-4, ..., 11-12, ..., 15-16)
        float effectivePan = stem.OutputBusIndex switch
        {
            -1 => -1.0f,
            -2 => 1.0f,
            > 0 and <= 16 => (stem.OutputBusIndex % 2 == 1) ? -0.75f : 0.75f,
            > 100 and <= 116 => 0.0f,
            _ => stem.Pan
        };

        Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, effectivePan);
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
        if (_masterMixerHandle != 0)
        {
            Bass.StreamFree(_masterMixerHandle);
            _masterMixerHandle = 0;
        }

        if (_musicTempoHandle != 0)
        {
            Bass.StreamFree(_musicTempoHandle);
            _musicTempoHandle = 0;
        }

        if (_utilityTempoHandle != 0)
        {
            Bass.StreamFree(_utilityTempoHandle);
            _utilityTempoHandle = 0;
        }

        if (_musicMixerHandle != 0)
        {
            Bass.StreamFree(_musicMixerHandle);
            _musicMixerHandle = 0;
        }

        if (_utilityMixerHandle != 0)
        {
            Bass.StreamFree(_utilityMixerHandle);
            _utilityMixerHandle = 0;
        }

        foreach (var channel in _stemChannels.Values)
        {
            Bass.StreamFree(channel);
        }
        _stemChannels.Clear();
        _stemModels.Clear();
        _referenceStemHandle = 0;
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
