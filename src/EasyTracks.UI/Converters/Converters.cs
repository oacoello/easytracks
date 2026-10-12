using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace EasyTracks_UI.Converters;

public class MuteColorConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveRed = new(Windows.UI.Color.FromArgb(255, 239, 68, 68));   // #EF4444
    private static readonly SolidColorBrush InactiveBg = new(Windows.UI.Color.FromArgb(255, 39, 39, 42));    // #27272A

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is true) ? ActiveRed : InactiveBg;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class SoloColorConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveGold = new(Windows.UI.Color.FromArgb(255, 234, 179, 8));   // #EAB308
    private static readonly SolidColorBrush InactiveBg = new(Windows.UI.Color.FromArgb(255, 39, 39, 42));    // #27272A

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is true) ? ActiveGold : InactiveBg;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class PlayPauseIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Segoe Fluent Icons: "\uE768" = Play, "\uE769" = Pause
        return (value is true) ? "\uE769" : "\uE768";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class PlayPauseColorConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveGreen = new(Windows.UI.Color.FromArgb(255, 16, 185, 129)); // #10B981
    private static readonly SolidColorBrush AmberPause = new(Windows.UI.Color.FromArgb(255, 245, 158, 11));  // #F59E0B

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is true) ? AmberPause : ActiveGreen;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class PadActiveColorConverter : IValueConverter
{
    private static readonly SolidColorBrush ActivePurple = new(Windows.UI.Color.FromArgb(255, 168, 85, 247)); // #A855F7
    private static readonly SolidColorBrush InactiveBg = new(Windows.UI.Color.FromArgb(255, 39, 39, 42));     // #27272A

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is true) ? ActivePurple : InactiveBg;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

