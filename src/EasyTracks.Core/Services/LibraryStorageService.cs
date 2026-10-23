using System.Text.Json;
using EasyTracks.Domain.Models;

namespace EasyTracks.Core.Services;

public class LibraryStorageService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string GetStorageDirectory()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EasyTracks");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }

    private static string GetSongsFilePath() => Path.Combine(GetStorageDirectory(), "library_songs.json");
    private static string GetSetlistsFilePath() => Path.Combine(GetStorageDirectory(), "setlists.json");

    public void SaveLibrarySongs(IEnumerable<Song> songs)
    {
        try
        {
            string json = JsonSerializer.Serialize(songs, JsonOpts);
            File.WriteAllText(GetSongsFilePath(), json);
        }
        catch { }
    }

    public List<Song> LoadLibrarySongs()
    {
        try
        {
            string path = GetSongsFilePath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<List<Song>>(json, JsonOpts);
                if (loaded != null) return loaded;
            }
        }
        catch { }
        return new List<Song>();
    }

    public void SaveSetlists(IEnumerable<Setlist> setlists)
    {
        try
        {
            string json = JsonSerializer.Serialize(setlists, JsonOpts);
            File.WriteAllText(GetSetlistsFilePath(), json);
        }
        catch { }
    }

    private static string GetSettingsFilePath() => Path.Combine(GetStorageDirectory(), "user_settings.json");

    private class UserAudioSettings
    {
        public double PadVolume { get; set; } = 0.75;
    }

    public double LoadPadVolume()
    {
        try
        {
            string path = GetSettingsFilePath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<UserAudioSettings>(json, JsonOpts);
                if (settings != null)
                {
                    return Math.Clamp(settings.PadVolume, 0.0, 1.0);
                }
            }
        }
        catch { }
        return 0.75;
    }

    public void SavePadVolume(double padVolume)
    {
        try
        {
            var settings = new UserAudioSettings { PadVolume = Math.Clamp(padVolume, 0.0, 1.0) };
            string json = JsonSerializer.Serialize(settings, JsonOpts);
            File.WriteAllText(GetSettingsFilePath(), json);
        }
        catch { }
    }

    public List<Setlist> LoadSetlists()
    {
        try
        {
            string path = GetSetlistsFilePath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<List<Setlist>>(json, JsonOpts);
                if (loaded != null) return loaded;
            }
        }
        catch { }
        return new List<Setlist>();
    }
}
