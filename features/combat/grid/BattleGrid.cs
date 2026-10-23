using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Logical tactical grid plus its placeholder rendering, in isometric
/// projection (ADR 0010).
///
/// Owns cell&lt;-&gt;world conversion and occupancy. This is the gameplay
/// "collision" layer for tactics: movement legality is decided here from
/// BattleMapData, not by physics bodies (see docs/COLLISION_AND_HIT_DETECTION.md).
/// All world sizes are in *art pixels* (the 640x360 pixel-art space); the
/// gameplay camera zooms by an integer factor (see docs/ART_IMPLEMENTATION_GUIDE.md).
/// </summary>
[GlobalClass]
public partial class BattleGrid : Node2D
{
    [Signal] public delegate void OccupancyChangedEventHandler(Vector2I cell);

    /// <summary>Isometric tile footprint in art pixels (2:1 diamond).</summary>
    public static readonly Vector2I TileSize = new(32, 16);
    /// <summary>Height of one elevation step (blocked cells are drawn as one-step blocks).</summary>
    public const int BlockHeight = 8;

    // Placeholder palette (warm with darker undertones). Replaced by tile art.
    private static readonly Color ColorFloor = new("5b5a3a");
    private static readonly Color ColorFloorAlt = new("545335");
    private static readonly Color ColorEdge = new("3f3d27");
    private static readonly Color ColorBlockTop = new("6f6253");
    private static readonly Color ColorBlockLeft = new("4a4036");
    private static readonly Color ColorBlockRight = new("3a3129");
    private static readonly Color ColorHazard = new("6a2a1c");
    private static readonly Color ColorHazardLine = new("d8742f");
    private static readonly Color ColorObjective = new("f2c14e");
    private static readonly Color ColorMoveFill = new(0.36f, 0.62f, 0.95f, 0.35f);
    private static readonly Color ColorMoveEdge = new("8cc4ff");

    private BattleMapData _mapData;
    private readonly Dictionary<Vector2I, Node> _occupants = new();

    [Export]
    public BattleMapData MapData
    {
        get => _mapData;
        set
        {
            _mapData = value;
            _occupants.Clear();
            MoveHighlights = new List<Vector2I>();
            QueueRedraw();
        }
    }

    /// <summary>Cells the active unit can move to (drawn as blue diamonds).</summary>
    public List<Vector2I> MoveHighlights { get; private set; } = new();

    /// <summary>Center of a cell's diamond in grid-local art pixels.</summary>
    public Vector2 CellToLocal(Vector2I cell) =>
        new((cell.X - cell.Y) * TileSize.X * 0.5f, (cell.X + cell.Y) * TileSize.Y * 0.5f);

    /// <summary>Inverse projection (pointer picking). Exact at cell centers.</summary>
    public Vector2I LocalToCell(Vector2 localPosition)
    {
        float a = localPosition.X / (TileSize.X * 0.5f);
        float b = localPosition.Y / (TileSize.Y * 0.5f);
        return new Vector2I(RoundHalfAway((a + b) * 0.5f), RoundHalfAway((b - a) * 0.5f));
    }

    /// <summary>Bounding box of all tiles (including block height) in grid-local art pixels.</summary>
    public Rect2 WorldBounds()
    {
        if (MapData == null)
            return new Rect2();
        Vector2 half = new Vector2(TileSize.X, TileSize.Y) * 0.5f;
        float left = CellToLocal(new Vector2I(0, MapData.Size.Y - 1)).X - half.X;
        float right = CellToLocal(new Vector2I(MapData.Size.X - 1, 0)).X + half.X;
        float top = CellToLocal(Vector2I.Zero).Y - half.Y - BlockHeight;
        float bottom = CellToLocal(MapData.Size - Vector2I.One).Y + half.Y;
        return new Rect2(left, top, right - left, bottom - top);
    }

    public Node OccupantAt(Vector2I cell) => _occupants.TryGetValue(cell, out Node occupant) ? occupant : null;

    public bool CanEnter(Vector2I cell) => MapData != null && MapData.IsWalkable(cell) && !_occupants.ContainsKey(cell);

    /// <summary>Registers <paramref name="occupant"/> at <paramref name="cell"/>. Returns false if not enterable.</summary>
    public bool Place(Node occupant, Vector2I cell)
    {
        if (!CanEnter(cell))
            return false;
        _occupants[cell] = occupant;
        EmitSignalOccupancyChanged(cell);
        return true;
    }

