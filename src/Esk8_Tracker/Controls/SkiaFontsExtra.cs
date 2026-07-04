using SkiaSharp;

namespace Esk8_Tracker.Controls;

/// <summary>Extra bundled typefaces for Skia-drawn controls (see <see cref="SkiaFonts"/>).</summary>
public static class SkiaFontsExtra
{
    private static SKTypeface? _spaceGroteskSemiBold;
    private static SKTypeface? _material;

    public static SKTypeface SpaceGroteskSemiBold =>
        _spaceGroteskSemiBold ??= Load("SpaceGrotesk-SemiBold.ttf");

    public static SKTypeface MaterialRounded =>
        _material ??= Load("MaterialSymbolsRounded-Regular.ttf");

    private static SKTypeface Load(string fileName)
    {
        try
        {
            var stream = FileSystem.OpenAppPackageFileAsync(fileName).GetAwaiter().GetResult();
            var managed = new SKManagedStream(stream, disposeManagedStream: true);
            return SKTypeface.FromStream(managed) ?? SKTypeface.Default;
        }
        catch
        {
            return SKTypeface.Default;
        }
    }
}
