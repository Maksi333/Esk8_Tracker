namespace Esk8_Tracker.Views;

/// <summary>
/// The app chrome: hosts the five sections, the custom bottom nav, and the shared
/// overlay surfaces (bottom sheets, styled confirm dialogs, pill toasts) that the
/// design places above everything including the nav bar.
/// </summary>
public partial class RootPage : ContentPage
{
    private record NavItem(AppTab Tab, string Icon, string Label);

    private static readonly NavItem[] NavItems =
    {
        new(AppTab.Ride, MaterialIcons.Skateboarding, "Ride"),
        new(AppTab.History, MaterialIcons.History, "History"),
        new(AppTab.Garage, MaterialIcons.Garage, "Garage"),
        new(AppTab.Stats, MaterialIcons.Leaderboard, "Stats"),
        new(AppTab.Settings, MaterialIcons.Tune, "Settings"),
    };

    public static RootPage? Current { get; private set; }

    private readonly IServiceProvider _services;
    private readonly Dictionary<AppTab, View> _sections = new();
    private readonly List<(Border Pill, Label Icon, Label Text)> _navViews = new();
    private AppTab _activeTab = AppTab.Ride;
    private TaskCompletionSource<bool>? _confirmTcs;
    private CancellationTokenSource? _toastCts;

    public AppTab ActiveTab => _activeTab;

    public RootPage(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        Current = this;
        BuildNavBar();
        SelectTab(AppTab.Ride);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        (GetSection(_activeTab) as ISection)?.OnShown();
    }

    protected override bool OnBackButtonPressed()
    {
        // Back closes overlays first, then returns to the Ride tab, then minimizes.
        // A minimized app never stops tracking — the foreground service owns the ride —
        // so backing out mid-ride is safe by design.
        if (OverlayHost.IsVisible)
        {
            CloseSheet();
            _confirmTcs?.TrySetResult(false);
            return true;
        }
        if (NavBar.IsVisible && _activeTab != AppTab.Ride)
        {
            SelectTab(AppTab.Ride);
            return true;
        }
        return base.OnBackButtonPressed();
    }

    // ---- tabs ----

