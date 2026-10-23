namespace EasyTracks.Domain.Models;

public static class MusicalKeyExtensions
{
    public static string ToDisplayString(this MusicalKey key) => key switch
    {
        MusicalKey.C => "C",
        MusicalKey.CSharp => "C#",
        MusicalKey.D => "D",
        MusicalKey.DSharp => "D#",
        MusicalKey.E => "E",
        MusicalKey.F => "F",
        MusicalKey.FSharp => "F#",
        MusicalKey.G => "G",
        MusicalKey.GSharp => "G#",
        MusicalKey.A => "A",
        MusicalKey.ASharp => "A#",
        MusicalKey.B => "B",
        _ => key.ToString()
    };

    public static bool TryParseMusicalKey(string text, out MusicalKey key)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            key = MusicalKey.C;
            return false;
        }

        string clean = text.Trim();
        clean = clean.Replace("♯", "#").Replace("♭", "b");

        // Normalize sharp / flat strings
        switch (clean.ToUpperInvariant())
        {
            case "C": key = MusicalKey.C; return true;
            case "C#":
            case "CSHARP":
            case "DB": key = MusicalKey.CSharp; return true;
            case "D": key = MusicalKey.D; return true;
            case "D#":
            case "DSHARP":
            case "EB": key = MusicalKey.DSharp; return true;
            case "E": key = MusicalKey.E; return true;
            case "F": key = MusicalKey.F; return true;
            case "F#":
            case "FSHARP":
            case "GB": key = MusicalKey.FSharp; return true;
            case "G": key = MusicalKey.G; return true;
            case "G#":
            case "GSHARP":
            case "AB": key = MusicalKey.GSharp; return true;
            case "A": key = MusicalKey.A; return true;
            case "A#":
            case "ASHARP":
            case "BB": key = MusicalKey.ASharp; return true;
            case "B": key = MusicalKey.B; return true;
            default:
                return Enum.TryParse(clean, true, out key);
        }
    }
}
