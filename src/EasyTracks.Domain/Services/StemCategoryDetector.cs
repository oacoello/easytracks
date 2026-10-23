using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EasyTracks.Domain.Models;

namespace EasyTracks.Domain.Services;

public class StemCategoryDetector
{
    private static readonly Dictionary<StemCategory, string[]> CategoryKeywords = new()
    {
        [StemCategory.Click] = new[] { "click track", "click", "metronomo", "metronome", "tik", "tempo" },
        [StemCategory.Guide] = new[] { "guide (spanish)", "guia (spanish)", "guide", "guia", "cues", "cue", "slate", "voice", "director" },
        [StemCategory.Drums] = new[] { "drums", "drum", "bateria", "baca", "kick", "snare", "hihat", "tom", "overhead", "cymbals" },
        [StemCategory.Percussion] = new[] { "percussion", "percusion", "perc", "shaker", "tambourine", "conga", "bongo", "cajon", "loop" },
        [StemCategory.Bass] = new[] { "synth bass", "bass", "bajo", "sub", "808" },
        [StemCategory.AcousticGuitar] = new[] { "acoustic guitar", "guitarra acustica", "acoustic", "acustica", "acoust", "ag", "guit ac", "guitarra ac" },
        [StemCategory.ElectricGuitar] = new[] { "electric guitar", "guitarra electrica", "electric", "electrica", "elec", "eg1", "eg2", "eg3", "eg", "gtr", "guitar", "guitarra", "lead gtr", "overdrive", "dist" },
        [StemCategory.Piano] = new[] { "ac piano", "piano", "grand", "rhodes", "wurlitzer", "pno" },
        [StemCategory.Keys] = new[] { "keys", "teclados", "teclado", "organo", "organ", "hammond", "clav", "pad", "pads" },
        [StemCategory.Synth] = new[] { "lead synth", "synth", "sintetizador", "arp", "arpeggio", "pluck" },
        [StemCategory.Strings] = new[] { "strings", "cuerdas", "violin", "viola", "cello", "orchestra" },
        [StemCategory.Brass] = new[] { "brass", "horns", "trompeta", "trumpet", "trombone", "sax", "saxo" },
        [StemCategory.LeadVocal] = new[] { "lead vox", "lead vocal", "voz lider", "voz principal", "vox lead" },
        [StemCategory.BackingVocals] = new[] { "bgvs", "bgv", "backing", "harmonies", "harmony", "coros", "voces", "vox" },
        [StemCategory.Choir] = new[] { "choir", "coro" },
        [StemCategory.SoundEffects] = new[] { "sfx", "fx", "sweep", "riser", "downlifter", "impact", "effects", "efectos" }
    };

    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        string normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public (StemCategory Category, double Confidence) DetectCategory(string fileNameOrPath)
    {
        var rawName = Path.GetFileNameWithoutExtension(fileNameOrPath);
        if (string.IsNullOrWhiteSpace(rawName)) rawName = fileNameOrPath;

        var cleanName = RemoveDiacritics(rawName).ToLowerInvariant();
        cleanName = cleanName.Replace("_", " ").Replace("-", " ").Replace(".", " ");

        // Priority 1: Word boundary check
        foreach (var (category, keywords) in CategoryKeywords)
        {
            foreach (var kw in keywords)
            {
                if (Regex.IsMatch(cleanName, $@"\b{Regex.Escape(kw)}\b", RegexOptions.IgnoreCase))
                {
                    return (category, 0.95);
                }
            }
        }

        // Priority 2: Substring matching
        foreach (var (category, keywords) in CategoryKeywords)
        {
            foreach (var kw in keywords)
            {
                if (cleanName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    return (category, 0.70);
                }
            }
        }

        return (StemCategory.Other, 0.0);
    }

    /// <summary>
    /// Returns the mixer bus group order requested:
    /// 0 = Click, 1 = Guide, 2 = Drums & Perc, 3 = Guitars (AG, EG, Bass), 4 = Keys, 5 = Everything else.
    /// </summary>
    public static int GetMixerBusOrder(StemCategory category) => category switch
    {
        StemCategory.Click => 0,
        StemCategory.Guide => 1,
        StemCategory.Drums or StemCategory.Percussion => 2,
        StemCategory.AcousticGuitar or StemCategory.ElectricGuitar or StemCategory.Bass => 3,
        StemCategory.Piano or StemCategory.Keys or StemCategory.Synth or StemCategory.Strings => 4,
        _ => 5
    };

    /// <summary>
    /// Default Mackie DL16S channel routing:
    /// Click & Guide -> Ch 7, AG -> Ch 9, Bass -> Ch 10, Piano -> Ch 11-12 Stereo (111).
    /// </summary>
    public static int GetDefaultMackieOutputBus(StemCategory category) => category switch
    {
        StemCategory.Click => 7,
        StemCategory.Guide => 7,
        StemCategory.AcousticGuitar => 9,
        StemCategory.Bass => 10,
        StemCategory.Piano => 111, // Stereo 11-12
        StemCategory.Drums => 3,
        StemCategory.Percussion => 4,
        StemCategory.ElectricGuitar => 8,
        StemCategory.Keys => 113, // Stereo 13-14
        StemCategory.Synth or StemCategory.Strings => 115, // Stereo 15-16
        StemCategory.LeadVocal or StemCategory.BackingVocals or StemCategory.Choir => 6,
        _ => 5
    };
}
