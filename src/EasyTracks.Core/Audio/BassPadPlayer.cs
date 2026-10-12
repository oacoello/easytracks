using ManagedBass;
using EasyTracks.Core.Abstractions;
using EasyTracks.Domain.Models;

namespace EasyTracks.Core.Audio;

public class BassPadPlayer : IPadPlayer
{
    private readonly Dictionary<MusicalKey, string> _padFileMap = new();
    private int _currentChannel;
    private int _fadingChannel;
    private float _volume = 0.7f;
    private MusicalKey _currentKey;
    private bool _disposed;

    public bool IsRunning => _currentChannel != 0 && Bass.ChannelIsActive(_currentChannel) == PlaybackState.Playing;
    public MusicalKey CurrentKey => _currentKey;

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (_currentChannel != 0)
            {
                Bass.ChannelSetAttribute(_currentChannel, ChannelAttribute.Volume, _volume);
            }
        }
    }

    public void LoadPadDirectory(string directoryPath)
    {
        _padFileMap.Clear();
        if (!Directory.Exists(directoryPath)) return;

        var files = Directory.GetFiles(directoryPath, "*.*")
            .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file).ToUpperInvariant();
            
            // Map keys
            foreach (MusicalKey key in Enum.GetValues<MusicalKey>())
            {
                string keyStr = key.ToString().ToUpperInvariant();
                if (fileName.Contains(keyStr))
                {
                    _padFileMap[key] = file;
                    break;
                }
            }
        }
    }

    public void RegisterPadFile(MusicalKey key, string filePath)
    {
        if (File.Exists(filePath))
        {
            _padFileMap[key] = filePath;
        }
    }

    public void PlayKey(MusicalKey key, TimeSpan crossfadeDuration)
    {
        if (!_padFileMap.TryGetValue(key, out var filePath) || !File.Exists(filePath))
        {
            return;
        }

        // If the same key is already playing, do nothing
        if (_currentKey == key && IsRunning)
        {
            return;
        }

        int fadeMs = (int)crossfadeDuration.TotalMilliseconds;
        if (fadeMs < 100) fadeMs = 100;

        // 1. Move active channel to fading channel and slide volume down to 0
        if (_currentChannel != 0)
        {
            if (_fadingChannel != 0)
            {
                Bass.StreamFree(_fadingChannel);
            }
            _fadingChannel = _currentChannel;
            Bass.ChannelSlideAttribute(_fadingChannel, ChannelAttribute.Volume, 0.0f, fadeMs);
            
            // Schedule freeing the old channel after fade
            int channelToFree = _fadingChannel;
            Task.Delay(fadeMs + 100).ContinueWith(_ =>
            {
                Bass.ChannelStop(channelToFree);
                Bass.StreamFree(channelToFree);
            });
        }

        // 2. Load and start the new pad channel with volume starting at 0 and sliding to target volume
        int newChannel = Bass.CreateStream(filePath, 0, 0, BassFlags.Loop | BassFlags.Float);
        if (newChannel == 0) return;

        Bass.ChannelSetAttribute(newChannel, ChannelAttribute.Volume, 0.0f);
        Bass.ChannelPlay(newChannel, false);
        Bass.ChannelSlideAttribute(newChannel, ChannelAttribute.Volume, _volume, fadeMs);

        _currentChannel = newChannel;
        _currentKey = key;
    }

    public void Stop(TimeSpan fadeOutDuration)
    {
        int fadeMs = (int)fadeOutDuration.TotalMilliseconds;
        if (_currentChannel != 0)
        {
            int channelToStop = _currentChannel;
            Bass.ChannelSlideAttribute(channelToStop, ChannelAttribute.Volume, 0.0f, fadeMs);
            Task.Delay(fadeMs + 100).ContinueWith(_ =>
            {
                Bass.ChannelStop(channelToStop);
                Bass.StreamFree(channelToStop);
            });
            _currentChannel = 0;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_currentChannel != 0)
            {
                Bass.StreamFree(_currentChannel);
                _currentChannel = 0;
            }
            if (_fadingChannel != 0)
            {
                Bass.StreamFree(_fadingChannel);
                _fadingChannel = 0;
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
