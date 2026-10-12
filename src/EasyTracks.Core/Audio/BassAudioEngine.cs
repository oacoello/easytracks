using ManagedBass;
using EasyTracks.Core.Abstractions;

namespace EasyTracks.Core.Audio;

public class BassAudioEngine : IAudioEngine
{
    private bool _isInitialized;
    private bool _disposed;

    public bool IsInitialized => _isInitialized;

    public void Initialize(int sampleRate = 44100, int deviceIndex = -1)
    {
        try
        {
            if (_isInitialized)
            {
                Bass.Free();
                _isInitialized = false;
            }

            // Initialize default device or specified device
            // BassFlags.Default initializes standard stereo 16-bit float mix
            if (!Bass.Init(deviceIndex, sampleRate, DeviceInitFlags.Latency))
            {
                var error = Bass.LastError;
                // If already initialized, ignore error
                if (error != Errors.Already)
                {
                    // Non-fatal device init warning
                }
            }

            // Configure optimal buffer sizes for live performance (e.g. 100ms playback buffer)
            Bass.Configure(Configuration.PlaybackBufferLength, 100);
            Bass.Configure(Configuration.UpdatePeriod, 10);

            _isInitialized = true;
        }
        catch (DllNotFoundException)
        {
            _isInitialized = false;
        }
    }

    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        int count = Bass.DeviceCount;

        for (int i = 0; i < count; i++)
        {
            if (Bass.GetDeviceInfo(i, out var info))
            {
                devices.Add(new AudioDeviceInfo(
                    i,
                    info.Name,
                    info.IsDefault,
                    info.Driver == null ? 2 : 8
                ));
            }
        }

        return devices;
    }

    public void SetOutputDevice(int deviceIndex)
    {
        if (!_isInitialized)
        {
            Initialize(44100, deviceIndex);
            return;
        }

        if (Bass.CurrentDevice != deviceIndex)
        {
            Bass.CurrentDevice = deviceIndex;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_isInitialized)
            {
                Bass.Free();
                _isInitialized = false;
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
