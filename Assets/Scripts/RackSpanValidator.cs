using System.Collections.Generic;
using System.Linq;

public static class RackSpanValidator
{
    public static bool CanPlace(
        int startingU,
        int rackUnits,
        int firstInstallableU,
        int lastInstallableU,
        IReadOnlyCollection<int> reservedU,
        IReadOnlyCollection<int> occupiedU)
    {
        if (rackUnits < 1 || startingU < firstInstallableU)
            return false;

        var endingU = startingU + rackUnits - 1;
        if (endingU > lastInstallableU)
            return false;

        for (var u = startingU; u <= endingU; u++)
        {
            if ((reservedU != null && reservedU.Contains(u)) || (occupiedU != null && occupiedU.Contains(u)))
                return false;
        }

        return true;
    }
}
