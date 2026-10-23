using System.Text.RegularExpressions;
using ManagedBass;
using EasyTracks.Domain.Models;
using EasyTracks.Domain.Services;

namespace EasyTracks.Core.Audio;

public class MultitrackAudioService
{
    private static readonly string[] SupportedExtensions = { ".wav", ".mp3", ".m4a", ".aac", ".flac", ".ogg", ".aiff", ".wma" };
    private readonly StemCategoryDetector _categoryDetector = new();

    /// <summary>
    /// Scans a directory and creates a Song with real audio stem references and auto-detected categories.
    /// </summary>
    public Song ImportFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory '{directoryPath}' does not exist.");
        }

        var dirInfo = new DirectoryInfo(directoryPath);
        string folderName = dirInfo.Name;

        // Auto-detect BPM from folder name, e.g. "Song Name - 72 BPM" or "128bpm"
        double bpm = 120.0;
        var bpmMatch = Regex.Match(folderName, @"\b(\d{2,3})\s*(?:bpm|BPM)\b");
        if (bpmMatch.Success && double.TryParse(bpmMatch.Groups[1].Value, out double parsedBpm))
        {
            bpm = parsedBpm;
        }

        // Auto-detect Key from folder name, e.g. "Key D", "Key Bbm", "Key F#"
        MusicalKey key = MusicalKey.C;
        KeyScale scale = KeyScale.Major;
        var keyMatch = Regex.Match(folderName, @"\b(?:Key|key|KEY)\s*([A-G][b#]?)(m|min|maj)?\b");
        if (keyMatch.Success)
        {
            string keyName = keyMatch.Groups[1].Value;
            if (Enum.TryParse<MusicalKey>(keyName, true, out var parsedKey))
            {
                key = parsedKey;
            }
            if (keyMatch.Groups[2].Success && (keyMatch.Groups[2].Value == "m" || keyMatch.Groups[2].Value == "min"))
            {
                scale = KeyScale.Minor;
            }
        }

        // Clean song title (strip BPM and Key from folder name)
        string title = Regex.Replace(folderName, @"\s*[-_]?\s*(?:\b\d{2,3}\s*(?:bpm|BPM)\b|\b(?:Key|key|KEY)\s*[A-G][b#]?m?\b)\s*", "").Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = folderName;
        }

        var song = new Song(title, bpm, key, scale)
        {
            Artist = "Imported Multitrack",
            Duration = TimeSpan.Zero,
            SourceDirectory = directoryPath
        };

        // Find all audio files
        var audioFiles = dirInfo.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
            .Where(f => SupportedExtensions.Contains(f.Extension.ToLowerInvariant()))
            .OrderBy(f => f.Name)
            .ToList();

        if (audioFiles.Count == 0)
        {
            throw new InvalidOperationException($"No supported audio stems found in '{directoryPath}'. Supported formats: {string.Join(", ", SupportedExtensions)}");
        }

        TimeSpan maxDuration = TimeSpan.FromSeconds(30);

        foreach (var file in audioFiles)
        {
            string stemName = Path.GetFileNameWithoutExtension(file.Name);
            // Clean common leading numbers like "01_Click" -> "Click"
            stemName = Regex.Replace(stemName, @"^\d+[\s\-_]+", "");

            var category = _categoryDetector.DetectCategory(stemName).Category;
            var stem = new StemTrack(stemName, file.FullName, category)
            {
                Volume = 0.85f,
                OutputBusIndex = StemCategoryDetector.GetDefaultMackieOutputBus(category)
            };

            // Calculate duration via BASS
            try
            {
                int stream = Bass.CreateStream(file.FullName, 0, 0, BassFlags.Decode | BassFlags.Float);
                if (stream != 0)
                {
                    long lengthBytes = Bass.ChannelGetLength(stream, PositionFlags.Bytes);
                    if (lengthBytes > 0)
                    {
                        double seconds = Bass.ChannelBytes2Seconds(stream, lengthBytes);
                        if (seconds > maxDuration.TotalSeconds)
                        {
                            maxDuration = TimeSpan.FromSeconds(seconds);
                        }
                    }
                    Bass.StreamFree(stream);
                }
            }
            catch
            {
                // Non-fatal if audio duration calculation fallback
            }

            song.Stems.Add(stem);
        }

        song.Duration = maxDuration;
        BuildDefaultArrangement(song);

        return song;
    }

    /// <summary>
    /// Generates real audible PCM WAV stems for immediate instant testing in the application.
    /// Creates a complete multitrack with Click, Guide, Drums, Bass, Keys, and Pad in high quality.
    /// </summary>
    public Song CreateSyntheticDemoSong(string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);

        double bpm = 72.0;
        int sampleRate = 44100;
        double durationSeconds = 60.0; // 1 minute demo song
        int totalSamples = (int)(durationSeconds * sampleRate);

        string clickPath = Path.Combine(outputFolder, "01_Click.wav");
        string guidePath = Path.Combine(outputFolder, "02_Guide.wav");
        string drumsPath = Path.Combine(outputFolder, "03_Drums.wav");
        string bassPath = Path.Combine(outputFolder, "04_Bass.wav");
        string keysPath = Path.Combine(outputFolder, "05_Keys.wav");
        string padPath = Path.Combine(outputFolder, "06_Pad.wav");

        double beatInterval = 60.0 / bpm;
        int samplesPerBeat = (int)(beatInterval * sampleRate);

        // 1. Generate Click (Accent high pitch on beat 1, low pitch on 2, 3, 4)
        if (!File.Exists(clickPath))
        {
            var clickSamples = new short[totalSamples];
            for (int sample = 0; sample < totalSamples; sample++)
            {
                int beatIndex = sample / samplesPerBeat;
                int sampleInBeat = sample % samplesPerBeat;
                int beatInBar = beatIndex % 4; // 0, 1, 2, 3

                double clickFreq = beatInBar == 0 ? 1500.0 : 800.0;
                int clickLength = sampleRate / 25; // 40ms pulse

                if (sampleInBeat < clickLength)
                {
                    double env = 1.0 - ((double)sampleInBeat / clickLength);
                    double wave = Math.Sin(2.0 * Math.PI * clickFreq * sampleInBeat / sampleRate);
                    clickSamples[sample] = (short)(wave * env * 22000);
                }
            }
            WriteMonoPcmWav(clickPath, clickSamples, sampleRate);
        }

        // 2. Generate Guide (Beep alerts 2 beats before section transitions at 0s, 15s, 35s, 50s)
        if (!File.Exists(guidePath))
        {
            var guideSamples = new short[totalSamples];
            int[] cuePointsSeconds = { 13, 33, 48 }; // Pre-cues for Verse, Chorus, Outro
            foreach (int cueSec in cuePointsSeconds)
            {
                int startSample = cueSec * sampleRate;
                for (int i = 0; i < sampleRate * 2 && (startSample + i) < totalSamples; i++)
                {
                    int beatOffset = i % (samplesPerBeat / 2);
                    if (beatOffset < sampleRate / 20)
                    {
                        double env = 1.0 - ((double)beatOffset / (sampleRate / 20));
                        guideSamples[startSample + i] = (short)(Math.Sin(2.0 * Math.PI * 1200.0 * beatOffset / sampleRate) * env * 18000);
                    }
                }
            }
            WriteMonoPcmWav(guidePath, guideSamples, sampleRate);
        }

        // 3. Generate Drums (Kick on 1 & 3, Snare/Clap on 2 & 4, HiHat 8ths)
        if (!File.Exists(drumsPath))
        {
            var drumSamples = new short[totalSamples];
            var rng = new Random(42);
            for (int sample = 0; sample < totalSamples; sample++)
            {
                int sampleInBeat = sample % samplesPerBeat;
                int beatIndex = sample / samplesPerBeat;
                int beatInBar = beatIndex % 4;

                double val = 0.0;

                // Kick on beat 0 and 2
                if ((beatInBar == 0 || beatInBar == 2) && sampleInBeat < sampleRate / 5)
                {
                    double env = Math.Exp(-sampleInBeat / (sampleRate * 0.04));
                    double pitch = 55.0 * Math.Exp(-sampleInBeat / (sampleRate * 0.02));
                    val += Math.Sin(2.0 * Math.PI * pitch * sampleInBeat / sampleRate) * env * 0.7;
                }

                // Snare on beat 1 and 3
                if ((beatInBar == 1 || beatInBar == 3) && sampleInBeat < sampleRate / 4)
                {
                    double env = Math.Exp(-sampleInBeat / (sampleRate * 0.05));
                    double noise = (rng.NextDouble() * 2.0 - 1.0);
                    double body = Math.Sin(2.0 * Math.PI * 180.0 * sampleInBeat / sampleRate);
                    val += (noise * 0.6 + body * 0.4) * env * 0.6;
                }

                // HiHat on 8th notes
                int eighthLength = samplesPerBeat / 2;
                int sampleInEighth = sample % eighthLength;
                if (sampleInEighth < sampleRate / 40)
                {
                    double env = Math.Exp(-sampleInEighth / (sampleRate * 0.01));
                    val += (rng.NextDouble() * 2.0 - 1.0) * env * 0.25;
                }

                drumSamples[sample] = (short)(Math.Clamp(val, -1.0, 1.0) * 26000);
            }
            WriteMonoPcmWav(drumsPath, drumSamples, sampleRate);
        }

        // 4. Generate Bass (Progression in E Major: E - B - C#m - A)
        if (!File.Exists(bassPath))
        {
            var bassSamples = new short[totalSamples];
            double[] bassFreqs = { 41.20, 61.74, 69.30, 55.00 }; // E1, B1, C#2, A1
            for (int sample = 0; sample < totalSamples; sample++)
            {
                int barIndex = sample / (samplesPerBeat * 4);
                double freq = bassFreqs[barIndex % bassFreqs.Length];
                int sampleInBeat = sample % samplesPerBeat;

                double env = Math.Exp(-sampleInBeat / (sampleRate * 0.35));
                double wave = Math.Sin(2.0 * Math.PI * freq * sample / sampleRate)
                            + 0.5 * Math.Sin(4.0 * Math.PI * freq * sample / sampleRate);
                bassSamples[sample] = (short)(wave * env * 18000);
            }
            WriteMonoPcmWav(bassPath, bassSamples, sampleRate);
        }

        // 5. Generate Keys (E Major acoustic arpeggio)
        if (!File.Exists(keysPath))
        {
            var keysSamples = new short[totalSamples];
            double[][] chordNotes = {
                new[] { 329.63, 415.30, 493.88 }, // E Maj (E4, G#4, B4)
                new[] { 246.94, 370.00, 493.88 }, // B Maj
                new[] { 277.18, 329.63, 415.30 }, // C#m
                new[] { 220.00, 277.18, 329.63 }  // A Maj
            };

            for (int sample = 0; sample < totalSamples; sample++)
            {
                int barIndex = sample / (samplesPerBeat * 4);
                var chord = chordNotes[barIndex % chordNotes.Length];
                int eighthIndex = (sample / (samplesPerBeat / 2)) % chord.Length;
                double noteFreq = chord[eighthIndex];
                int sampleInEighth = sample % (samplesPerBeat / 2);

                double env = Math.Exp(-sampleInEighth / (sampleRate * 0.25));
                double wave = Math.Sin(2.0 * Math.PI * noteFreq * sample / sampleRate);
                keysSamples[sample] = (short)(wave * env * 14000);
            }
            WriteMonoPcmWav(keysPath, keysSamples, sampleRate);
        }

        // 6. Generate Ambient Pad (Lush stereo chorus warm pad)
        if (!File.Exists(padPath))
        {
            var padSamples = new short[totalSamples];
            double[][] padChords = {
                new[] { 164.81, 207.65, 246.94, 329.63 }, // E Maj
                new[] { 123.47, 185.00, 246.94, 370.00 }, // B Maj
                new[] { 138.59, 164.81, 207.65, 277.18 }, // C#m
                new[] { 110.00, 138.59, 164.81, 220.00 }  // A Maj
            };

            for (int sample = 0; sample < totalSamples; sample++)
            {
                int barIndex = sample / (samplesPerBeat * 4);
                var chord = padChords[barIndex % padChords.Length];
                double sum = 0.0;
                foreach (double f in chord)
                {
                    sum += Math.Sin(2.0 * Math.PI * f * sample / sampleRate) * 0.25;
                }
                padSamples[sample] = (short)(sum * 16000);
            }
            WriteMonoPcmWav(padPath, padSamples, sampleRate);
        }

        var song = new Song("Way Maker (Live Demo)", bpm, MusicalKey.E, KeyScale.Major)
        {
            Artist = "EasyTracks Live MultiTracks",
            Duration = TimeSpan.FromSeconds(durationSeconds)
        };

        song.Stems.Add(new StemTrack("Click", clickPath, StemCategory.Click) { Volume = 0.85f, OutputBusIndex = 7 });
        song.Stems.Add(new StemTrack("Guide", guidePath, StemCategory.Guide) { Volume = 0.90f, OutputBusIndex = 7 });
        song.Stems.Add(new StemTrack("Drums", drumsPath, StemCategory.Drums) { Volume = 0.95f, OutputBusIndex = 3 });
        song.Stems.Add(new StemTrack("Bass", bassPath, StemCategory.Bass) { Volume = 0.90f, OutputBusIndex = 10 });
        song.Stems.Add(new StemTrack("Keys", keysPath, StemCategory.Keys) { Volume = 0.80f, Pan = -0.3f, OutputBusIndex = 113 });
        song.Stems.Add(new StemTrack("Ambient Pad", padPath, StemCategory.Keys) { Volume = 0.75f, Pan = 0.3f, OutputBusIndex = 113 });

        BuildDefaultArrangement(song);
        return song;
    }

    private void BuildDefaultArrangement(Song song)
    {
        double totalSecs = song.Duration.TotalSeconds;
        if (totalSecs <= 0) totalSecs = 180;

        // Newly imported songs start with a single full-song waveform span (no arbitrary fake sections)
        // so the user can place section markers snapped to exact bars (Playback / Prime style).
        var arr = new Arrangement("Original");
        arr.Sections.Add(new SectionMarker("SECUENCIA", SectionType.Custom, TimeSpan.Zero, TimeSpan.FromSeconds(totalSecs), "#0284C7"));

        song.Arrangements.Add(arr);
        song.ActiveArrangementId = arr.Id;
    }

    private static void WriteMonoPcmWav(string filePath, short[] samples, int sampleRate)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        int byteRate = sampleRate * 1 * 2; // 1 channel, 16-bit
        int subChunk2Size = samples.Length * 2;
        int chunkSize = 36 + subChunk2Size;

        // RIFF header
        writer.Write("RIFF"u8.ToArray());
        writer.Write(chunkSize);
        writer.Write("WAVE"u8.ToArray());

        // fmt chunk
        writer.Write("fmt "u8.ToArray());
        writer.Write(16); // Subchunk1Size
        writer.Write((short)1); // AudioFormat PCM = 1
        writer.Write((short)1); // NumChannels = 1 (Mono)
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)2); // BlockAlign = 2
        writer.Write((short)16); // BitsPerSample = 16

        // data chunk
        writer.Write("data"u8.ToArray());
        writer.Write(subChunk2Size);

        // write audio samples
        foreach (short sample in samples)
        {
            writer.Write(sample);
        }
    }

    /// <summary>
    /// Extracts real audio amplitude peaks for each section of a song by sequentially decoding musical stems with BASS.
    /// Uses sequential float sample reading (ChannelGetData) instead of random seeks so .m4a, .aac, .mp3, and .wav stems
    /// always render a rich, accurate soundwave.
    /// </summary>
    public List<float[]>? ExtractSectionPeaksForSong(Song song, IReadOnlyList<SectionMarker> sections, int barsPerSection = 40)
    {
        if (sections.Count == 0 || song.Stems.Count == 0) return null;

        // Pick up to 4 representative musical stems (avoiding Click/Guide and SynthCache stems)
        var candidateStems = song.Stems
            .Where(s => s.Category != StemCategory.Click
                     && s.Category != StemCategory.Guide
                     && !string.IsNullOrEmpty(s.FilePath)
                     && !s.FilePath.Contains("SynthCache", StringComparison.OrdinalIgnoreCase)
                     && File.Exists(s.FilePath))
            .OrderByDescending(s => s.Category == StemCategory.Drums
                                 || s.Category == StemCategory.Piano
                                 || s.Category == StemCategory.Keys
                                 || s.Category == StemCategory.AcousticGuitar
                                 || s.Category == StemCategory.ElectricGuitar
                                 || s.Category == StemCategory.Bass)
            .Take(4)
            .ToList();

        if (candidateStems.Count == 0)
        {
            candidateStems = song.Stems
                .Where(s => !string.IsNullOrEmpty(s.FilePath) && File.Exists(s.FilePath))
                .Take(2)
                .ToList();
        }

        if (candidateStems.Count == 0) return null;

        const int totalEnvelopeBins = 400;
        float[] fullSongEnvelope = new float[totalEnvelopeBins];
        double maxDetectedSeconds = song.Duration.TotalSeconds;
        float globalMax = 0.001f;

        float[] readBuffer = new float[16384];

        foreach (var stem in candidateStems)
        {
            int stream = 0;
            try
            {
                stream = Bass.CreateStream(stem.FilePath, 0, 0, BassFlags.Decode | BassFlags.Float);
                if (stream == 0) continue;

                long totalBytes = Bass.ChannelGetLength(stream, PositionFlags.Bytes);
                double fileSeconds = totalBytes > 0 ? Bass.ChannelBytes2Seconds(stream, totalBytes) : maxDetectedSeconds;
                if (fileSeconds > maxDetectedSeconds)
                {
                    maxDetectedSeconds = fileSeconds;
                }

                if (totalBytes <= 0)
                {
                    // Estimate total bytes from duration if container header doesn't report exact byte length
                    double estSec = maxDetectedSeconds > 5 ? maxDetectedSeconds : 240.0;
                    totalBytes = Math.Max(1, Bass.ChannelSeconds2Bytes(stream, estSec));
                }

                long bytesReadTotal = 0;
                while (true)
                {
                    int bytesRead = Bass.ChannelGetData(stream, readBuffer, readBuffer.Length * sizeof(float));
                    if (bytesRead <= 0) break;

                    int floatsRead = bytesRead / sizeof(float);
                    int binIndex = (int)Math.Clamp((bytesReadTotal * totalEnvelopeBins) / Math.Max(1L, totalBytes), 0, totalEnvelopeBins - 1);

                    float chunkPeak = 0f;
                    for (int i = 0; i < floatsRead; i += 4)
                    {
                        float abs = MathF.Abs(readBuffer[i]);
                        if (abs > chunkPeak) chunkPeak = abs;
                    }

                    if (chunkPeak > fullSongEnvelope[binIndex])
                    {
                        fullSongEnvelope[binIndex] = Math.Max(fullSongEnvelope[binIndex], chunkPeak);
                    }
                    else
                    {
                        fullSongEnvelope[binIndex] += chunkPeak * 0.35f;
                    }

                    if (fullSongEnvelope[binIndex] > globalMax)
                    {
                        globalMax = fullSongEnvelope[binIndex];
                    }

                    bytesReadTotal += bytesRead;
                }
            }
            catch
            {
                // Ignore individual stem decode errors
            }
            finally
            {
                if (stream != 0) Bass.StreamFree(stream);
            }
        }

        if (maxDetectedSeconds > 5 && Math.Abs(song.Duration.TotalSeconds - maxDetectedSeconds) > 1.0)
        {
            song.Duration = TimeSpan.FromSeconds(maxDetectedSeconds);
            if (sections.Count == 1 && sections[0].StartTime == TimeSpan.Zero)
            {
                sections[0].EndTime = song.Duration;
            }
        }

        double songTotalSec = Math.Max(1.0, maxDetectedSeconds > 1.0 ? maxDetectedSeconds : song.Duration.TotalSeconds);
        var result = new List<float[]>(sections.Count);

        for (int s = 0; s < sections.Count; s++)
        {
            var sec = sections[s];
            double startRatio = Math.Clamp(sec.StartTime.TotalSeconds / songTotalSec, 0.0, 1.0);
            double endRatio = Math.Clamp(sec.EndTime.TotalSeconds / songTotalSec, startRatio + 0.005, 1.0);

            int sectionBars = Math.Max(8, barsPerSection);
            var peaks = new float[sectionBars];

            for (int i = 0; i < sectionBars; i++)
            {
                double t0 = startRatio + ((double)i / sectionBars) * (endRatio - startRatio);
                double t1 = startRatio + ((double)(i + 1) / sectionBars) * (endRatio - startRatio);

                int binStart = Math.Clamp((int)(t0 * totalEnvelopeBins), 0, totalEnvelopeBins - 1);
                int binEnd = Math.Clamp((int)Math.Ceiling(t1 * totalEnvelopeBins), binStart, totalEnvelopeBins - 1);

                float localPeak = 0f;
                for (int b = binStart; b <= binEnd; b++)
                {
                    if (fullSongEnvelope[b] > localPeak) localPeak = fullSongEnvelope[b];
                }

                float norm = localPeak / globalMax;
                peaks[i] = Math.Clamp(MathF.Pow(norm, 0.7f), 0.14f, 1.0f);
            }

            result.Add(peaks);
        }

        return result;
    }
}
