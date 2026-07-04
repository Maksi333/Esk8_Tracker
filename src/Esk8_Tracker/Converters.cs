using System.Globalization;

namespace Esk8_Tracker;

/// <summary>bool → one of two resource colors (selected/unselected segmented control, chips).</summary>
public abstract class BoolColorConverter : IValueConverter
{
    protected abstract string TrueKey { get; }
    protected abstract string FalseKey { get; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        App.Res(value is true ? TrueKey : FalseKey);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SegBgConverter : BoolColorConverter
{
    protected override string TrueKey => "SurfaceElevated";
    protected override string FalseKey => "SurfaceCard";
}

public sealed class SegBorderConverter : BoolColorConverter
{
    protected override string TrueKey => "BorderStrong";
    protected override string FalseKey => "BorderDefault";
}

public sealed class SegTextConverter : BoolColorConverter
{
    protected override string TrueKey => "TextPrimary";
    protected override string FalseKey => "TextTertiary";
}

/// <summary>Accent-colored when selected, tertiary otherwise (auto-pause / units segments).</summary>
public sealed class AccentTextConverter : BoolColorConverter
{
    protected override string TrueKey => "Accent";
    protected override string FalseKey => "TextTertiary";
}

public sealed class AutoBorderConverter : BoolColorConverter
{
    protected override string TrueKey => "Accent";
    protected override string FalseKey => "BorderDefault";
}

/// <summary>Units segment fill: accent when selected, transparent otherwise.</summary>
public sealed class UnitSelBgConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? App.Res("Accent") : Microsoft.Maui.Graphics.Colors.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class UnitSelTextConverter : BoolColorConverter
{
    protected override string TrueKey => "OnAccent";
    protected override string FalseKey => "TextTertiary";
}
