namespace EasyTracks.Domain.Models;

public enum StemCategory
{
    Click,
    Guide,          // Cues / Slate
    Drums,
    Percussion,
    Bass,
    AcousticGuitar,
    ElectricGuitar,
    Piano,
    Keys,
    Synth,
    Strings,
    Brass,
    LeadVocal,
    BackingVocals,
    Choir,
    SoundEffects,
    Other
}

public enum MusicalKey
{
    C,
    CSharp,
    Db = CSharp,
    D,
    DSharp,
    Eb = DSharp,
    E,
    F,
    FSharp,
    Gb = FSharp,
    G,
    GSharp,
    Ab = GSharp,
    A,
    ASharp,
    Bb = ASharp,
    B
}

public enum KeyScale
{
    Major,
    Minor
}

public enum TransitionType
{
    Immediate,      // Play next song immediately upon completion
    Crossfade,      // Crossfade audio into next song
    PadContinuous,  // Pad stays running seamlessly into next song's key
    ManualStop      // Wait for musician to press play
}

public enum SectionType
{
    CountIn,
    Intro,
    Verse,
    PreChorus,
    Chorus,
    PostChorus,
    Interlude,
    Instrumental,
    Bridge,
    Vamp,
    Tag,
    Breakdown,
    Outro,
    Ending,
    Custom
}
