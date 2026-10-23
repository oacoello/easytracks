using EasyTracks.Domain.Models;

namespace EasyTracks.Core.Audio;

/// <summary>
/// Generates native synthetic Metronome Click (with downbeat accent) and Section Cue Guide WAV stems
/// so every song in EasyTracks has dedicated Click and Guide faders, VU meters, and USB routing (Ch 1 & Ch 2).
/// </summary>
public static class ClickAndCueService
{
    private const int SampleRate = 44100;

    private static string GetCacheDirectory()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EasyTracks",
            "SynthCache");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void EnsureClickAndGuideStems(Song song, bool forceRegenerateGuide = false)
    {
        var detector = new EasyTracks.Domain.Services.StemCategoryDetector();

        // Re-verify stem categories for previously saved songs (e.g. "Guía", "Guía (Spanish)", "Click Track")
        foreach (var stem in song.Stems)
        {
            var (detected, confidence) = detector.DetectCategory(stem.Name);
            if (detected != StemCategory.Other && (stem.Category == StemCategory.Other || confidence >= 0.9))
            {
                stem.Category = detected;
            }
        }

        // Remove duplicate synthetic Click/Guide stems if the song already has real Click/Guide files from its folder
        bool hasRealClick = song.Stems.Any(s => s.Category == StemCategory.Click && !string.IsNullOrEmpty(s.FilePath) && !s.FilePath.Contains("SynthCache", StringComparison.OrdinalIgnoreCase));
        if (hasRealClick)
        {
            song.Stems.RemoveAll(s => s.Category == StemCategory.Click && (string.IsNullOrEmpty(s.FilePath) || s.FilePath.Contains("SynthCache", StringComparison.OrdinalIgnoreCase)));
        }

        bool hasRealGuide = song.Stems.Any(s => s.Category == StemCategory.Guide && !string.IsNullOrEmpty(s.FilePath) && !s.FilePath.Contains("SynthCache", StringComparison.OrdinalIgnoreCase));
        if (hasRealGuide)
        {
            song.Stems.RemoveAll(s => s.Category == StemCategory.Guide && (string.IsNullOrEmpty(s.FilePath) || s.FilePath.Contains("SynthCache", StringComparison.OrdinalIgnoreCase)));
        }

        double bpm = song.OriginalBpm > 20 ? song.OriginalBpm : 120.0;
        int beatsPerBar = song.TimeSignatureNumerator > 0 ? song.TimeSignatureNumerator : 4;
        double durationSec = Math.Max(30.0, song.Duration.TotalSeconds > 5 ? song.Duration.TotalSeconds : 210.0);

        // 1. Ensure Click Stem
        var clickStem = song.Stems.FirstOrDefault(s => s.Category == StemCategory.Click);
        if (clickStem == null)
        {
            string clickPath = GenerateClickWav(song.Id, bpm, beatsPerBar, durationSec);
            clickStem = new StemTrack
            {
                Name = "Click",
                Category = StemCategory.Click,
                FilePath = clickPath,
                Volume = 0.85f,
                Pan = 0.0f,
                OutputBusIndex = 7 // Default Mackie USB Ch 7
            };
            song.Stems.Insert(0, clickStem);
        }
        else if (string.IsNullOrWhiteSpace(clickStem.FilePath) || !File.Exists(clickStem.FilePath))
        {
            clickStem.FilePath = GenerateClickWav(song.Id, bpm, beatsPerBar, durationSec);
        }

        // 2. Ensure Guide / Cues Stem
        var guideStem = song.Stems.FirstOrDefault(s => s.Category == StemCategory.Guide);
        if (guideStem == null)
        {
            string guidePath = GenerateGuideCueWav(song, bpm, beatsPerBar, durationSec, true);
            guideStem = new StemTrack
            {
                Name = "Guide",
                Category = StemCategory.Guide,
                FilePath = guidePath,
                Volume = 0.90f,
                Pan = 0.0f,
                OutputBusIndex = 7 // Default Mackie USB Ch 7
            };
            int insertIdx = song.Stems.Count > 0 && song.Stems[0].Category == StemCategory.Click ? 1 : 0;
            song.Stems.Insert(insertIdx, guideStem);
        }
        else if (forceRegenerateGuide || string.IsNullOrWhiteSpace(guideStem.FilePath) || !File.Exists(guideStem.FilePath))
        {
            // Only overwrite if it's a synthetic cached guide or missing file
            if (string.IsNullOrWhiteSpace(guideStem.FilePath) ||
                !File.Exists(guideStem.FilePath) ||
                guideStem.FilePath.Contains("SynthCache", StringComparison.OrdinalIgnoreCase))
            {
                guideStem.FilePath = GenerateGuideCueWav(song, bpm, beatsPerBar, durationSec, true);
            }
        }
    }

    public static string GenerateClickWav(Guid songId, double bpm, int beatsPerBar, double durationSeconds)
    {
        string path = Path.Combine(GetCacheDirectory(), $"Click_{songId:N}_{(int)bpm}_{beatsPerBar}.wav");
        if (File.Exists(path)) return path;

        int totalSamples = (int)(durationSeconds * SampleRate);
        short[] samples = new short[totalSamples];

        double secondsPerBeat = 60.0 / Math.Max(30.0, bpm);
        if (beatsPerBar == 6)
        {
            // 6/8 feel: dotted quarter or eighth subdivision
            secondsPerBeat = (60.0 / Math.Max(30.0, bpm)) / 2.0;
        }

        int clickDurationSamples = (int)(0.038 * SampleRate); // 38ms crisp transient

        int beatIndex = 0;
        for (double t = 0; t < durationSeconds; t += secondsPerBeat)
        {
            int startSample = (int)(t * SampleRate);
            bool isDownbeat = (beatIndex % beatsPerBar) == 0;
            bool isSecondaryAccent = beatsPerBar == 6 && (beatIndex % beatsPerBar) == 3;

            double baseFreq = isDownbeat ? 1680.0 : (isSecondaryAccent ? 1320.0 : 1050.0);
            double amplitude = isDownbeat ? 0.85 : (isSecondaryAccent ? 0.65 : 0.52);

            for (int i = 0; i < clickDurationSamples && (startSample + i) < totalSamples; i++)
            {
                double localTime = (double)i / SampleRate;
                // Exponential decay + short woodblock pitch drop transient
                double env = Math.Exp(-localTime * 95.0);
                double pitchEnv = 1.0 + 0.18 * Math.Exp(-localTime * 350.0);
                double signal = Math.Sin(2.0 * Math.PI * baseFreq * pitchEnv * localTime)
                              + 0.35 * Math.Sin(2.0 * Math.PI * (baseFreq * 2.1) * localTime);

                double val = signal * env * amplitude;
                int idx = startSample + i;
                int mixed = samples[idx] + (int)(val * 24000);
                samples[idx] = (short)Math.Clamp(mixed, short.MinValue, short.MaxValue);
            }

            beatIndex++;
        }

        WriteMonoWavFile(path, samples);
        return path;
    }

    public static string GenerateGuideCueWav(Song song, double bpm, int beatsPerBar, double durationSeconds, bool overwrite)
    {
        string path = Path.Combine(GetCacheDirectory(), $"Guide_{song.Id:N}.wav");
        if (!overwrite && File.Exists(path)) return path;

        int totalSamples = (int)(durationSeconds * SampleRate);
        short[] samples = new short[totalSamples];

        WriteMonoWavFile(path, samples);
        return path;
    }

    private static void AddCueChime(short[] buffer, double startTimeSeconds, string sectionName)
    {
        int startSample = (int)(startTimeSeconds * SampleRate);
        int durationSamples = (int)(0.22 * SampleRate);

        // Pick distinctive chord/formant interval based on section type
        double f1 = 587.33; // D5
        double f2 = 880.00; // A5
        if (sectionName.StartsWith("Coro", StringComparison.OrdinalIgnoreCase) ||
            sectionName.StartsWith("Chorus", StringComparison.OrdinalIgnoreCase))
        {
            f1 = 659.25; // E5
            f2 = 987.77; // B5
        }
        else if (sectionName.StartsWith("Puente", StringComparison.OrdinalIgnoreCase) ||
                 sectionName.StartsWith("Bridge", StringComparison.OrdinalIgnoreCase))
        {
            f1 = 523.25; // C5
            f2 = 783.99; // G5
        }

        for (int i = 0; i < durationSamples && (startSample + i) < buffer.Length; i++)
        {
            double t = (double)i / SampleRate;
            double env = Math.Sin(Math.PI * (t / 0.22)) * Math.Exp(-t * 10.0);
            double freq = t < 0.09 ? f1 : f2;
            double sig = (Math.Sin(2.0 * Math.PI * freq * t) + 0.4 * Math.Sin(2.0 * Math.PI * freq * 1.5 * t)) * env * 0.55;

            int idx = startSample + i;
            int mixed = buffer[idx] + (int)(sig * 22000);
            buffer[idx] = (short)Math.Clamp(mixed, short.MinValue, short.MaxValue);
        }
    }

    private static void AddCountInVoicePulse(short[] buffer, double beatTimeSeconds, int beatNumber, int beatsPerBar)
    {
        int startSample = (int)(beatTimeSeconds * SampleRate);
        int durationSamples = (int)(0.095 * SampleRate);

        // Rising cadence on final beat before section entry
        double freq = beatNumber == beatsPerBar ? 784.0 : 523.25;

        for (int i = 0; i < durationSamples && (startSample + i) < buffer.Length; i++)
        {
            double t = (double)i / SampleRate;
            double env = Math.Exp(-t * 32.0);
            double sig = (Math.Sin(2.0 * Math.PI * freq * t) + 0.5 * Math.Sin(2.0 * Math.PI * (freq * 2.0) * t)) * env * 0.48;

            int idx = startSample + i;
            int mixed = buffer[idx] + (int)(sig * 20000);
            buffer[idx] = (short)Math.Clamp(mixed, short.MinValue, short.MaxValue);
        }
    }

    private static void WriteMonoWavFile(string filePath, short[] samples)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var bw = new BinaryWriter(fs);

            int byteRate = SampleRate * 2; // 16-bit mono
            int dataChunkSize = samples.Length * 2;

            // RIFF header
            bw.Write("RIFF"u8.ToArray());
            bw.Write(36 + dataChunkSize);
            bw.Write("WAVE"u8.ToArray());

            // fmt subchunk
            bw.Write("fmt "u8.ToArray());
            bw.Write(16); // PCM chunk size
            bw.Write((short)1); // AudioFormat = 1 (PCM)
            bw.Write((short)1); // NumChannels = 1 (Mono)
            bw.Write(SampleRate);
            bw.Write(byteRate);
            bw.Write((short)2); // BlockAlign
            bw.Write((short)16); // BitsPerSample

            // data subchunk
            bw.Write("data"u8.ToArray());
            bw.Write(dataChunkSize);

            byte[] rawBytes = new byte[dataChunkSize];
            Buffer.BlockCopy(samples, 0, rawBytes, 0, dataChunkSize);
            bw.Write(rawBytes);
        }
        catch
        {
            // Ignore file lock if already playing
        }
    }
}
