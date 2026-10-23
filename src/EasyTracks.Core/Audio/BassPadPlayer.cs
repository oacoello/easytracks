using System.Text.RegularExpressions;
using ManagedBass;
using EasyTracks.Core.Abstractions;
using EasyTracks.Domain.Models;

namespace EasyTracks.Core.Audio;

public class BassPadPlayer : IPadPlayer
{
    private readonly Dictionary<MusicalKey, string> _padFileMap = new();
    private string? _lastDirectory;
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
        _lastDirectory = directoryPath;
        _padFileMap.Clear();

        var candidateDirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(directoryPath))
            candidateDirs.Add(directoryPath);

        string userProfilePads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music", "EasyTracks", "Pads");
        if (!candidateDirs.Contains(userProfilePads, StringComparer.OrdinalIgnoreCase))
            candidateDirs.Add(userProfilePads);

        string myMusicPads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "EasyTracks", "Pads");
        if (!candidateDirs.Contains(myMusicPads, StringComparer.OrdinalIgnoreCase))
            candidateDirs.Add(myMusicPads);

        foreach (var dir in candidateDirs)
        {
            if (!Directory.Exists(dir)) continue;

            var files = Directory.GetFiles(dir, "*.*")
                .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".flac", StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file).Trim();

                if (TryDetectPadKey(fileName, out var detectedKey) && !_padFileMap.ContainsKey(detectedKey))
                {
                    _padFileMap[detectedKey] = file;
                }
            }
        }
    }

    private static bool TryDetectPadKey(string fileName, out MusicalKey detectedKey)
    {
        // 1. Check if filename starts with note name (e.g. "Ab FULL MIX", "C FULL MIX", "Bb_ambient", "F#")
        var prefixMatch = Regex.Match(fileName, @"^([A-Ga-g][b#♯♭]?)(?:[\s_\-\.]|$)", RegexOptions.IgnoreCase);
        if (prefixMatch.Success)
        {
            string token = prefixMatch.Groups[1].Value;
            if (MusicalKeyExtensions.TryParseMusicalKey(token, out detectedKey))
            {
                return true;
            }
        }

        // 2. Check for note name after common prefixes (e.g. "Pad_Ab", "Key C#", "Tono D")
        var tokenMatch = Regex.Match(fileName, @"\b([A-Ga-g][b#♯♭]?)\b", RegexOptions.IgnoreCase);
        if (tokenMatch.Success)
        {
            string token = tokenMatch.Groups[1].Value;
            if (MusicalKeyExtensions.TryParseMusicalKey(token, out detectedKey))
            {
                return true;
            }
        }

        detectedKey = MusicalKey.C;
        return false;
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
            LoadPadDirectory(_lastDirectory ?? string.Empty);
            _padFileMap.TryGetValue(key, out filePath);
        }

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
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

        bool wasRunning = _currentChannel != 0 && Bass.ChannelIsActive(_currentChannel) == PlaybackState.Playing;

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

        // 2. Load and start the new pad channel
        int newChannel = Bass.CreateStream(filePath, 0, 0, BassFlags.Loop | BassFlags.Float);
        if (newChannel == 0)
        {
            Bass.Init(-1, 44100, DeviceInitFlags.Default);
            newChannel = Bass.CreateStream(filePath, 0, 0, BassFlags.Loop | BassFlags.Float);
        }
        if (newChannel == 0) return;

        int attackMs = wasRunning ? fadeMs : Math.Min(fadeMs, 400);
        Bass.ChannelSetAttribute(newChannel, ChannelAttribute.Volume, wasRunning ? 0.0f : _volume * 0.35f);
        Bass.ChannelPlay(newChannel, false);
        Bass.ChannelSlideAttribute(newChannel, ChannelAttribute.Volume, _volume, attackMs);

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
