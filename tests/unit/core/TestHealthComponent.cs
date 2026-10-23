using System.Collections.Generic;
using Godot;

namespace Utopia.Tests.Unit.Core;

public class TestHealthComponent : TestCase
{
    private HealthComponent Make(int maxHealth = 10) => AddNode(new HealthComponent { MaxHealth = maxHealth });

    public void TestStartsAtFullHealth()
    {
        HealthComponent health = Make(12);
        AssertEq(health.CurrentHealth, 12);
        AssertFalse(health.IsDead());
    }

    public void TestDamageIsClampedAndReportsDealtAmount()
    {
        HealthComponent health = Make(5);
        AssertEq(health.ApplyDamage(3), 3);
        AssertEq(health.ApplyDamage(10), 2, "only remaining health can be dealt");
        AssertEq(health.CurrentHealth, 0);
        AssertTrue(health.IsDead());
    }

    public void TestDiedEmitsExactlyOnce()
    {
        HealthComponent health = Make(3);
        int deaths = 0;
        health.Died += () => deaths++;
        health.ApplyDamage(3);
        health.ApplyDamage(3);
        AssertEq(deaths, 1);
    }

    public void TestZeroOrNegativeDamageIsIgnored()
    {
        HealthComponent health = Make(5);
        int events = 0;
        health.HealthChanged += (_, _, _) => events++;
        AssertEq(health.ApplyDamage(0), 0);
        AssertEq(health.ApplyDamage(-4), 0);
        AssertEq(events, 0);
    }

    public void TestHealCapsAtMaxAndCannotHealTheDead()
    {
        HealthComponent health = Make(10);
        health.ApplyDamage(4);
        AssertEq(health.Heal(100), 4);
        AssertEq(health.CurrentHealth, 10);
        health.ApplyDamage(10);
        AssertEq(health.Heal(5), 0);
        health.Revive(3);
        AssertEq(health.CurrentHealth, 3);
    }

    public void TestLoweringMaxHealthClampsCurrent()
    {
        HealthComponent health = Make(10);
        health.SetCurrentHealth(8);
        health.MaxHealth = 5;
        AssertEq(health.CurrentHealth, 5);
    }

    public void TestDamagedSignalCarriesSource()
    {
        HealthComponent health = Make(10);
        var sources = new List<StringName>();
        health.Damaged += (_, source) => sources.Add(source);
        health.ApplyDamage(1, "hazard");
        AssertEq(sources, new[] { "hazard" });
    }
}
