using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Utopia.Tests.Integration;

/// <summary>Movement and animation contract of the reusable character scene.</summary>
public class TestCharacterScene : TestCase
{
    private const int Cell = 48;

    private static Vector2 At(float x, float y) => new(Cell * x, Cell * y);

    private Character MakeCharacter()
    {
        var unit = Instantiate<Character>("res://features/characters/character.tscn");
        unit.Configure(new UnitStats { MaxHealth = 10 }, "HERO_DESERTER_NAME");
        unit.PlaceAt(new Vector2I(1, 1), At(1.5f, 1.5f));
        return unit;
    }

    private static void MotionOn() => SettingsService.Instance.ReducedMotion = false;

    public void TestLogicalCellUpdatesBeforeAnimationFinishes()
    {
        MotionOn();
        Character unit = MakeCharacter();
        unit.StepTo(new Vector2I(2, 1), At(2.5f, 1.5f));
        AssertEq(unit.Cell, new Vector2I(2, 1), "gameplay state is immediate");
        AssertTrue(unit.Mover.IsMoving(), "visual is still catching up");
    }

    public async Task TestVisualEndsExactlyOnCellCenter()
    {
        MotionOn();
        Character unit = MakeCharacter();
        Vector2 target = At(2.5f, 1.5f);
        unit.StepTo(new Vector2I(2, 1), target);
        AssertTrue(await WaitUntil(() => !unit.Mover.IsMoving(), 2.0));
        AssertEq(unit.Position, target, "visible contact alignment");
    }

    public async Task TestFrameRateDoesNotChangeOutcome()
    {
        MotionOn();
        Character unit = MakeCharacter();
        Vector2 target = At(2.5f, 1.5f);
        Engine.TimeScale = 4.0; // simulates very large frame deltas
        unit.StepTo(new Vector2I(2, 1), target);
        await WaitUntil(() => !unit.Mover.IsMoving(), 2.0);
        Engine.TimeScale = 1.0;
        AssertEq(unit.Position, target);
        AssertEq(unit.Cell, new Vector2I(2, 1));
    }

    public void TestReducedMotionSnaps()
    {
        SettingsService.Instance.ReducedMotion = true;
        Character unit = MakeCharacter();
        unit.StepTo(new Vector2I(1, 2), At(1.5f, 2.5f));
        AssertFalse(unit.Mover.IsMoving());
        AssertEq(unit.Position, At(1.5f, 2.5f));
    }

    public async Task TestGameplayStateDrivesAnimationTransitions()
    {
        MotionOn();
        Character unit = MakeCharacter();
        AssertEq(unit.Animator.CurrentAnimation(), CharacterAnimator.AnimIdle);
        unit.StepTo(new Vector2I(2, 1), At(2.5f, 1.5f));
        AssertEq(unit.Animator.CurrentAnimation(), CharacterAnimator.AnimMove);
        await WaitUntil(() => !unit.Mover.IsMoving(), 2.0);
        AssertEq(unit.Animator.CurrentAnimation(), CharacterAnimator.AnimIdle);
        unit.Health.ApplyDamage(1);
        AssertEq(unit.Animator.CurrentAnimation(), CharacterAnimator.AnimHit);
        await WaitUntil(() => unit.Animator.CurrentAnimation() == CharacterAnimator.AnimIdle, 2.0);
        unit.Health.ApplyDamage(99);
        AssertEq(unit.Animator.CurrentAnimation(), CharacterAnimator.AnimDefeat);
        unit.StepTo(new Vector2I(3, 1), At(3.5f, 1.5f));
        AssertEq(unit.Animator.CurrentAnimation(), CharacterAnimator.AnimDefeat, "defeat is terminal");
    }

    public async Task TestAnimationEventsFireFromTracks()
    {
        MotionOn();
        Character unit = MakeCharacter();
        var events = new List<StringName>();
        unit.Animator.AnimationEvent += e => events.Add(e);
        unit.StepTo(new Vector2I(2, 1), At(2.5f, 1.5f));
        await WaitSeconds(0.25);
        AssertContains(events, "footstep");
        unit.Health.ApplyDamage(1);
        await WaitFrames(2);
        AssertContains(events, "hit_impact");
    }

    public void TestFacingFollowsHorizontalMovement()
    {
        SettingsService.Instance.ReducedMotion = true;
        Character unit = MakeCharacter();
        unit.StepTo(new Vector2I(0, 1), At(0.5f, 1.5f));
        AssertEq(unit.Visual.Facing, -1);
        unit.StepTo(new Vector2I(1, 1), At(1.5f, 1.5f));
        AssertEq(unit.Visual.Facing, 1);
    }

    public void TestDefeatedSignal()
    {
        Character unit = MakeCharacter();
        int defeated = 0;
        unit.Defeated += () => defeated++;
        unit.Health.ApplyDamage(100);
        AssertEq(defeated, 1);
        AssertTrue(unit.IsDefeated());
    }
}
