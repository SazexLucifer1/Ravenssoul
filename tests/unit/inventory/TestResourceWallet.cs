using System.Text.Json.Nodes;

namespace Utopia.Tests.Unit.Inventory;

public class TestResourceWallet : TestCase
{
    public void TestAddAndSpend()
    {
        var wallet = new ResourceWallet();
        wallet.Add(ResourceWallet.Gold, 50);
        AssertTrue(wallet.Spend(ResourceWallet.Gold, 20));
        AssertEq(wallet.Amount(ResourceWallet.Gold), 30);
    }

    public void TestCannotOverspend()
    {
        var wallet = new ResourceWallet();
        wallet.Add(ResourceWallet.Wood, 5);
        AssertFalse(wallet.Spend(ResourceWallet.Wood, 6));
        AssertEq(wallet.Amount(ResourceWallet.Wood), 5);
    }

    public void TestSaveRoundTripUsesStableIdsAndSanitizes()
    {
        var wallet = new ResourceWallet();
        wallet.Add(ResourceWallet.SoulEnergy, 3);
        JsonObject data = wallet.ToSaveData();
        AssertEq(data["soul_energy"], 3);
        data["food"] = -10;
        var restored = new ResourceWallet();
        restored.LoadSaveData(data);
        AssertEq(restored.Amount(ResourceWallet.SoulEnergy), 3);
        AssertEq(restored.Amount(ResourceWallet.Food), 0, "negative values are clamped");
    }
}
