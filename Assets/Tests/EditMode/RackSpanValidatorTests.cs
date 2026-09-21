using System.Collections.Generic;
using NUnit.Framework;

public sealed class RackSpanValidatorTests
{
    [Test]
    public void ValidatesRackInstallSpans()
    {
        var reserved = new HashSet<int> { 40 };
        var occupied = new HashSet<int>();

        Assert.IsTrue(RackSpanValidator.CanPlace(3, 1, 3, 39, reserved, occupied));
        Assert.IsTrue(RackSpanValidator.CanPlace(38, 2, 3, 39, reserved, occupied));

        occupied.Add(10);
        Assert.IsFalse(RackSpanValidator.CanPlace(10, 1, 3, 39, reserved, occupied));
        Assert.IsFalse(RackSpanValidator.CanPlace(9, 2, 3, 39, reserved, occupied));

        Assert.IsFalse(RackSpanValidator.CanPlace(1, 1, 3, 39, reserved, occupied));
        Assert.IsFalse(RackSpanValidator.CanPlace(2, 1, 3, 39, reserved, occupied));
        Assert.IsFalse(RackSpanValidator.CanPlace(40, 1, 3, 39, reserved, occupied));
        Assert.IsFalse(RackSpanValidator.CanPlace(39, 2, 3, 39, reserved, occupied));
    }
}
