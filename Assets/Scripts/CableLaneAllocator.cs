using System.Collections.Generic;
using System.Linq;

public readonly struct CableLaneReservation
{
    public readonly string ConnectionId;
    public readonly IReadOnlyDictionary<string, int> EdgeSlots;
    public readonly int MaximumSlot;

    public CableLaneReservation(string connectionId, IReadOnlyDictionary<string, int> edgeSlots, int maximumSlot)
    {
        ConnectionId = connectionId;
        EdgeSlots = edgeSlots;
        MaximumSlot = maximumSlot;
    }
}

public sealed class CableLaneAllocator
{
    private readonly Dictionary<string, SortedSet<int>> _usedSlotsByEdge = new();
    private readonly Dictionary<string, CableLaneReservation> _reservations = new();

    public CableLaneReservation Reserve(string connectionId, IEnumerable<string> edgeKeys)
    {
        Release(connectionId);

        var keys = (edgeKeys ?? Enumerable.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();
        var allocated = new Dictionary<string, int>();
        var maximumSlot = 0;
        foreach (var key in keys)
        {
            if (!_usedSlotsByEdge.TryGetValue(key, out var used))
            {
                used = new SortedSet<int>();
                _usedSlotsByEdge[key] = used;
            }

            var slot = 0;
            while (used.Contains(slot))
                slot++;
            used.Add(slot);
            allocated[key] = slot;
            maximumSlot = System.Math.Max(maximumSlot, slot);
        }

        var reservation = new CableLaneReservation(connectionId, allocated, maximumSlot);
        _reservations[connectionId] = reservation;
        return reservation;
    }

    public void Release(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || !_reservations.TryGetValue(connectionId, out var reservation))
            return;

        foreach (var pair in reservation.EdgeSlots)
        {
            if (!_usedSlotsByEdge.TryGetValue(pair.Key, out var used))
                continue;
            used.Remove(pair.Value);
            if (used.Count == 0)
                _usedSlotsByEdge.Remove(pair.Key);
        }

        _reservations.Remove(connectionId);
    }

    public void Clear()
    {
        _usedSlotsByEdge.Clear();
        _reservations.Clear();
    }
}
