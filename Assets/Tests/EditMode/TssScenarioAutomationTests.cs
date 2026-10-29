using NUnit.Framework;

public sealed class TssScenarioAutomationTests
{
    [Test]
    public void AutomationBuildsValidT568BState()
    {
        var state = TssScenarioAutomation.CreateT568BState("Infrastructure001", "Patch:01", "Infrastructure001:A");

        Assert.IsTrue(TssT568BValidator.HasExactEightPins(state));
        Assert.IsTrue(TssT568BValidator.IsElectricallyCorrect(state));
        Assert.AreEqual("Infrastructure001", state.connectionId);
        Assert.AreEqual("Patch:01", state.endpointId);
    }
}
