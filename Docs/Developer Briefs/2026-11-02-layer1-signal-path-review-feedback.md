# Review Feedback — Layer-1 Signal Path vs. Schedule Compliance

**Re:** commit `644265563` "Separate Layer 1 signal paths from schedule compliance"
**Verdict:** Very close. The core design is implemented correctly. One blocking
regression needs a second pass before this can merge.

---

## What is correct — please do not change these

The signal/schedule split landed well. The following are verified and should be
left as-is:

- **Parity rule.** `HasSignalParity` computes
  `crossoverParity == (IsSwitchEndpoint(a) == IsSwitchEndpoint(b))`, which is the
  correct MDI/MDI-X rule: **even** parity when exactly one endpoint is a switch,
  **odd** otherwise. Switches resolve as MDI-X; routers, hosts, APs, and unknown
  endpoints fall through to MDI. This correctly covers desktop (host↔switch)
  runs, not just the inter-closet switch link.
- **Parity-aware traversal.** `HasSignalPath` carries
  `(endpoint, usedPermanent, crossoverParity)` in the BFS key, XORs crossover
  across every segment (player cords and permanent runs alike), and rejects
  `Rollover` edges. `HasDirectSignalConnection` covers the no-permanent direct
  case. Termination gating is inherited correctly because unterminated permanent
  runs never enter `_physicalConnections`.
- **The three new EditMode tests** (schedule-mismatched switch link,
  rejected even-parity switch pair, carrier-survives-shutdown) are logically
  sound and should pass.
- **Panel relabel** to `[Schedule match]` / `[Schedule mismatch]` is correct.

---

## Blocking issue — the router WAN interface (`Fa0/1`) loses carrier

### What happens

The commit removed the router-`Fa0/1` carrier special-case in **both** carrier
assignments:

- `TssTrainingSession.cs:175` (`RefreshInterfaceCarriers`)
- `TssDeviceSessionController.cs:277` (`OpenTerminalSession`)

Both now set carrier purely from "endpoint is on an effective physical path."
That is correct for every port **except** the router WAN port, which connects to
the provider demarc through a *permanent* segment.

### Why the effective-path logic cannot see the WAN link

The effective-path cache is built by pairing **leaves** — player endpoints that
are not configured permanent endpoints. Trace the WAN chain:

```text
Router Fa0/1  --player cable-->  PatchPanel:Rack01:Port18  --permanent (Infrastructure043)-->  Demarc:Provider:Port01
```

- `PatchPanel:Rack01:Port18` is a configured permanent endpoint → excluded from leaves.
- `Demarc:Provider:Port01` is a permanent endpoint → excluded from leaves.
- Router `Fa0/1` is therefore the **only** leaf on this chain.

A single leaf pairs with nothing, so `GetEffectivePhysicalPaths()` produces no
path containing router `Fa0/1`, its endpoint is absent from
`_effectiveEndpointCache`, and `CarrierUp` is set to `false` — even when the WAN
is cabled correctly all the way to the demarc.

The special-case you removed existed precisely because the generic leaf-pair
logic structurally cannot represent the WAN-to-demarc link. The brief asked you
to stop letting `IsProviderDemarcConnected(...)` **suppress** carrier on an
otherwise-valid path; it did not ask you to remove it as a carrier source. The
intended change is *additive* (OR), not a deletion.

### It breaks an existing test

`TssPlacementEvaluatorTests.cs:570`
`ProviderDemarcPathThroughPermanentInfrastructureSetsWanCarrier` cables
router → patch → demarc and asserts
`router.Interfaces["Fa0/1"].CarrierUp` after `NotifyNetworkStateChanged()`. With
only one leaf, the effective path set is empty and that assertion now fails. The
commit left this test untouched, so the suite should currently be red. Run the
EditMode tests to confirm.

---

## Suggested correction

Keep the demarc as an **additional** carrier source for the router `Fa0/1` port,
rather than deleting it. In `RefreshInterfaceCarriers` (`TssTrainingSession.cs`):

```csharp
iface.CarrierUp = effectiveEndpoints.Contains(iface.EndpointId)
    || (device.Category == EquipmentCategory.Router && iface.Name == "Fa0/1"
        && IsProviderDemarcConnected(iface.EndpointId));
```

Apply the same OR in `TssDeviceSessionController.cs` (`OpenTerminalSession`),
using the session's `ctx.Category` / interface name in place of `device`.

This yields the behavior the brief intended:

- `Fa0/1` gets carrier from a valid physical path **or** a valid demarc path.
- Demarc *service* availability stays on its own authority
  (`SetProviderDemarcConnected` / `IsProviderDemarcConnected`) and is still not
  granted by an arbitrary router connection.
- The failing WAN test passes again, and the three new parity tests continue to
  pass.

### Please also add a regression test

Add a case asserting that router `Fa0/1` carrier is **false** when the WAN is
*not* connected to the demarc (no player cable to `PatchPanel:…:Port18`), so the
OR cannot silently grant carrier. Together with the existing
`ProviderDemarcPathThroughPermanentInfrastructureSetsWanCarrier`, that pins both
directions.

---

## Summary for the next pass

1. Restore the demarc as an additive carrier source for router `Fa0/1` in both
   `TssTrainingSession.cs` and `TssDeviceSessionController.cs` (the OR above).
2. Confirm `ProviderDemarcPathThroughPermanentInfrastructureSetsWanCarrier`
   passes again.
3. Add the negative WAN-carrier test.
4. Leave the parity logic, the signal-path traversal, and the three new tests
   unchanged — they are correct.
