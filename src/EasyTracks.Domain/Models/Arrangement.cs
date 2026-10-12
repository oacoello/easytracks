namespace EasyTracks.Domain.Models;

public class SectionMarker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public SectionType Type { get; set; } = SectionType.Custom;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int StartBar { get; set; }
    public int EndBar { get; set; }
    public string ColorHex { get; set; } = "#3B82F6"; // Default blue
    public bool IsLoopEnabled { get; set; }

    public SectionMarker() { }

    public SectionMarker(string name, SectionType type, TimeSpan startTime, TimeSpan endTime, string colorHex = "#3B82F6")
    {
        Name = name;
        Type = type;
        StartTime = startTime;
        EndTime = endTime;
        ColorHex = colorHex;
    }
}

public class Arrangement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Default Arrangement";
    public List<SectionMarker> Sections { get; set; } = new();

    public Arrangement() { }

    public Arrangement(string name)
    {
        Name = name;
    }
}
