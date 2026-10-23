using Godot;

namespace Utopia.Tests.Unit.Core;

public class TestRoutes : TestCase
{
    private static readonly RouteTable RouteTable = GD.Load<RouteTable>(SceneRouter.RouteTablePath);

    public void TestEveryRoutePointsToALoadableScene()
    {
        foreach (StringName route in RouteTable.Routes.Keys)
        {
            string path = RouteTable.PathFor(route);
            AssertTrue(ResourceLoader.Exists(path), $"route {route} -> {path}");
            AssertTrue(GD.Load(path) is PackedScene, $"route {route} is not a scene");
        }
    }

    public void TestRouteConstantsAreRegistered()
    {
        foreach (StringName route in new[] { Routes.Bootstrap, Routes.MainMenu, Routes.Gameplay })
            AssertTrue(RouteTable.HasRoute(route), route);
    }

    public void TestMainSceneIsBootstrap() =>
        AssertEq(ProjectSettings.GetSetting("application/run/main_scene").AsString(), RouteTable.PathFor(Routes.Bootstrap));
}
