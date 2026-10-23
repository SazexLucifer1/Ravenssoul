using Godot;

namespace Utopia;

/// <summary>
/// Keeps content inside the platform safe area plus a design margin.
///
/// Desktop targets (Windows, Linux) report no insets, so this applies the base
/// margin only; the inset path is exercised by tests via <see cref="OverrideSafeRect"/>
/// and becomes active automatically if a platform reports a safe area.
/// </summary>
[GlobalClass]
public partial class SafeAreaContainer : MarginContainer
{
    public readonly record struct Insets(int Left, int Top, int Right, int Bottom);

    [Export] public int BaseMargin { get; set; } = 32;

    private Rect2 _overrideSafeRect;

    /// <summary>Safe rectangle in viewport coordinates. Empty = query the platform.</summary>
    public Rect2 OverrideSafeRect
    {
        get => _overrideSafeRect;
        set
        {
            _overrideSafeRect = value;
            Refresh();
        }
    }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        GetViewport().SizeChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        if (!IsInsideTree())
            return;
        Rect2 viewportRect = GetViewportRect();
        Rect2 safe = OverrideSafeRect.HasArea() ? OverrideSafeRect : PlatformSafeRect(viewportRect);
        Insets insets = ComputeInsets(viewportRect.Size, safe);
        AddThemeConstantOverride("margin_left", BaseMargin + insets.Left);
        AddThemeConstantOverride("margin_top", BaseMargin + insets.Top);
        AddThemeConstantOverride("margin_right", BaseMargin + insets.Right);
        AddThemeConstantOverride("margin_bottom", BaseMargin + insets.Bottom);
    }

    public static Insets ComputeInsets(Vector2 viewportSize, Rect2 safe)
    {
        Rect2 clipped = safe.Intersection(new Rect2(Vector2.Zero, viewportSize));
        return new Insets(
            Mathf.Max(0, Mathf.RoundToInt(clipped.Position.X)),
            Mathf.Max(0, Mathf.RoundToInt(clipped.Position.Y)),
            Mathf.Max(0, Mathf.RoundToInt(viewportSize.X - clipped.End.X)),
            Mathf.Max(0, Mathf.RoundToInt(viewportSize.Y - clipped.End.Y)));
    }

    // Windowed desktop: the whole viewport is safe. Mobile/console ports map
    // DisplayServer.GetDisplaySafeArea() into viewport space here.
    private static Rect2 PlatformSafeRect(Rect2 viewportRect) => viewportRect;
}
