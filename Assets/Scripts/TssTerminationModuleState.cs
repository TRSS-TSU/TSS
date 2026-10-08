using System;

public enum TssConductorColor
{
    None = 0,
    WhiteOrange = 1,
    Orange = 2,
    WhiteGreen = 3,
    Blue = 4,
    WhiteBlue = 5,
    Green = 6,
    WhiteBrown = 7,
    Brown = 8
}

[Serializable]
public sealed class TssTerminationTerminalState
{
    public int pin;
    public TssConductorColor conductor;
    public bool hasConductor;
    public bool isPunched;
    public int operationOrder;
    public int rotationSteps;
    public bool flipped;

    public TssTerminationTerminalState Clone()
    {
        return new TssTerminationTerminalState
        {
            pin = pin,
            conductor = conductor,
            hasConductor = hasConductor,
            isPunched = isPunched,
            operationOrder = operationOrder,
            rotationSteps = rotationSteps,
            flipped = flipped
        };
    }
}

[Serializable]
public sealed class TssTerminationModuleState
{
    public string connectionId;
    public string endpointId;
    public string moduleId;
    public string endpointRole;
    public TssTerminationTerminalState[] terminals = Array.Empty<TssTerminationTerminalState>();

    public TssTerminationModuleState Clone()
    {
        var copy = new TssTerminationModuleState
        {
            connectionId = connectionId,
            endpointId = endpointId,
            moduleId = moduleId,
            endpointRole = endpointRole,
            terminals = new TssTerminationTerminalState[terminals?.Length ?? 0]
        };

        for (var i = 0; i < copy.terminals.Length; i++)
            copy.terminals[i] = terminals[i]?.Clone();

        return copy;
    }
}

public static class TssT568BValidator
{
    private static readonly TssConductorColor[] Expected =
    {
        TssConductorColor.WhiteOrange,
        TssConductorColor.Orange,
        TssConductorColor.WhiteGreen,
        TssConductorColor.Blue,
        TssConductorColor.WhiteBlue,
        TssConductorColor.Green,
        TssConductorColor.WhiteBrown,
        TssConductorColor.Brown
    };

    public static bool IsElectricallyCorrect(TssTerminationModuleState state)
    {
        if (!HasExactEightPins(state))
            return false;

        for (var i = 0; i < Expected.Length; i++)
        {
            var terminal = FindPin(state.terminals, i + 1);
            if (terminal == null
                || !terminal.hasConductor
                || !terminal.isPunched
                || terminal.conductor != Expected[i])
                return false;
        }

        return true;
    }

    public static bool HasExactEightPins(TssTerminationModuleState state)
    {
        if (state?.terminals == null || state.terminals.Length != 8)
            return false;

        var seen = new bool[8];
        foreach (var terminal in state.terminals)
        {
            if (terminal == null || terminal.pin < 1 || terminal.pin > 8)
                return false;

            var index = terminal.pin - 1;
            if (seen[index])
                return false;

            seen[index] = true;
        }

        return true;
    }

    private static TssTerminationTerminalState FindPin(TssTerminationTerminalState[] terminals, int pin)
    {
        foreach (var terminal in terminals)
        {
            if (terminal != null && terminal.pin == pin)
                return terminal;
        }

        return null;
    }
}
