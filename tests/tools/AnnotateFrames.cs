using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Utopia.Tests.Tools;

/// <summary>
/// Draws review annotations (focal path, competing focal points, unclear
/// state, highest-impact fix) onto captured frames for
/// docs/VISUAL_QUALITY_RUBRIC.md. Needs a renderer (Xvfb on Linux):
///   xvfb-run -a godot --path . --rendering-driver opengl3 --audio-driver Dummy \
///     res://tests/tools/annotate_frames.tscn -- --in=&lt;captures dir&gt; --out=docs/art/annotated
/// Specs live in Specs below; coordinates are in 1280x720 frame pixels.
/// </summary>
public partial class AnnotateFrames : Node
{
    private static readonly Color Focal = new("6fe08a");
    private static readonly Color Competing = new("f2c14e");
    private static readonly Color Problem = new("ff5a4f");
    private static readonly Color Fix = new("8cc4ff");

    private sealed record Spec(string Input, string Output, List<AnnotationMark> Marks);

    private static readonly Spec[] Specs =
    {
        new("state_battle_start.png", "battle_start_annotated.png", new List<AnnotationMark>
        {
            AnnotationMark.Circle(new Vector2(352, 360), 26, Focal, "1 unit (intended first read) - only ~12 art px tall", new Vector2(300, 312)),
            AnnotationMark.Circle(new Vector2(352, 365), 62, Focal, "2 move range", new Vector2(140, 470)),
            AnnotationMark.Circle(new Vector2(928, 366), 24, Focal, "3 objective", new Vector2(880, 410)),
            AnnotationMark.Arrow(new Vector2(380, 352), new Vector2(900, 362), Focal),
            AnnotationMark.Box(new Rect2(32, 32, 260, 146), Competing, "competing: bright HUD frames out-weigh the unit", new Vector2(32, 196)),
            AnnotationMark.Box(new Rect2(948, 32, 300, 164), Competing, "", new Vector2(948, 210)),
            AnnotationMark.Box(new Rect2(430, 590, 420, 40), Problem, "missing: turn / AP / hand / threat - no tactical state shown", new Vector2(470, 652)),
            AnnotationMark.Box(new Rect2(256, 176, 768, 384), Fix, "fix #1: 32x48 unit sprites + iso tile art; fix #2: card hand + AP bar bottom-centre", new Vector2(256, 572)),
        }),
        new("en_main_menu.png", "main_menu_annotated.png", new List<AnnotationMark>
        {
            AnnotationMark.Circle(new Vector2(224, 298), 34, Focal, "1 primary action (clear)", new Vector2(400, 290)),
            AnnotationMark.Box(new Rect2(460, 60, 780, 560), Problem, "60% of frame empty: no key art / world fantasy", new Vector2(600, 340)),
            AnnotationMark.Box(new Rect2(60, 70, 200, 70), Fix, "fix: pixel display font + emblem; wood/brass frame art", new Vector2(60, 230)),
        }),
    };

    private string _inDir = "";
    private string _outDir = "res://docs/art/annotated";

    public override async void _Ready()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--in="))
                _inDir = arg["--in=".Length..];
            else if (arg.StartsWith("--out="))
                _outDir = arg["--out=".Length..];
        }
        DirAccess.MakeDirRecursiveAbsolute(_outDir);
        foreach (Spec spec in Specs)
            await Render(spec);
        GetTree().Quit();
    }

    private async Task Render(Spec spec)
    {
        Image image = Image.LoadFromFile(_inDir.PathJoin(spec.Input));
        if (image == null)
        {
            GD.PushError($"missing capture {spec.Input}");
            return;
        }
        var viewport = new SubViewport { Size = new Vector2I(1280, 720), RenderTargetUpdateMode = SubViewport.UpdateMode.Once };
        viewport.AddChild(new TextureRect { Texture = ImageTexture.CreateFromImage(image) });
        viewport.AddChild(new AnnotationOverlay { Marks = spec.Marks, Size = new Vector2(1280, 720) });
        AddChild(viewport);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string output = _outDir.PathJoin(spec.Output);
        viewport.GetTexture().GetImage().SavePng(output);
        GD.Print("saved ", output);
        viewport.QueueFree();
    }
}