    private void BuildNavBar()
    {
        for (var i = 0; i < NavItems.Length; i++)
        {
            var item = NavItems[i];

            var icon = new Label
            {
                Text = item.Icon,
                FontSize = 23,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalOptions = LayoutOptions.Center,
            };
            var pill = new Border
            {
                StrokeThickness = 0,
                BackgroundColor = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 100 },
                Padding = new Thickness(16, 2),
                HorizontalOptions = LayoutOptions.Center,
                Content = icon,
            };
            var text = new Label
            {
                FontSize = 10.5,
                Text = item.Label,
                HorizontalOptions = LayoutOptions.Center,
            };
            var stack = new VerticalStackLayout
            {
                Spacing = 2,
                Padding = new Thickness(0, 6, 0, 4),
                Children = { pill, text },
            };
            var tab = item.Tab;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => SelectTab(tab);
            stack.GestureRecognizers.Add(tap);
            AutomationProperties.SetName(stack, item.Label);

            Grid.SetColumn(stack, i);
            NavBar.Children.Add(stack);
            _navViews.Add((pill, icon, text));
        }
    }

    public void SelectTab(AppTab tab)
    {
        _activeTab = tab;
        var view = GetSection(tab);
        foreach (var child in SectionHost.Children.OfType<View>())
            child.IsVisible = false;
        if (!SectionHost.Children.Contains(view))
            SectionHost.Children.Add(view);
        view.IsVisible = true;

        for (var i = 0; i < NavItems.Length; i++)
        {
            var active = NavItems[i].Tab == tab;
            var (pill, icon, text) = _navViews[i];
            pill.BackgroundColor = active ? (Color)App.Res("AccentFaint") : Colors.Transparent;
            icon.FontFamily = active ? "MaterialRoundedFilled" : "MaterialRounded";
            icon.TextColor = active ? (Color)App.Res("Accent") : (Color)App.Res("TextTertiary");
            text.TextColor = icon.TextColor;
            text.FontFamily = active ? "InterSemiBold" : "InterMedium";
        }

        (view as ISection)?.OnShown();
    }

    private View GetSection(AppTab tab)
    {
        if (_sections.TryGetValue(tab, out var v)) return v;
        View view = tab switch
        {
            AppTab.Ride => _services.GetRequiredService<RideSection>(),
            AppTab.History => _services.GetRequiredService<HistorySection>(),
            AppTab.Garage => _services.GetRequiredService<GarageSection>(),
            AppTab.Stats => _services.GetRequiredService<StatsSection>(),
            AppTab.Settings => _services.GetRequiredService<SettingsSection>(),
            _ => throw new ArgumentOutOfRangeException(nameof(tab)),
        };
        _sections[tab] = view;
        return view;
    }

    /// <summary>Hide the bottom nav while a ride is active/paused/summary (design rule).</summary>
    public void SetNavVisible(bool visible) => NavBar.IsVisible = visible;

    // ---- toast ----

    public async Task ShowToastAsync(string message, string? icon = null)
    {
        _toastCts?.Cancel();
        var cts = _toastCts = new CancellationTokenSource();

        ToastIcon.Text = icon ?? MaterialIcons.CheckCircle;
        ToastText.Text = message;
        ToastHost.IsVisible = true;
        ToastBorder.Opacity = 0;
        ToastBorder.TranslationY = 14;
        _ = ToastBorder.FadeTo(1, 250, Easing.CubicOut);
        await ToastBorder.TranslateTo(0, 0, 250, Easing.CubicOut);

        try
        {
            await Task.Delay(2200, cts.Token);
            await ToastBorder.FadeTo(0, 200, Easing.CubicIn);
            ToastHost.IsVisible = false;
        }
        catch (TaskCanceledException)
        {
            // superseded by a newer toast
        }
    }

    // ---- styled confirm dialog (e.g. "Discard this ride?") ----

    public Task<bool> ShowConfirmAsync(string icon, string title, string body,
        string confirmText, string cancelText, bool destructive = true)
    {
        _confirmTcs?.TrySetResult(false);
        var tcs = _confirmTcs = new TaskCompletionSource<bool>();

        var confirmBtn = new Button
        {
            Text = confirmText,
            BackgroundColor = destructive ? (Color)App.Res("Danger") : (Color)App.Res("Accent"),
            TextColor = destructive ? Colors.White : (Color)App.Res("OnAccent"),
            FontFamily = "InterSemiBold",
            FontSize = 14,
            CornerRadius = 12,
            HeightRequest = 48,
        };
        var cancelBtn = new Button
        {
            Text = cancelText,
            BackgroundColor = (Color)App.Res("SurfaceElevated"),
            TextColor = (Color)App.Res("TextPrimary"),
            BorderColor = (Color)App.Res("BorderDefault"),
            BorderWidth = 1,
            FontFamily = "InterSemiBold",
            FontSize = 14,
            CornerRadius = 12,
            HeightRequest = 48,
        };
        var buttons = new Grid { ColumnSpacing = 10, ColumnDefinitions = { new(), new() } };
        buttons.Add(cancelBtn, 0);
        buttons.Add(confirmBtn, 1);

        var card = new Border
        {
            BackgroundColor = (Color)App.Res("SurfaceCard"),
            Stroke = (Color)App.Res("BorderDefault"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
            Padding = 24,
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    new Label
                    {
                        Text = icon, FontFamily = "MaterialRounded", FontSize = 42,
                        TextColor = destructive ? (Color)App.Res("Danger") : (Color)App.Res("Accent"),
                        HorizontalOptions = LayoutOptions.Center,
                    },
                    new Label
                    {
                        Text = title, FontFamily = "SpaceGroteskSemiBold", FontSize = 18,
                        TextColor = (Color)App.Res("TextPrimary"),
                        HorizontalTextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 12, 0, 6),
                    },
                    new Label
                    {
                        Text = body, FontFamily = "Inter", FontSize = 13,
                        TextColor = (Color)App.Res("TextTertiary"),
                        LineHeight = 1.5,
                        HorizontalTextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 20),
                    },
                    buttons,
                },
            },
        };

        var scrim = BuildScrim(0.75);
        var wrapper = new Grid { Padding = 28 };
        wrapper.Children.Add(card);
        card.VerticalOptions = LayoutOptions.Center;

        void Close(bool result)
        {
            OverlayHost.Children.Clear();
            OverlayHost.IsVisible = false;
            tcs.TrySetResult(result);
        }

        confirmBtn.Clicked += (_, _) => Close(true);
        cancelBtn.Clicked += (_, _) => Close(false);

        OverlayHost.Children.Clear();
        OverlayHost.Children.Add(scrim);
        OverlayHost.Children.Add(wrapper);
        OverlayHost.IsVisible = true;
        card.Scale = 0.9;
        card.Opacity = 0;
        _ = card.FadeTo(1, 200, Easing.CubicOut);
        _ = card.ScaleTo(1, 220, Easing.CubicOut);

        return tcs.Task;
    }

    // ---- bottom sheet ----

    public void ShowSheet(View content, Action? onDismiss = null)
    {
        var handle = new BoxView
        {
            WidthRequest = 38, HeightRequest = 4, CornerRadius = 2,
            Color = (Color)App.Res("BorderDefault"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 16),
        };
        var sheet = new Border
        {
            BackgroundColor = (Color)App.Res("SurfaceCard"),
            Stroke = (Color)App.Res("BorderDefault"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(22, 22, 0, 0),
            },
            Padding = new Thickness(18, 18, 18, 26),
            VerticalOptions = LayoutOptions.End,
            Content = new VerticalStackLayout { Children = { handle, content } },
        };

        var scrim = BuildScrim(0.7);
        var scrimTap = new TapGestureRecognizer();
        scrimTap.Tapped += (_, _) => { CloseSheet(); onDismiss?.Invoke(); };
        scrim.GestureRecognizers.Add(scrimTap);

        OverlayHost.Children.Clear();
        OverlayHost.Children.Add(scrim);
        OverlayHost.Children.Add(sheet);
        OverlayHost.IsVisible = true;
        sheet.TranslationY = 400;
        _ = sheet.TranslateTo(0, 0, 300, Easing.CubicOut);
    }

    public void CloseSheet()
    {
        OverlayHost.Children.Clear();
        OverlayHost.IsVisible = false;
    }

    private static BoxView BuildScrim(double opacity) => new()
    {
        Color = Color.FromRgba(3, 5, 7, (int)(opacity * 255)),
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
    };
}
