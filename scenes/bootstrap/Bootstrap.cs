using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// First scene. Verifies that required services and content are available,
/// then hands over to the main menu (or a dev-only --route=&lt;id&gt; override).
/// </summary>
public partial class Bootstrap : Control
{
    [Export] public Label StatusLabel { get; set; }
    [Export] public Control ErrorPanel { get; set; }
    [Export] public GameButton QuitButton { get; set; }

    public override async void _Ready()
    {
        ErrorPanel.Visible = false;
        QuitButton.Activated += () => GetTree().Quit(1);
        // Let autoloads finish _Ready and the loading text render once.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        // Dev/QA builds only, and only when requested on the command line.
        if (!GetTree().Root.HasNode("Automation"))
            AutomationGate.StartIfRequested(GetTree());
        List<string> problems = VerifyStartup();
        if (problems.Count > 0)
        {
            foreach (string problem in problems)
                DevLog.Error("bootstrap", problem);
            ShowStartupError();
            return;
        }
        // If bootstrap itself was reached through the router, let that finish first.
        while (SceneRouter.Instance.IsTransitioning)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _ = SceneRouter.Instance.Goto(StartRoute());
    }

    /// <summary>Returns developer-facing problems; empty means the game can start.</summary>
    public static List<string> VerifyStartup()
    {
        var problems = new List<string>();
        foreach (StringName route in new[] { Routes.MainMenu, Routes.Gameplay })
            if (!SceneRouter.Instance.HasRoute(route))
                problems.Add($"Missing route '{route}'");
        if (!System.Array.Exists(TranslationServer.GetLoadedLocales(), l => l == LocaleRegistry.SourceLocale))
            problems.Add($"Source locale catalog '{LocaleRegistry.SourceLocale}' is not loaded");
        RosterCatalog roster = GameSession.Roster;
        if (roster.DefaultHero == null || roster.DefaultBattalion == null)
            problems.Add("Roster catalog has no default hero/battalion");
        return problems;
    }

    /// <summary>Dev builds accept <c>-- --route=gameplay</c> to jump straight into a scene.</summary>
    public static StringName StartRoute()
    {
        if (OS.IsDebugBuild())
        {
            foreach (string arg in OS.GetCmdlineUserArgs())
            {
                if (!arg.StartsWith("--route="))
                    continue;
                StringName route = arg["--route=".Length..];
                if (SceneRouter.Instance.HasRoute(route) && route != Routes.Bootstrap)
                    return route;
            }
        }
        return Routes.MainMenu;
    }

    private void ShowStartupError()
    {
        StatusLabel.Visible = false;
        ErrorPanel.Visible = true;
        QuitButton.GrabFocus();
    }
}
