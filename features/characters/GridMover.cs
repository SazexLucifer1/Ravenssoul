using Godot;

namespace Utopia;

/// <summary>
/// Moves a Node2D between grid cell positions with a short tween.
///
/// Visual-only: the logical cell is already updated by the time the tween
/// starts, so frame rate never changes game outcomes. Reduced motion snaps.
/// </summary>
[GlobalClass]
public partial class GridMover : Node
{
    [Signal] public delegate void MoveStartedEventHandler(Vector2I direction);
    [Signal] public delegate void MoveFinishedEventHandler();

    [Export] public Node2D Body { get; set; }
    [Export(PropertyHint.Range, "0.05,0.5")] public float StepSeconds { get; set; } = 0.18f;

    private Tween _tween;

    public bool IsMoving() => _tween != null && _tween.IsRunning();

    public void SnapTo(Vector2 worldPosition)
    {
        _tween?.Kill();
        Body.Position = worldPosition;
    }

    public void MoveTo(Vector2 worldPosition, Vector2I direction)
    {
        _tween?.Kill();
        EmitSignalMoveStarted(direction);
        if (UiMotion.Reduced() || !Body.IsInsideTree())
        {
            Body.Position = worldPosition;
            EmitSignalMoveFinished();
            return;
        }
        _tween = Body.CreateTween();
        _tween.TweenProperty(Body, "position", worldPosition, StepSeconds)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _tween.Finished += EmitSignalMoveFinished;
    }
}
