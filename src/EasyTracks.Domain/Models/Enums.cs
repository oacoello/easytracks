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
    ManualStop,     // Se detiene al terminar la canción
    Wait,           // Pasa a la siguiente canción en pausa
    AutoPlay,       // Pasa a la siguiente canción y reproduce inmediatamente
    Crossfade,      // Fundido cruzado / solapamiento
    Hold,           // Pasa a la siguiente en pausa, sosteniendo Pad y Click en la nueva tonalidad
    Immediate = AutoPlay,
    PadContinuous = Hold
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

public enum AudioRoutingMode
{
    Stereo2Ch,          // 2 Canales (Estéreo Normal o Split L: Click/Cues, R: Tracks)
    Multichannel16Ch    // 16 Canales USB Directos (Mackie DL16S / Interfaz Multicanal)
}

