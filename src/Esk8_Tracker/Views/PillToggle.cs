namespace Esk8_Tracker.Views;

/// <summary>46×27 pill switch: knob slides, track turns green when on (design spec).</summary>
public class PillToggle : ContentView
{
    private readonly Border _track;
    private readonly BoxView _knob;

    public static readonly BindableProperty IsOnProperty = BindableProperty.Create(
        nameof(IsOn), typeof(bool), typeof(PillToggle), false,
        BindingMode.TwoWay, propertyChanged: OnIsOnChanged);

    public static readonly BindableProperty DescriptionProperty = BindableProperty.Create(
        nameof(Description), typeof(string), typeof(PillToggle), "");

    public event EventHandler<bool>? Toggled;

    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public PillToggle()
    {
        _knob = new BoxView
        {
            WidthRequest = 21,
            HeightRequest = 21,
            CornerRadius = 11,
            Color = Colors.White,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center,
            TranslationX = 3,
        };
        _track = new Border
        {
            WidthRequest = 46,
            HeightRequest = 27,
            StrokeThickness = 0,
            BackgroundColor = (Color)App.Res("BorderDefault"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 100 },
            Padding = 0,
            Content = _knob,
        };
        Content = _track;
        WidthRequest = 46;
        HeightRequest = 27;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => { IsOn = !IsOn; Toggled?.Invoke(this, IsOn); };
        GestureRecognizers.Add(tap);
        UpdateVisual(animate: false);
    }

    private static void OnIsOnChanged(BindableObject bindable, object oldValue, object newValue)
        => ((PillToggle)bindable).UpdateVisual(animate: true);

    private void UpdateVisual(bool animate)
    {
        SemanticProperties.SetDescription(this, $"{Description} {(IsOn ? "on" : "off")}");
        var targetX = IsOn ? 22.0 : 3.0;
        var targetColor = IsOn ? (Color)App.Res("Go") : (Color)App.Res("BorderDefault");
        if (animate)
        {
            _ = _knob.TranslateTo(targetX, 0, 200, Easing.CubicInOut);
            _track.BackgroundColor = targetColor;
        }
        else
        {
            _knob.TranslationX = targetX;
            _track.BackgroundColor = targetColor;
        }
    }
}
