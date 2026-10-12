namespace EasyTracks.Domain.Models;

public class SetlistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SongId { get; set; }
    public Song? Song { get; set; }
    
    // Per-setlist overrides
    public double? TargetBpm { get; set; }
    public int? TargetSemitoneOffset { get; set; }
    public Guid? SelectedArrangementId { get; set; }
    public TransitionType Transition { get; set; } = TransitionType.ManualStop;
    public TimeSpan CrossfadeDuration { get; set; } = TimeSpan.FromSeconds(3);

    public SetlistItem() { }

    public SetlistItem(Song song, TransitionType transition = TransitionType.ManualStop)
    {
        SongId = song.Id;
        Song = song;
        Transition = transition;
    }
}

public class Setlist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Setlist";
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public List<SetlistItem> Items { get; set; } = new();

    public Setlist() { }

    public Setlist(string name)
    {
        Name = name;
    }
}
