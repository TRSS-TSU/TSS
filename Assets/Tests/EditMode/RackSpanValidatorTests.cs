using System.Collections.Generic;
using NUnit.Framework;

public sealed class RackSpanValidatorTests
{
    [Test]
    public void ValidatesRackInstallSpans()
    {
        var reserved = new HashSet<int> { 40 };
        var occupied = new HashSet<int>();

        Assert.IsTrue(CanPlace(3, 1, 3, 39, reserved, occupied));
        Assert.IsTrue(CanPlace(38, 2, 3, 39, reserved, occupied));

        occupied.Add(10);
        Assert.IsFalse(CanPlace(10, 1, 3, 39, reserved, occupied));
        Assert.IsFalse(CanPlace(9, 2, 3, 39, reserved, occupied));

        Assert.IsFalse(CanPlace(1, 1, 3, 39, reserved, occupied));
        Assert.IsFalse(CanPlace(2, 1, 3, 39, reserved, occupied));
        Assert.IsFalse(CanPlace(40, 1, 3, 39, reserved, occupied));
        Assert.IsFalse(CanPlace(39, 2, 3, 39, reserved, occupied));
    }

    private static bool CanPlace(
        int startingU,
        int rackUnits,
        int firstInstallableU,
        int lastInstallableU,
        IReadOnlyCollection<int> reservedU,
        IReadOnlyCollection<int> occupiedU)
    {
        return RackSpanValidator.CanPlace(
            startingU,
            rackUnits,
            firstInstallableU,
            lastInstallableU,
            reservedU,
            occupiedU);
    }
}
