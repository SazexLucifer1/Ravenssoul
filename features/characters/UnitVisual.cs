using Godot;

namespace Utopia;

/// <summary>
/// Placeholder unit art drawn in code until sprites exist. Swap this node for
/// a Sprite2D/AnimatedSprite2D scene without touching gameplay scripts.
/// </summary>
[GlobalClass]
public partial class UnitVisual : Node2D
{
    [Export] public Color BodyColor { get; set; } = new("3d6f9e");
    [Export] public Color TrimColor { get; set; } = new("ece6d6");
    [Export] public float Radius { get; set; } = 6.0f;

    private int _facing = 1;

    /// <summary>1 = facing right, -1 = facing left. Mirrors the banner.</summary>
    public int Facing
    {
        get => _facing;
        set
        {
            _facing = value >= 0 ? 1 : -1;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        // Contact shadow grounds the unit on the tile (art guide: grounding).
        DrawSetTransform(new Vector2(0, Radius * 0.9f), 0.0f, new Vector2(1.0f, 0.5f));
        DrawCircle(Vector2.Zero, Radius, new Color(0, 0, 0, 0.35f));
        DrawSetTransform(new Vector2(0, -Radius * 0.6f), 0.0f, Vector2.One);
        DrawCircle(Vector2.Zero, Radius, BodyColor);
        DrawArc(Vector2.Zero, Radius, 0.0f, Mathf.Tau, 24, TrimColor, 1.0f);
        var pole = new Vector2(Radius * 0.5f * Facing, -Radius);
        DrawLine(pole, pole + new Vector2(0, -Radius), TrimColor, 1.0f);
        DrawColoredPolygon(new[]
        {
            pole + new Vector2(0, -Radius),
            pole + new Vector2(Radius * 0.8f * Facing, -Radius * 0.75f),
            pole + new Vector2(0, -Radius * 0.5f),
        }, BodyColor.Lightened(0.2f));
    }
}
