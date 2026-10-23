using Godot;

namespace Utopia.Tests.Unit.Art;

/// <summary>Enforces the pixel-art presentation rules in docs/ART_IMPLEMENTATION_GUIDE.md.</summary>
public class TestPixelRules : TestCase
{
    public void TestTexturesDefaultToNearestFiltering() =>
        AssertEq(ProjectSettings.GetSetting("rendering/textures/canvas_textures/default_texture_filter").AsInt32(), 0);

    public void TestPixelSnappingIsEnabled()
    {
        AssertTrue(ProjectSettings.GetSetting("rendering/2d/snap/snap_2d_transforms_to_pixel").AsBool());
        AssertTrue(ProjectSettings.GetSetting("rendering/2d/snap/snap_2d_vertices_to_pixel").AsBool());
    }

    public void TestGameplayCameraUsesAnIntegerZoomAndUnitsAreYSorted()
    {
        Node gameplay = GD.Load<PackedScene>("res://scenes/gameplay/gameplay.tscn").Instantiate();
        var camera = gameplay.GetNode<Camera2D>("World/Camera2D");
        AssertEq(camera.Zoom, new Vector2(2, 2), "640x360 art space on the 1280x720 canvas");
        AssertTrue(gameplay.GetNode<Node2D>("World/BattleGrid/Units").YSortEnabled);
        gameplay.Free();
    }

    public void TestLogicalResolutionIsAnIntegerMultipleOfTheArtFrame()
    {
        int width = ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32();
        int height = ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32();
        AssertEq(new Vector2I(width, height), new Vector2I(640, 360) * 2);
    }
}
