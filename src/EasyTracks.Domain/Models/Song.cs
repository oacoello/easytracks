namespace EasyTracks.Domain.Models;

public class Song
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public double OriginalBpm { get; set; } = 120.0;
    public double PlaybackBpm { get; set; } = 120.0;
    public MusicalKey OriginalKey { get; set; } = MusicalKey.C;
    public KeyScale OriginalScale { get; set; } = KeyScale.Major;
    public int KeySemitoneOffset { get; set; } = 0; // -12 to +12 semitones
    public int TimeSignatureNumerator { get; set; } = 4;
    public int TimeSignatureDenominator { get; set; } = 4;
    public TimeSpan Duration { get; set; }

    public List<StemTrack> Stems { get; set; } = new();
    public List<Arrangement> Arrangements { get; set; } = new();
    public Guid ActiveArrangementId { get; set; }

    public Arrangement? ActiveArrangement => Arrangements.Find(a => a.Id == ActiveArrangementId) ?? Arrangements.FirstOrDefault();

    public Song() { }

    public Song(string title, double bpm, MusicalKey key, KeyScale scale = KeyScale.Major)
    {
        Title = title;
        OriginalBpm = bpm;
        PlaybackBpm = bpm;
        OriginalKey = key;
        OriginalScale = scale;
        
        var defaultArrangement = new Arrangement("Original");
        Arrangements.Add(defaultArrangement);
        ActiveArrangementId = defaultArrangement.Id;
    }
}
