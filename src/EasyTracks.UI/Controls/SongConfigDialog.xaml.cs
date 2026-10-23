using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EasyTracks.Core.Services;
using EasyTracks.Domain.Models;

namespace EasyTracks_UI.Controls;

public sealed partial class SongConfigDialog : ContentDialog
{
    private readonly SongMetadataService _metadataService = new();
    public Song TargetSong { get; }

    public SongConfigDialog(Song targetSong)
    {
        TargetSong = targetSong;
        this.InitializeComponent();

        if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var accentStyle) && accentStyle is Style style)
        {
            this.PrimaryButtonStyle = style;
        }

        // Populate Key combo box with musical display notation
        var allKeys = new[]
        {
            MusicalKey.C, MusicalKey.CSharp, MusicalKey.D, MusicalKey.DSharp,
            MusicalKey.E, MusicalKey.F, MusicalKey.FSharp, MusicalKey.G,
            MusicalKey.GSharp, MusicalKey.A, MusicalKey.ASharp, MusicalKey.B
        };
        foreach (var key in allKeys)
        {
            KeyCombo.Items.Add(key.ToDisplayString());
        }

        // Initialize with song's existing values
        TitleBox.Text = targetSong.Title;
        ArtistBox.Text = targetSong.Artist;
        AlbumBox.Text = !string.IsNullOrWhiteSpace(targetSong.Album)
            ? targetSong.Album
            : (targetSong.ActiveArrangement?.Name ?? "Original");
        BpmBox.Text = targetSong.PlaybackBpm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        KeyCombo.SelectedItem = targetSong.OriginalKey.ToDisplayString();
        ScaleCombo.SelectedIndex = targetSong.OriginalScale == KeyScale.Minor ? 1 : 0;
        SelectTimeSignatureInCombo($"{targetSong.TimeSignatureNumerator}/{targetSong.TimeSignatureDenominator}");

        var activeArr = targetSong.ActiveArrangement;
        if (activeArr != null && activeArr.Sections.Count > 0)
        {
            SectionsBox.Text = string.Join(", ", activeArr.Sections.Select(s => s.Name));
        }
    }

    private void SelectTimeSignatureInCombo(string ts)
    {
        if (string.IsNullOrWhiteSpace(ts)) return;
        string cleanTs = ts.Trim();
        foreach (ComboBoxItem item in TimeSigCombo.Items)
        {
            if (string.Equals(item.Content?.ToString(), cleanTs, StringComparison.OrdinalIgnoreCase))
            {
                TimeSigCombo.SelectedItem = item;
                return;
            }
        }
        var customItem = new ComboBoxItem { Content = cleanTs };
        TimeSigCombo.Items.Add(customItem);
        TimeSigCombo.SelectedItem = customItem;
    }

    private string? _fetchedCoverUrl;
    private string? _fetchedAlbum;
    private TimeSpan? _fetchedDuration;

    private async void OnFetchClicked(object sender, RoutedEventArgs e)
    {
        string url = UrlTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url)) return;

        FetchProgress.Visibility = Visibility.Visible;
        FetchProgress.IsActive = true;
        FetchBtn.IsEnabled = false;
        FetchStatusText.Visibility = Visibility.Collapsed;

        try
        {
            var meta = await _metadataService.FetchMetadataFromUrlAsync(url);
            TitleBox.Text = meta.Title;
            ArtistBox.Text = meta.Artist;
            if (!string.IsNullOrWhiteSpace(meta.Album))
            {
                AlbumBox.Text = meta.Album;
            }
            BpmBox.Text = meta.Bpm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            KeyCombo.SelectedItem = meta.Key.ToDisplayString();
            ScaleCombo.SelectedIndex = meta.Scale == KeyScale.Minor ? 1 : 0;
            _fetchedCoverUrl = meta.CoverImageUrl;
            _fetchedAlbum = meta.Album;
            _fetchedDuration = meta.Duration;

            if (!string.IsNullOrEmpty(meta.TimeSignature))
            {
                SelectTimeSignatureInCombo(meta.TimeSignature);
            }

            if (meta.Sections.Count > 0)
            {
                SectionsBox.Text = string.Join(", ", meta.Sections);
            }

            FetchStatusText.Text = $"¡Datos obtenidos: {meta.Title} • {meta.Artist} • {meta.Album} • {meta.Bpm} BPM • {meta.TimeSignature} • {meta.Key.ToDisplayString()}!";
            FetchStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129));
            FetchStatusText.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            FetchStatusText.Text = $"Error: {ex.Message}";
            FetchStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));
            FetchStatusText.Visibility = Visibility.Visible;
        }
        finally
        {
            FetchProgress.IsActive = false;
            FetchProgress.Visibility = Visibility.Collapsed;
            FetchBtn.IsEnabled = true;
        }
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        TargetSong.Title = TitleBox.Text.Trim();
        TargetSong.Artist = ArtistBox.Text.Trim();
        string albumVal = AlbumBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(albumVal))
        {
            TargetSong.Album = albumVal;
        }
        else if (!string.IsNullOrEmpty(_fetchedAlbum))
        {
            TargetSong.Album = _fetchedAlbum;
        }

        if (!string.IsNullOrEmpty(_fetchedCoverUrl)) TargetSong.CoverUrl = _fetchedCoverUrl;
        if (_fetchedDuration.HasValue && _fetchedDuration.Value.TotalSeconds > 10)
        {
            TargetSong.Duration = _fetchedDuration.Value;
        }

        if (double.TryParse(BpmBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double bpm) && bpm >= 30 && bpm <= 300)
        {
            TargetSong.OriginalBpm = bpm;
            TargetSong.PlaybackBpm = bpm;
        }

        if (KeyCombo.SelectedItem is string keyStr && MusicalKeyExtensions.TryParseMusicalKey(keyStr, out var key))
        {
            TargetSong.OriginalKey = key;
        }

        TargetSong.OriginalScale = ScaleCombo.SelectedIndex == 1 ? KeyScale.Minor : KeyScale.Major;

        if (TimeSigCombo.SelectedItem is ComboBoxItem tsItem && tsItem.Content is string tsText)
        {
            var parts = tsText.Split('/');
            if (parts.Length == 2 && int.TryParse(parts[0], out int num) && int.TryParse(parts[1], out int den))
            {
                TargetSong.TimeSignatureNumerator = num;
                TargetSong.TimeSignatureDenominator = den;
            }
        }

        // Parse arrangement sections
        string rawSections = SectionsBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(rawSections))
        {
            var sectionNames = rawSections.Split(new[] { ',', ';', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (sectionNames.Count > 0)
            {
                double totalSecs = TargetSong.Duration.TotalSeconds > 10 ? TargetSong.Duration.TotalSeconds : 180.0;
                double perSecDuration = totalSecs / sectionNames.Count;

                string arrName = !string.IsNullOrWhiteSpace(TargetSong.Album) ? TargetSong.Album : "Original";
                var arr = new Arrangement(arrName);
                for (int i = 0; i < sectionNames.Count; i++)
                {
                    string name = sectionNames[i].ToUpperInvariant();
                    double start = i * perSecDuration;
                    double end = (i + 1) * perSecDuration;

                    string color = name switch
                    {
                        var n when n.Contains("INTRO") => "#3B82F6",
                        var n when n.Contains("VERS") => "#10B981",
                        var n when n.Contains("CORO") || n.Contains("CHORUS") => "#F59E0B",
                        var n when n.Contains("PUENTE") || n.Contains("BRIDGE") => "#8B5CF6",
                        var n when n.Contains("OUTRO") || n.Contains("FINAL") => "#64748B",
                        _ => "#EC4899"
                    };

                    arr.Sections.Add(new SectionMarker(name, SectionType.Custom, TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), color));
                }

                TargetSong.Arrangements.Clear();
                TargetSong.Arrangements.Add(arr);
                TargetSong.ActiveArrangementId = arr.Id;
            }
        }
    }
}
