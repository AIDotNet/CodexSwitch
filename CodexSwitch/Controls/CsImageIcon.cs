using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace CodexSwitch.Controls;

public sealed class CsImageIcon : Image
{
    public static readonly StyledProperty<string> PathProperty =
        AvaloniaProperty.Register<CsImageIcon, string>(nameof(Path), "");

    public string Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PathProperty)
            Source = TryLoad(Path);
    }

    private static IImage? TryLoad(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }
}
