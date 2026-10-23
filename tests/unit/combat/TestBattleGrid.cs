using System.Collections.Generic;
using Godot;

namespace Utopia.Tests.Unit.Combat;

/// <summary>
/// Grid movement is the tactical "collision" system. These tests cover the
/// grid equivalents of tunneling, corners, overlap, and repeated hits.
/// </summary>
public class TestBattleGrid : TestCase
{
    private static readonly BattleMapData Map = GD.Load<BattleMapData>("res://features/combat/content/maps/map_training_ground.tres");

    private BattleGrid MakeGrid() => AddNode(new BattleGrid { MapData = Map });

    public void TestAuthoredMapIsValid() => AssertEmpty(Map.Validate());

    public void TestBlockedAndOutsideCellsCannotBeEntered()
    {
        BattleGrid grid = MakeGrid();
        AssertFalse(grid.CanEnter(new Vector2I(4, 3)), "blocked");
        AssertFalse(grid.CanEnter(new Vector2I(-1, 0)), "outside left");
        AssertFalse(grid.CanEnter(Map.Size), "outside bottom-right corner");
        AssertTrue(grid.CanEnter(new Vector2I(0, 0)));
    }

    public void TestOccupiedCellsCannotOverlap()
    {
        BattleGrid grid = MakeGrid();
        var a = new Node();
        var b = new Node();
        AssertTrue(grid.Place(a, new Vector2I(1, 1)));
        AssertFalse(grid.Place(b, new Vector2I(1, 1)));
        AssertEq(grid.OccupantAt(new Vector2I(1, 1)), a);
        a.Free();
        b.Free();
    }

    public void TestCellWorldRoundTripIsExactAtCellCenters()
    {
        BattleGrid grid = MakeGrid();
        foreach (Vector2I cell in new[] { new Vector2I(0, 0), new Vector2I(5, 7), new Vector2I(11, 11) })
            AssertEq(grid.LocalToCell(grid.CellToLocal(cell)), cell);
    }

    private PlayerUnitController ControllerWithUnit(Vector2I start)
    {
        BattleGrid grid = MakeGrid();
        var unit = Instantiate<Character>("res://features/characters/character.tscn", grid);
        unit.Configure(new UnitStats { MaxHealth = 10 }, "HERO_DESERTER_NAME");
        grid.Place(unit, start);
        unit.PlaceAt(start, grid.CellToLocal(start));
        return AddNode(new PlayerUnitController { Grid = grid, Unit = unit });
    }

    public void TestNoTunnelingThroughWalls()
    {
        // (3,4) -> right is the wall column x=4; a step must never skip past it.
        PlayerUnitController controller = ControllerWithUnit(new Vector2I(3, 4));
        AssertFalse(controller.TryStep(Vector2I.Right));
        AssertEq(controller.Unit.Cell, new Vector2I(3, 4));
        AssertFalse(controller.TryStep(new Vector2I(2, 0)), "multi-cell jumps are rejected");
        AssertFalse(controller.TryStep(new Vector2I(1, 1)), "diagonal corner cutting is rejected");
    }

    public void TestHazardHitsOncePerEntry()
    {
        PlayerUnitController controller = ControllerWithUnit(new Vector2I(5, 7));
        var hits = new List<int>();
        controller.HazardEntered += (_, dealt) => hits.Add(dealt);
        AssertTrue(controller.TryStep(Vector2I.Down)); // onto hazard (5,8)
        AssertEq(hits.Count, 1);
        AssertEq(controller.Unit.Health.CurrentHealth, 10 - Map.HazardDamage);
        AssertFalse(controller.TryStep(Vector2I.Zero), "standing still is not a step");
        AssertEq(hits.Count, 1, "no repeated hit without re-entering");
    }

    public void TestObjectiveSignal()
    {
        PlayerUnitController controller = ControllerWithUnit(Map.ObjectiveCell + Vector2I.Left);
        var reached = new List<Vector2I>();
        controller.ObjectiveReached += cell => reached.Add(cell);
        controller.TryStep(Vector2I.Right);
        AssertEq(reached, new[] { Map.ObjectiveCell });
    }

    public void TestIsometricProjectionIs2To1()
    {
        BattleGrid grid = MakeGrid();
        Vector2 origin = grid.CellToLocal(Vector2I.Zero);
        AssertEq(grid.CellToLocal(new Vector2I(1, 0)) - origin, new Vector2(16, 8), "+x goes down-right");
        AssertEq(grid.CellToLocal(new Vector2I(0, 1)) - origin, new Vector2(-16, 8), "+y goes down-left");
        AssertEq(BattleGrid.TileSize.X, BattleGrid.TileSize.Y * 2);
    }

    public void TestEveryCellRoundTripsAndFitsTheBounds()
    {
        BattleGrid grid = MakeGrid();
        Rect2 bounds = grid.WorldBounds();
        for (int y = 0; y < Map.Size.Y; y++)
        {
            for (int x = 0; x < Map.Size.X; x++)
            {
                Vector2 center = grid.CellToLocal(new Vector2I(x, y));
                AssertEq(grid.LocalToCell(center), new Vector2I(x, y));
                AssertEq(grid.LocalToCell(center + new Vector2(5, 2)), new Vector2I(x, y), "points inside the diamond pick the cell");
                AssertTrue(bounds.HasPoint(center));
            }
        }
        AssertTrue(bounds.Size.X <= 640 && bounds.Size.Y <= 360, "a 12x12 map fits the 640x360 art frame");
    }
}
