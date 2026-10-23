using ManagedBass;
using EasyTracks.Core.Abstractions;

namespace EasyTracks.Core.Audio;

public class BassAudioEngine : IAudioEngine
{
    private bool _isInitialized;
    private bool _disposed;
    private int _currentDeviceIndex = -1;
    private bool _wasMackieConnected;
    private readonly System.Timers.Timer _pnpWatcherTimer;

    public bool IsInitialized => _isInitialized;
    public int CurrentDeviceIndex => _currentDeviceIndex;
    public event EventHandler<AudioDeviceInfo>? MackieConsoleConnected;

    public BassAudioEngine()
    {
        _pnpWatcherTimer = new System.Timers.Timer(3000);
        _pnpWatcherTimer.Elapsed += (_, _) => CheckForMackiePnP();
        _pnpWatcherTimer.Start();
    }

    private void CheckForMackiePnP()
    {
        try
        {
            var devices = GetOutputDevices();
            var mackie = devices.FirstOrDefault(d => d.IsMackieConsole && d.Id >= 0);
            if (mackie != null && !_wasMackieConnected)
            {
                _wasMackieConnected = true;
                MackieConsoleConnected?.Invoke(this, mackie);
            }
            else if (mackie == null)
            {
                _wasMackieConnected = false;
            }
        }
        catch
        {
            // Ignore PnP polling errors
        }
    }

    public void Initialize(int sampleRate = 44100, int deviceIndex = -1)
    {
        try
        {
            if (_isInitialized)
            {
                Bass.Free();
                _isInitialized = false;
            }

            // Virtual offline profile (-99) falls back to default Windows output (-1)
            int actualDevice = deviceIndex < -1 ? -1 : deviceIndex;

            if (!Bass.Init(actualDevice, sampleRate, DeviceInitFlags.Latency))
            {
                var error = Bass.LastError;
                if (error != Errors.Already)
                {
                    // Fallback to default device if specific device failed
                    Bass.Init(-1, sampleRate, DeviceInitFlags.Latency);
                }
            }

            _currentDeviceIndex = deviceIndex;

            // Configure optimal buffer sizes for live performance (100ms playback buffer)
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
        bool foundHardwareMackie = false;

        try
        {
            int count = Bass.DeviceCount;
            for (int i = 1; i < count; i++) // Skip 0 ("No sound")
            {
                if (Bass.GetDeviceInfo(i, out var info) && info.IsEnabled)
                {
                    string name = info.Name ?? $"Output Device {i}";
                    bool isMackie = name.Contains("Mackie", StringComparison.OrdinalIgnoreCase) ||
                                    name.Contains("DL16S", StringComparison.OrdinalIgnoreCase) ||
                                    name.Contains("DL32S", StringComparison.OrdinalIgnoreCase);

                    if (isMackie) foundHardwareMackie = true;

                    devices.Add(new AudioDeviceInfo(
                        i,
                        name,
                        info.IsDefault,
                        isMackie ? 16 : 2,
                        isMackie
                    ));
                }
            }
        }
        catch (DllNotFoundException)
        {
        }

        if (devices.Count == 0)
        {
            devices.Add(new AudioDeviceInfo(-1, "Windows Default Audio (WASAPI)", true, 2, false));
        }

        // Always provide Mackie DL16S pre-configuration profile when unplugged so the engineer can route channels offline
        if (!foundHardwareMackie)
        {
            devices.Add(new AudioDeviceInfo(
                -99,
                "Mackie DL16S USB (Modo Pre-Configuración / Desconectada)",
                false,
                16,
                true
            ));
        }

        return devices;
    }

    public void SetOutputDevice(int deviceIndex)
    {
        _currentDeviceIndex = deviceIndex;
        int actualDevice = deviceIndex < -1 ? -1 : deviceIndex;

        if (!_isInitialized)
        {
            Initialize(44100, actualDevice);
            return;
        }

        try
        {
            if (!Bass.Init(actualDevice, 44100, DeviceInitFlags.Latency))
            {
                if (Bass.LastError == Errors.Already)
                {
                    Bass.CurrentDevice = actualDevice;
                }
            }
            else
            {
                Bass.CurrentDevice = actualDevice;
            }
        }
        catch
        {
            // Ignore if switching to same or fallback device
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _pnpWatcherTimer.Stop();
            _pnpWatcherTimer.Dispose();
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
