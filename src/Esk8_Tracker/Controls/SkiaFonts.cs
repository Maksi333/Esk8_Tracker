using SkiaSharp;

namespace Esk8_Tracker.Controls;

/// <summary>
/// Lazy, synchronous loaders for the app's bundled typefaces so Skia-drawn
/// controls can render text with the same fonts as the rest of the UI.
/// MAUI fonts are bundled as Android assets at the package root, so they are
/// read via <see cref="FileSystem.OpenAppPackageFileAsync(string)"/>.
/// </summary>
public static class SkiaFonts
{
    private static SKTypeface? _chakraPetch;
    private static SKTypeface? _chakraPetchSemiBold;

    /// <summary>ChakraPetch-Regular, or <see cref="SKTypeface.Default"/> if loading fails.</summary>
    public static SKTypeface ChakraPetch =>
        _chakraPetch ??= Load("ChakraPetch-Regular.ttf");

    /// <summary>ChakraPetch-SemiBold, or <see cref="SKTypeface.Default"/> if loading fails.</summary>
    public static SKTypeface ChakraPetchSemiBold =>
        _chakraPetchSemiBold ??= Load("ChakraPetch-SemiBold.ttf");

    private static SKTypeface Load(string fileName)
    {
        try
        {
            var stream = FileSystem.OpenAppPackageFileAsync(fileName).GetAwaiter().GetResult();

            // SKTypeface.FromStream takes ownership of the SKManagedStream (and,
            // via disposeManagedStream: true, of the underlying package stream),
            // so neither is disposed here.
            var managed = new SKManagedStream(stream, disposeManagedStream: true);
            var typeface = SKTypeface.FromStream(managed);
            return typeface ?? SKTypeface.Default;
        }
        catch
        {
            return SKTypeface.Default;
        }
    }
}
