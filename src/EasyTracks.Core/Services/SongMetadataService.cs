using System.Net.Http;
using System.Text.RegularExpressions;
using EasyTracks.Domain.Models;

namespace EasyTracks.Core.Services;

public record ScrapedSongMetadata(
    string Title,
    string Artist,
    string Album,
    double Bpm,
    MusicalKey Key,
    KeyScale Scale,
    string TimeSignature,
    TimeSpan Duration,
    string? CoverImageUrl,
    List<string> Sections
);

public class SongMetadataService
{
    private readonly HttpClient _httpClient;

    public SongMetadataService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }
    }

    /// <summary>
    /// Fetches song arrangement and metadata from MultiTracks.com or Secuencias.com.
    /// Falls back cleanly if fields are not present.
    /// </summary>
    public async Task<ScrapedSongMetadata> FetchMetadataFromUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be empty.", nameof(url));
        }

        string html = await _httpClient.GetStringAsync(url);
        return ParseHtmlMetadata(url, html);
    }

    public ScrapedSongMetadata ParseHtmlMetadata(string url, string html)
    {
        string title = "";
        string artist = "";
        string album = "";
        double bpm = 120.0;
        MusicalKey key = MusicalKey.C;
        KeyScale scale = KeyScale.Major;
        string timeSignature = "4/4";
        TimeSpan duration = TimeSpan.FromMinutes(4.0);
        string? coverUrl = null;
        var sections = new List<string>();

        // 1. Title Extraction
        // Check for MultiTracks/Secuencias specific: <h1 class="song-banner--title...">Title<br />...</h1>
        var bannerTitleMatch = Regex.Match(html, @"<h1[^>]*class=""[^""]*song-banner--title[^""]*""[^>]*>(.*?)</h1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (bannerTitleMatch.Success)
        {
            string raw = bannerTitleMatch.Groups[1].Value;
            // Remove <span class="song-banner--title--translated">...</span> or take primary title
            var clean = Regex.Replace(raw, @"<[^>]+>", " ").Trim();
            clean = System.Net.WebUtility.HtmlDecode(clean);
            var parts = clean.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) title = parts[0].Trim();
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            var ogTitleMatch = Regex.Match(html, @"<meta\s+property=""og:title""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
            if (ogTitleMatch.Success)
            {
                string rawTitle = System.Net.WebUtility.HtmlDecode(ogTitleMatch.Groups[1].Value);
                var parts = rawTitle.Split(new[] { " by ", " - ", " | " }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0) title = parts[0].Trim();
                if (parts.Length > 1 && string.IsNullOrWhiteSpace(artist)) artist = parts[1].Trim();
            }
        }

        // 2. Artist & Album Extraction
        // <h2 class="song-banner--artist"><a href="/artists/...">Leeland</a> - <a href="/songs/...">Better Word</a></h2>
        var bannerArtistMatch = Regex.Match(html, @"<h2[^>]*class=""[^""]*song-banner--artist[^""]*""[^>]*>(.*?)</h2>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (bannerArtistMatch.Success)
        {
            var links = Regex.Matches(bannerArtistMatch.Groups[1].Value, @"<a[^>]*>(.*?)</a>", RegexOptions.IgnoreCase);
            if (links.Count > 0) artist = System.Net.WebUtility.HtmlDecode(links[0].Groups[1].Value).Trim();
            if (links.Count > 1) album = System.Net.WebUtility.HtmlDecode(links[1].Groups[1].Value).Trim();
        }

        if (string.IsNullOrWhiteSpace(artist))
        {
            var artistFallback = Regex.Match(html, @"class=""(?:artist|author|subtitle)[^""]*""[^>]*>([^<]+)<", RegexOptions.IgnoreCase);
            if (artistFallback.Success) artist = System.Net.WebUtility.HtmlDecode(artistFallback.Groups[1].Value).Trim();
        }

        // 3. Cover Image
        var ogImageMatch = Regex.Match(html, @"<meta\s+property=""og:image""\s+content=""([^""]+)""", RegexOptions.IgnoreCase);
        if (ogImageMatch.Success)
        {
            coverUrl = ogImageMatch.Groups[1].Value;
        }

        // 4. Parse all <dt>...</dt><dd>...</dd> pairs in the page (MultiTracks.com & Secuencias.com song-banner--meta-list)
        bool foundBpm = false;
        bool foundKey = false;
        bool foundTimeSig = false;
        bool foundDuration = false;

        var dtDdMatches = Regex.Matches(html, @"<dt\b[^>]*>(.*?)</dt>\s*<dd\b[^>]*>(.*?)</dd>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (Match m in dtDdMatches)
        {
            string rawLabel = Regex.Replace(m.Groups[1].Value, @"<[^>]+>", " ");
            string label = System.Net.WebUtility.HtmlDecode(rawLabel).Trim().TrimEnd(':').Trim();

            string rawVal = Regex.Replace(m.Groups[2].Value, @"<[^>]+>", " ");
            string val = System.Net.WebUtility.HtmlDecode(rawVal).Trim();

            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(val)) continue;

            // BPM / Tempo
            if (!foundBpm && (label.Contains("BPM", StringComparison.OrdinalIgnoreCase) ||
                              label.Contains("Tempo", StringComparison.OrdinalIgnoreCase)))
            {
                var numMatch = Regex.Match(val, @"(\d{2,3}(?:\.\d+)?)");
                if (numMatch.Success && double.TryParse(numMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedBpm) && parsedBpm >= 30 && parsedBpm <= 300)
                {
                    bpm = Math.Round(parsedBpm, 1);
                    foundBpm = true;
                }
            }
            // Time Signature / Compás / Métrica / Meter
            else if (!foundTimeSig && (label.Contains("Time", StringComparison.OrdinalIgnoreCase) ||
                                       label.Contains("Compás", StringComparison.OrdinalIgnoreCase) ||
                                       label.Contains("Compas", StringComparison.OrdinalIgnoreCase) ||
                                       label.Contains("Métrica", StringComparison.OrdinalIgnoreCase) ||
                                       label.Contains("Metrica", StringComparison.OrdinalIgnoreCase) ||
                                       label.Contains("Meter", StringComparison.OrdinalIgnoreCase)))
            {
                var tsMatch = Regex.Match(val, @"(\d{1,2}\s*/\s*\d{1,2})");
                if (tsMatch.Success)
                {
                    timeSignature = tsMatch.Groups[1].Value.Replace(" ", "");
                    foundTimeSig = true;
                }
            }
            // Key / Tono
            else if (!foundKey && (label.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
                                   label.Contains("Tono", StringComparison.OrdinalIgnoreCase)))
            {
                var kMatch = Regex.Match(val, @"([A-G][b#♯♭]?)(m|min|maj)?\b", RegexOptions.IgnoreCase);
                if (kMatch.Success && MusicalKeyExtensions.TryParseMusicalKey(kMatch.Groups[1].Value, out var parsedKey))
                {
                    key = parsedKey;
                    if (kMatch.Groups[2].Success && kMatch.Groups[2].Value.StartsWith("m", StringComparison.OrdinalIgnoreCase) &&
                        !kMatch.Groups[2].Value.StartsWith("maj", StringComparison.OrdinalIgnoreCase))
                    {
                        scale = KeyScale.Minor;
                    }
                    foundKey = true;
                }
            }
            // Duration / Length / Duración
            else if (!foundDuration && (label.Contains("Length", StringComparison.OrdinalIgnoreCase) ||
                                       label.Contains("Duración", StringComparison.OrdinalIgnoreCase) ||
                                       label.Contains("Duracion", StringComparison.OrdinalIgnoreCase) ||
                                       label.Contains("Duration", StringComparison.OrdinalIgnoreCase)))
            {
                var durMatch = Regex.Match(val, @"(\d{1,2}):(\d{2})");
                if (durMatch.Success && int.TryParse(durMatch.Groups[1].Value, out int min) && int.TryParse(durMatch.Groups[2].Value, out int sec))
                {
                    duration = TimeSpan.FromMinutes(min) + TimeSpan.FromSeconds(sec);
                    foundDuration = true;
                }
            }
            // Album / Arrangement fallback from meta list
            else if (string.IsNullOrWhiteSpace(album) && (label.Contains("Album", StringComparison.OrdinalIgnoreCase) ||
                                                          label.Contains("Álbum", StringComparison.OrdinalIgnoreCase)))
            {
                album = val;
            }
        }

        // 5. Structured data-attributes & JSON-LD fallbacks for BPM, TimeSignature, Key, Duration
        if (!foundBpm)
        {
            var dataBpm = Regex.Match(html, @"(?:data-bpm|""bpm""|""tempo"")\s*[=:]\s*[""']?(\d{2,3}(?:\.\d+)?)[""']?", RegexOptions.IgnoreCase);
            if (dataBpm.Success && double.TryParse(dataBpm.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dbpm) && dbpm >= 35 && dbpm <= 260)
            {
                bpm = Math.Round(dbpm, 1);
            }
        }

        if (!foundTimeSig)
        {
            var dataTs = Regex.Match(html, @"(?:data-time-sig(?:nature)?|""timeSignature""|""meter"")\s*[=:]\s*[""'](\d{1,2}/\d{1,2})[""']", RegexOptions.IgnoreCase);
            if (dataTs.Success)
            {
                timeSignature = dataTs.Groups[1].Value;
            }
            else
            {
                // Restrict search to song-banner area if present
                var bannerBlock = Regex.Match(html, @"song-banner--meta-list.*?</dl>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                string searchScope = bannerBlock.Success ? bannerBlock.Value : html;
                var tsFallback = Regex.Match(searchScope, @"\b(4/4|6/8|3/4|12/8|2/4|5/4)\b");
                if (tsFallback.Success) timeSignature = tsFallback.Groups[1].Value;
            }
        }

        if (!foundKey)
        {
            var dataKey = Regex.Match(html, @"(?:data-key|""originalKey""|""key"")\s*[=:]\s*[""']([A-G][b#♯♭]?)(m|min)?[""']", RegexOptions.IgnoreCase);
            if (dataKey.Success && MusicalKeyExtensions.TryParseMusicalKey(dataKey.Groups[1].Value, out var dKey))
            {
                key = dKey;
                if (dataKey.Groups[2].Success && dataKey.Groups[2].Value.StartsWith("m", StringComparison.OrdinalIgnoreCase))
                {
                    scale = KeyScale.Minor;
                }
            }
        }

        if (!foundDuration)
        {
            var durationSecMatch = Regex.Match(html, @"data-song-duration=""([\d.]+)""", RegexOptions.IgnoreCase);
            if (durationSecMatch.Success && double.TryParse(durationSecMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double secs) && secs > 10)
            {
                duration = TimeSpan.FromSeconds(secs);
            }
        }

        // 8. Song Map / Sections from song-banner--song-sections
        // In MultiTracks/Secuencias: <span class="song-banner--tip">Intro</span>
        var tipMatches = Regex.Matches(html, @"class=""song-banner--tip"">([^<]+)</span>", RegexOptions.IgnoreCase);
        if (tipMatches.Count > 0)
        {
            foreach (Match m in tipMatches)
            {
                string sName = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
                if (!string.IsNullOrWhiteSpace(sName))
                {
                    sections.Add(sName);
                }
            }
        }
        else
        {
            var sectionMatches = Regex.Matches(html, @"\b(Intro|Verse\s*\d*|Verso\s*\d*|Chorus\s*\d*|Coro\s*\d*|Pre-Chorus|Pre-Coro|Bridge\s*\d*|Puente\s*\d*|Refrain|Instrumental|Interlude|Interludio|Outro|Ending|Final)\b", RegexOptions.IgnoreCase);
            foreach (Match m in sectionMatches)
            {
                string cleanSec = m.Value.Trim();
                cleanSec = char.ToUpper(cleanSec[0]) + cleanSec.Substring(1);
                if (sections.Count < 24)
                {
                    sections.Add(cleanSec);
                }
            }
        }

        if (sections.Count == 0)
        {
            sections.AddRange(new[] { "Intro", "Verse 1", "Chorus", "Verse 2", "Chorus", "Bridge", "Chorus", "Outro" });
        }

        if (string.IsNullOrWhiteSpace(title)) title = "Imported Multitrack";
        if (string.IsNullOrWhiteSpace(artist)) artist = "Original Artist";

        return new ScrapedSongMetadata(title, artist, album, bpm, key, scale, timeSignature, duration, coverUrl, sections);
    }
}
