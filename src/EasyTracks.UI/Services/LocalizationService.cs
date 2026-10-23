using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EasyTracks_UI.Services;

public partial class LocalizationService : ObservableObject
{
    public static LocalizationService Instance { get; } = new();

    private Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLanguage = "es";

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged(nameof(CurrentLanguage));
                RefreshAllBindings();
            }
        }
    }

    public string LocalizationFilePath { get; private set; } = "";

    public LocalizationService()
    {
        ResolveAndLoadFile();
    }

    public void ResolveAndLoadFile()
    {
        // Priority 1: Workspace root localization.json for instant live editing by user
        string workspacePath = @"c:\Users\oacoe\Documents\antigravity\vibrant-nobel\localization.json";
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EasyTracks",
            "localization.json");
        string baseDirPath = Path.Combine(AppContext.BaseDirectory, "localization.json");

        if (File.Exists(workspacePath))
        {
            LocalizationFilePath = workspacePath;
        }
        else if (File.Exists(appDataPath))
        {
            LocalizationFilePath = appDataPath;
        }
        else if (File.Exists(baseDirPath))
        {
            LocalizationFilePath = baseDirPath;
        }
        else
        {
            LocalizationFilePath = workspacePath;
        }

        Reload();
    }

    public void Reload()
    {
        try
        {
            if (File.Exists(LocalizationFilePath))
            {
                string json = File.ReadAllText(LocalizationFilePath);
                var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                if (parsed != null)
                {
                    _translations = parsed;
                }
            }
        }
        catch
        {
            // Keep existing or fallback values on malformed JSON
        }

        RefreshAllBindings();
    }

    public string Get(string key, string fallback)
    {
        if (_translations.TryGetValue(_currentLanguage, out var langDict) &&
            langDict.TryGetValue(key, out var val) &&
            !string.IsNullOrEmpty(val))
        {
            return val;
        }

        if (_translations.TryGetValue("es", out var esDict) &&
            esDict.TryGetValue(key, out var esVal) &&
            !string.IsNullOrEmpty(esVal))
        {
            return esVal;
        }

        return fallback;
    }

    private void RefreshAllBindings()
    {
        OnPropertyChanged(string.Empty);
    }

    public string GetEditableFilePath()
    {
        if (string.IsNullOrWhiteSpace(LocalizationFilePath))
        {
            ResolveAndLoadFile();
        }
        return LocalizationFilePath;
    }

    // Bindable localized properties for XAML x:Bind Mode=OneWay
    public string Nav_Live => Get("Nav_Live", "En Vivo");
    public string Nav_Setlist => Get("Nav_Setlist", "Setlists");
    public string Nav_Songs => Get("Nav_Songs", "Canciones");
    public string Nav_Settings => Get("Nav_Settings", "Ajustes");
    public string Btn_Edit => Get("Btn_Edit", "EDITAR");
    public string Btn_Stage => Get("Btn_Stage", "ESCENARIO");
    public string Btn_AddSong => Get("Btn_AddSong", "+ Agregar Canción");
    public string Btn_UnmuteAll => Get("Btn_UnmuteAll", "Activar Todo");
    public string Btn_ClearSolo => Get("Btn_ClearSolo", "Quitar Solo");
    public string Btn_Mackie16Ch => Get("Btn_Mackie16Ch", "🎛 Mackie 16-Ch");
    public string Btn_Split2Ch => Get("Btn_Split2Ch", "🎧 Split 2-Ch L/R");
    public string Btn_EditSections => Get("Btn_EditSections", "Editar Secciones");
    public string Btn_SnapBar => Get("Btn_SnapBar", "Snap Compás");
    public string Btn_Rename => Get("Btn_Rename", "Renombrar");
    public string Btn_MinusBar => Get("Btn_MinusBar", "-1 Compás");
    public string Btn_PlusBar => Get("Btn_PlusBar", "+1 Compás");
    public string Btn_Split => Get("Btn_Split", "✂ Dividir");
    public string Btn_AddMarker => Get("Btn_AddMarker", "+ Marcador");
    public string Btn_RemoveMarker => Get("Btn_RemoveMarker", "🗑 Quitar");
    public string Lbl_ArrangementHeader => Get("Lbl_ArrangementHeader", "ESTRUCTURA Y MARCADORES");
    public string Lbl_InstrumentBuses => Get("Lbl_InstrumentBuses", "BUSES Y RUTEO:");
    public string Lbl_Master => Get("Lbl_Master", "MASTER");
    public string Lbl_MainLR => Get("Lbl_MainLR", "SALIDA L/R");
    public string Lbl_AmbientPad => Get("Lbl_AmbientPad", "AMBIENT PAD");
    public string Lbl_PadKey => Get("Lbl_PadKey", "TONO PAD:");
    public string Lbl_Crossfade => Get("Lbl_Crossfade", "Fundido:");
    public string Lbl_Seconds => Get("Lbl_Seconds", "seg");
    public string Setlist_Title => Get("Setlist_Title", "Gestión de Setlists");
    public string Setlist_Subtitle => Get("Setlist_Subtitle", "Crea, organiza y cambia entre diferentes servicios o ensayos.");
    public string Setlist_SavedHeader => Get("Setlist_SavedHeader", "Tus Setlists Guardados");
    public string Setlist_NewBtn => Get("Setlist_NewBtn", "+ Nuevo Setlist");
    public string Setlist_NameLabel => Get("Setlist_NameLabel", "Nombre del Setlist Seleccionado");
    public string Setlist_DateLabel => Get("Setlist_DateLabel", "Fecha del Setlist");
    public string Setlist_SaveChangesBtn => Get("Setlist_SaveChangesBtn", "Guardar Cambios");
    public string Setlist_SongsInListHeader => Get("Setlist_SongsInListHeader", "Canciones en este Setlist (Arrastra para reordenar)");
    public string Songs_Title => Get("Songs_Title", "Biblioteca de Canciones y Secuencias");
    public string Songs_Subtitle => Get("Songs_Subtitle", "Gestiona tus secuencias multitrack, busca por título, artista, tono o versión (Arrangement).");
    public string Songs_ImportBtn => Get("Songs_ImportBtn", "+ Importar Carpeta de Secuencia (Stems)");
    public string Songs_ConfigMetaBtn => Get("Songs_ConfigMetaBtn", "Configurar Metadata y Enlace");
    public string Songs_SearchPlaceholder => Get("Songs_SearchPlaceholder", "Buscar por título, artista, álbum/arrangement o tono...");
    public string Col_Song => Get("Col_Song", "CANCIÓN");
    public string Col_Artist => Get("Col_Artist", "ARTISTA");
    public string Col_Arrangement => Get("Col_Arrangement", "ARRANGEMENT / ÁLBUM");
    public string Col_Key => Get("Col_Key", "KEY ORIGINAL");
    public string Col_Bpm => Get("Col_Bpm", "BPM");
    public string Col_TimeSig => Get("Col_TimeSig", "COMPÁS");
    public string Col_Duration => Get("Col_Duration", "DURACIÓN");
    public string Col_Actions => Get("Col_Actions", "ACCIONES");
    public string Settings_Title => Get("Settings_Title", "Configuración de Hardware, Idioma y Preferencias");
    public string Settings_HardwareTitle => Get("Settings_HardwareTitle", "🎛 Consola e Interfaz de Audio (Mackie DL16S USB / WASAPI)");
    public string Settings_HardwareSub => Get("Settings_HardwareSub", "Auto-detección Plug & Play por USB para Mackie DL16S (16 canales directos) o ruteo de 2 canales.");
    public string Settings_ScanUsbBtn => Get("Settings_ScanUsbBtn", "🔄 Escanear USB");
    public string Settings_OutputDeviceLabel => Get("Settings_OutputDeviceLabel", "Dispositivo de Salida Activo:");
    public string Settings_RoutingModeLabel => Get("Settings_RoutingModeLabel", "Modo de Ruteo de Salida:");
    public string Settings_ThemeTitle => Get("Settings_ThemeTitle", "Tema de Apariencia");
    public string Settings_ThemeSub => Get("Settings_ThemeSub", "Alterna entre modo oscuro para escenario, modo claro o sincronizado con el sistema.");
    public string Settings_ThemeDark => Get("Settings_ThemeDark", "Oscuro (Modo Escenario)");
    public string Settings_ThemeLight => Get("Settings_ThemeLight", "Claro (Modo Día)");
    public string Settings_ThemeSystem => Get("Settings_ThemeSystem", "Tema del Dispositivo (Sistema)");
    public string Settings_LangTitle => Get("Settings_LangTitle", "Idioma de la Interfaz / Language");
    public string Settings_LangSub => Get("Settings_LangSub", "Selecciona el idioma de los menús o edita el archivo localization.json para personalizar cualquier texto.");
    public string Settings_OpenLocalizationBtn => Get("Settings_OpenLocalizationBtn", "📝 Editar Archivo de Textos (localization.json)");
    public string Settings_ReloadLocalizationBtn => Get("Settings_ReloadLocalizationBtn", "🔄 Recargar Textos");
    public string Settings_FoldersTitle => Get("Settings_FoldersTitle", "Carpetas Dedicadas del Sistema");
    public string Settings_FoldersSub => Get("Settings_FoldersSub", "Coloca tus archivos en estas rutas para detección instantánea o ábrelas directamente:");
    public string Settings_OpenTracksBtn => Get("Settings_OpenTracksBtn", "📂 Abrir Carpeta de Secuencias (Tracks)");
    public string Settings_OpenPadsBtn => Get("Settings_OpenPadsBtn", "📂 Abrir Carpeta de Ambient Pads (Pads)");

    // CamelCase aliases used in XAML bindings
    public string NavLive => Nav_Live;
    public string NavSetlist => Nav_Setlist;
    public string NavSongs => Nav_Songs;
    public string NavSettings => Nav_Settings;
    public string EditButton => Btn_Edit;
    public string StageButton => Btn_Stage;
    public string PreviousSongTooltip => Get("Tooltip_PrevSong", "Canción anterior");
    public string PlayPauseTooltip => Get("Tooltip_PlayPause", "Reproducir / Pausar");
    public string StopTooltip => Get("Tooltip_Stop", "Detener");
    public string NextSongTooltip => Get("Tooltip_NextSong", "Siguiente canción");
    public string LoopSectionTooltip => Get("Tooltip_LoopSection", "Repetir sección en bucle");
    public string FadeOutTooltip => Get("Tooltip_FadeOut", "Fade Out (Desvanecer audio)");
    public string ArrangementHeader => Lbl_ArrangementHeader;
    public string SnapBarButton => Btn_SnapBar;
    public string EditSectionsButton => Btn_EditSections;
    public string RenameSectionButton => Btn_Rename;
    public string SplitSectionButton => Btn_Split;
    public string AddMarkerButton => Btn_AddMarker;
    public string RemoveMarkerButton => Btn_RemoveMarker;
    public string BusesAndRoutingLabel => Lbl_InstrumentBuses;
    public string Mackie16ChButton => Btn_Mackie16Ch;
    public string Split2ChButton => Btn_Split2Ch;
    public string UnmuteAllButton => Btn_UnmuteAll;
    public string ClearSoloButton => Btn_ClearSolo;
    public string MasterLabel => Lbl_Master;
    public string AmbientPadButton => Lbl_AmbientPad;
    public string PadKeyLabel => Lbl_PadKey;
    public string CrossfadeLabel => Lbl_Crossfade;
    public string SetlistTitle => Setlist_Title;
    public string SetlistSubtitle => Setlist_Subtitle;
    public string SavedSetlistsHeader => Setlist_SavedHeader;
    public string NewSetlistButton => Setlist_NewBtn;
    public string SelectedSetlistNameLabel => Setlist_NameLabel;
    public string SetlistDateLabel => Setlist_DateLabel;
    public string SaveChangesButton => Setlist_SaveChangesBtn;
    public string SongsInSetlistHeader => Setlist_SongsInListHeader;
    public string SongsLibraryTitle => Songs_Title;
    public string SongsLibrarySubtitle => Songs_Subtitle;
    public string SearchSongsPlaceholder => Songs_SearchPlaceholder;
    public string ImportFolderButton => Songs_ImportBtn;
    public string ConfigureMetadataButton => Songs_ConfigMetaBtn;
    public string ColTitle => Col_Song;
    public string ColArtist => Col_Artist;
    public string ColArrangement => Col_Arrangement;
    public string ColKey => Col_Key;
    public string ColBpm => Col_Bpm;
    public string ColTimeSig => Col_TimeSig;
    public string ColDuration => Col_Duration;
    public string ColActions => Col_Actions;
    public string SettingsTitle => Settings_Title;
    public string HardwareSectionTitle => Settings_HardwareTitle;
    public string HardwareSectionDesc => Settings_HardwareSub;
    public string ScanUsbButton => Settings_ScanUsbBtn;
    public string ActiveOutputDeviceLabel => Settings_OutputDeviceLabel;
    public string OutputRoutingModeLabel => Settings_RoutingModeLabel;
    public string LanguageSectionTitle => Settings_LangTitle;
    public string LanguageSectionDesc => Settings_LangSub;
    public string OpenLocalizationFileButton => Settings_OpenLocalizationBtn;
    public string ReloadLocalizationButton => Settings_ReloadLocalizationBtn;
    public string ThemeSectionTitle => Settings_ThemeTitle;
    public string ThemeSectionDesc => Settings_ThemeSub;
    public string ThemeDarkOption => Settings_ThemeDark;
    public string ThemeLightOption => Settings_ThemeLight;
    public string ThemeSystemOption => Settings_ThemeSystem;
    public string FoldersSectionTitle => Settings_FoldersTitle;
    public string FoldersSectionDesc => Settings_FoldersSub;
    public string OpenTracksFolderButton => Settings_OpenTracksBtn;
    public string OpenPadsFolderButton => Settings_OpenPadsBtn;
}
