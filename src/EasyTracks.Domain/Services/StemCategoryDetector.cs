using System.Text.RegularExpressions;
using EasyTracks.Domain.Models;

namespace EasyTracks.Domain.Services;

public class StemCategoryDetector
{
    private static readonly Dictionary<StemCategory, string[]> CategoryKeywords = new()
    {
        [StemCategory.Click] = new[] { "click", "metronomo", "metronome", "tik", "tempo" },
        [StemCategory.Guide] = new[] { "guide", "guia", "cue", "cues", "slate", "voice", "director" },
        [StemCategory.Drums] = new[] { "drum", "drums", "bateria", "baca", "kick", "snare", "hihat", "tom", "overhead", "cymbals" },
        [StemCategory.Percussion] = new[] { "perc", "percussion", "percusión", "shaker", "tambourine", "conga", "bongo", "cajon", "loop" },
        [StemCategory.Bass] = new[] { "bass", "bajo", "sub", "synth bass", "808" },
        [StemCategory.AcousticGuitar] = new[] { "acoustic", "acustica", "acoust", "ag", "guit ac", "guitarra ac" },
        [StemCategory.ElectricGuitar] = new[] { "electric", "electrica", "elec", "eg", "gtr", "guitar", "guitarra", "lead gtr", "overdrive", "dist" },
        [StemCategory.Piano] = new[] { "piano", "grand", "rhodes", "wurlitzer", "ac piano" },
        [StemCategory.Keys] = new[] { "keys", "teclado", "teclados", "organ", "organo", "hammond", "clav" },
        [StemCategory.Synth] = new[] { "synth", "sintetizador", "lead synth", "arp", "arpeggio", "pluck" },
        [StemCategory.Strings] = new[] { "strings", "cuerdas", "violin", "viola", "cello", "orchestra" },
        [StemCategory.Brass] = new[] { "brass", "horns", "trompeta", "trumpet", "trombone", "sax", "saxo" },
        [StemCategory.LeadVocal] = new[] { "lead vox", "lead vocal", "voz lider", "voz principal", "vox lead", "lead" },
        [StemCategory.BackingVocals] = new[] { "bgv", "bgvs", "backing", "harmonies", "harmony", "coros", "voces" },
        [StemCategory.Choir] = new[] { "choir", "coro" },
        [StemCategory.SoundEffects] = new[] { "fx", "sfx", "sweep", "riser", "downlifter", "impact", "effects", "efectos" }
    };

    public (StemCategory Category, double Confidence) DetectCategory(string fileNameOrPath)
    {
        var cleanName = Path.GetFileNameWithoutExtension(fileNameOrPath).ToLowerInvariant();
        cleanName = cleanName.Replace("_", " ").Replace("-", " ").Replace(".", " ");

        // Priority 1: Exact / High confidence checks
        foreach (var (category, keywords) in CategoryKeywords)
        {
            foreach (var kw in keywords)
            {
                // Word boundary check
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
}
