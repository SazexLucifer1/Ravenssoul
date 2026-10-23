using System.Collections.Generic;
using Godot;

namespace Utopia.Tests.Tools;

/// <summary>One review mark drawn by AnnotationOverlay.</summary>
public sealed record AnnotationMark(string Type, Vector2 At, float Radius, Rect2 Rect, Vector2 To, Color Color, string Label, Vector2? LabelAt)
{
    public static AnnotationMark Circle(Vector2 at, float radius, Color color, string label, Vector2 labelAt) =>
        new("circle", at, radius, default, default, color, label, labelAt);

    public static AnnotationMark Box(Rect2 rect, Color color, string label, Vector2 labelAt) =>
        new("rect", default, 0, rect, default, color, label, labelAt);

    public static AnnotationMark Arrow(Vector2 from, Vector2 to, Color color) =>
        new("arrow", from, 0, default, to, color, "", null);
}

/// <summary>Draws AnnotationMarks over a captured frame (see AnnotateFrames).</summary>
public partial class AnnotationOverlay : Control
{
    public List<AnnotationMark> Marks { get; set; } = new();

    public override void _Draw()
    {
        Font font = ThemeDB.FallbackFont;
        foreach (AnnotationMark mark in Marks)
        {
            Vector2 labelAt = Vector2.Zero;
            switch (mark.Type)
            {
                case "circle":
                    DrawArc(mark.At, mark.Radius, 0, Mathf.Tau, 48, mark.Color, 3.0f);
                    labelAt = mark.At + new Vector2(mark.Radius + 6, -4);
                    break;
                case "rect":
                    DrawRect(mark.Rect, mark.Color, false, 3.0f);
                    labelAt = new Vector2(mark.Rect.Position.X, mark.Rect.End.Y + 18);
                    break;
                case "arrow":
                    DrawDashedLine(mark.At, mark.To, mark.Color, 3.0f, 10.0f);
                    Vector2 d = (mark.To - mark.At).Normalized();
                    DrawColoredPolygon(new[] { mark.To, mark.To - d * 14 + d.Orthogonal() * 7, mark.To - d * 14 - d.Orthogonal() * 7 }, mark.Color);
                    break;
            }
            if (mark.LabelAt is Vector2 explicitAt)
                labelAt = explicitAt;
            if (string.IsNullOrEmpty(mark.Label))
                continue;
            float width = font.GetStringSize(mark.Label, HorizontalAlignment.Left, -1, 16).X;
            DrawRect(new Rect2(labelAt + new Vector2(-4, -16), new Vector2(width + 8, 22)), new Color(0, 0, 0, 0.75f));
            DrawString(font, labelAt, mark.Label, HorizontalAlignment.Left, -1, 16, mark.Color);
        }
    }
}