    /// <summary>
    /// Moves an occupant one or more cells. The destination is validated; the
    /// caller is responsible for path legality (adjacent steps only).
    /// </summary>
    public bool Relocate(Vector2I fromCell, Vector2I toCell)
    {
        if (!_occupants.ContainsKey(fromCell) || !CanEnter(toCell))
            return false;
        _occupants[toCell] = _occupants[fromCell];
        _occupants.Remove(fromCell);
        EmitSignalOccupancyChanged(fromCell);
        EmitSignalOccupancyChanged(toCell);
        return true;
    }

    public void Remove(Vector2I cell)
    {
        if (_occupants.Remove(cell))
            EmitSignalOccupancyChanged(cell);
    }

    public void SetMoveHighlights(List<Vector2I> cells)
    {
        MoveHighlights = cells;
        QueueRedraw();
    }

    private Vector2[] Diamond(Vector2I cell, float lift = 0.0f, float inset = 0.0f)
    {
        Vector2 c = CellToLocal(cell) - new Vector2(0, lift);
        float hx = TileSize.X * 0.5f - inset * 2.0f;
        float hy = TileSize.Y * 0.5f - inset;
        return new[] { c + new Vector2(0, -hy), c + new Vector2(hx, 0), c + new Vector2(0, hy), c + new Vector2(-hx, 0) };
    }

    public override void _Draw()
    {
        if (MapData == null)
            return;
        // Back-to-front (painter's order) so raised blocks overlap correctly.
        for (int depth = 0; depth < MapData.Size.X + MapData.Size.Y - 1; depth++)
        {
            for (int x = 0; x < MapData.Size.X; x++)
            {
                var cell = new Vector2I(x, depth - x);
                if (cell.Y < 0 || cell.Y >= MapData.Size.Y)
                    continue;
                DrawCell(cell);
            }
        }
    }

    private void DrawCell(Vector2I cell)
    {
        Vector2[] top = Diamond(cell);
        if (!MapData.IsWalkable(cell))
        {
            DrawBlock(cell);
            return;
        }
        Color floorColor = (cell.X + cell.Y) % 2 == 0 ? ColorFloor : ColorFloorAlt;
        DrawColoredPolygon(top, MapData.IsHazard(cell) ? ColorHazard : floorColor);
        if (MapData.IsHazard(cell))
        {
            // Hazards are never communicated by color alone: ember cross-hatch.
            Vector2[] inner = Diamond(cell, 0.0f, 3.0f);
            DrawLine(inner[0].Lerp(inner[3], 0.5f), inner[1].Lerp(inner[2], 0.5f), ColorHazardLine, 1.0f);
            DrawLine(inner[0].Lerp(inner[1], 0.5f), inner[3].Lerp(inner[2], 0.5f), ColorHazardLine, 1.0f);
        }
        if (MoveHighlights.Contains(cell))
        {
            DrawColoredPolygon(Diamond(cell, 0.0f, 1.0f), ColorMoveFill);
            DrawPolyline(Closed(Diamond(cell, 0.0f, 1.0f)), ColorMoveEdge, 1.0f);
        }
        DrawPolyline(Closed(top), ColorEdge, 1.0f);
        if (cell == MapData.ObjectiveCell)
        {
            DrawPolyline(Closed(Diamond(cell, 0.0f, 2.0f)), ColorObjective, 1.0f);
            Vector2 c = CellToLocal(cell) - new Vector2(0, 6);
            DrawColoredPolygon(new[] { c + new Vector2(0, -4), c + new Vector2(3, 0), c + new Vector2(0, 4), c + new Vector2(-3, 0) }, ColorObjective);
        }
    }

    private void DrawBlock(Vector2I cell)
    {
        Vector2[] baseShape = Diamond(cell);
        Vector2[] top = Diamond(cell, BlockHeight);
        DrawColoredPolygon(new[] { top[3], top[2], baseShape[2], baseShape[3] }, ColorBlockLeft);
        DrawColoredPolygon(new[] { top[2], top[1], baseShape[1], baseShape[2] }, ColorBlockRight);
        DrawColoredPolygon(top, ColorBlockTop);
        DrawPolyline(Closed(top), ColorBlockRight, 1.0f);
    }

    private static Vector2[] Closed(Vector2[] points)
    {
        var output = new Vector2[points.Length + 1];
        points.CopyTo(output, 0);
        output[^1] = points[0];
        return output;
    }

    private static int RoundHalfAway(float value) => (int)System.MathF.Round(value, System.MidpointRounding.AwayFromZero);
}
