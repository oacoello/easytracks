namespace EasyTracks.Domain.Models;

public class StemTrack
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public StemCategory Category { get; set; } = StemCategory.Other;
    
    // Audio Mix Properties
    public float Volume { get; set; } = 1.0f; // 0.0 to 1.0 (or > 1.0 with boost)
    public float Pan { get; set; } = 0.0f;    // -1.0 (Left) to +1.0 (Right)
    public bool IsMuted { get; set; }
    public bool IsSolo { get; set; }

    // Routing: 0 = Master, 1..N = Independent Hardware Stereo/Mono Bus Outputs
    public int OutputBusIndex { get; set; } = 0;

    public StemTrack() { }

    public StemTrack(string name, string filePath, StemCategory category)
    {
        Name = name;
        FilePath = filePath;
        Category = category;
    }
}
