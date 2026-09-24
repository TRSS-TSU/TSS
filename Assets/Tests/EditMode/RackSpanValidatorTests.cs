using System.Collections.Generic;
using System.Reflection;
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
        // ponytail: reflection avoids adding production asmdefs just so this one EditMode test can see Assembly-CSharp.
        var type = Assembly.Load("Assembly-CSharp").GetType("RackSpanValidator", throwOnError: true);
        var method = type.GetMethod("CanPlace", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method);

        return (bool)method.Invoke(null, new object[]
        {
            startingU,
            rackUnits,
            firstInstallableU,
            lastInstallableU,
            reservedU,
            occupiedU
        });
    }
}
