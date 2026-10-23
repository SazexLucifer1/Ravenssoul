using System.Linq;

namespace Utopia.Tests.Unit.Combat;

public class TestTurnOrder : TestCase
{
    private sealed record Entry(string Id, int Speed);

    public void TestSortedByDescendingSpeed()
    {
        var order = TurnOrder.SortBySpeed(new[] { new Entry("a", 3), new Entry("b", 9), new Entry("c", 5) }, e => e.Speed);
        AssertEq(order.Select(e => e.Id).ToList(), new[] { "b", "c", "a" });
    }

    public void TestTiesKeepInputOrder()
    {
        var order = TurnOrder.SortBySpeed(new[] { new Entry("a", 4), new Entry("b", 4), new Entry("c", 4) }, e => e.Speed);
        AssertEq(order.Select(e => e.Id).ToList(), new[] { "a", "b", "c" });
    }

    public void TestWorksWithStatResources()
    {
        var slow = new UnitStats { Speed = 1 };
        var fast = new UnitStats { Speed = 7 };
        AssertEq(TurnOrder.SortBySpeed(new[] { slow, fast })[0], fast);
    }
}
