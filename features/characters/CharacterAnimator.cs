using Godot;

namespace Utopia;

/// <summary>
/// Translates gameplay signals into animation. Gameplay never calls
/// AnimationPlayer directly, and animation never decides gameplay outcomes.
///
/// Animation tracks call <see cref="EmitAnimationEvent"/> at authored frames
/// (footsteps, impact frames); audio/VFX/camera listeners subscribe to
/// <see cref="AnimationEvent"/>.
/// </summary>
[GlobalClass]
public partial class CharacterAnimator : Node
{
    [Signal] public delegate void AnimationEventEventHandler(StringName eventName);

    public static readonly StringName AnimIdle = "unit/idle";
    public static readonly StringName AnimMove = "unit/move";
    public static readonly StringName AnimHit = "unit/hit";
    public static readonly StringName AnimDefeat = "unit/defeat";

    [Export] public AnimationPlayer Player { get; set; }
    [Export] public HealthComponent Health { get; set; }
    [Export] public GridMover Mover { get; set; }
    [Export] public UnitVisual Visual { get; set; }

    public override void _Ready()
    {
        Health.Damaged += OnDamaged;
        Health.Died += OnDied;
        Mover.MoveStarted += OnMoveStarted;
        Mover.MoveFinished += OnMoveFinished;
        Player.AnimationFinished += OnAnimationFinished;
        Play(AnimIdle);
    }

    public void Play(StringName animation)
    {
        if (Health.IsDead() && animation != AnimDefeat)
            return;
        if (Player.HasAnimation(animation))
            Player.Play(animation);
    }

    public StringName CurrentAnimation() => Player.CurrentAnimation;

    /// <summary>Called from animation method tracks.</summary>
    public void EmitAnimationEvent(StringName eventName) => EmitSignalAnimationEvent(eventName);

    private void OnMoveStarted(Vector2I direction)
    {
        if (direction.X != 0)
            Visual.Facing = Mathf.Sign(direction.X);
        Play(AnimMove);
    }

    private void OnMoveFinished()
    {
        if (CurrentAnimation() == AnimMove)
            Play(AnimIdle);
    }

    private void OnDamaged(int amount, StringName source)
    {
        if (!Health.IsDead())
            Play(AnimHit);
    }

    private void OnDied() => Play(AnimDefeat);

    private void OnAnimationFinished(StringName animation)
    {
        if (animation == AnimHit)
            Play(AnimIdle);
    }
}
