# Completion Plan: Deterministic L1/L2/L3 Reachability

## Objective

Finish `TssNetworkReachabilityEvaluator` as the only decision source for endpoint ping/tracert, monitor status, LEDs, printer outcomes, and endpoint evidence.

## Dependencies

`01-network-state-and-cli.md` must be complete because the evaluator consumes configured device/interface/endpoint state.

## Implementation

1. Resolve source/destination by stable role and profile address, never display name.
2. Evaluate Layer 1 first: data endpoint exists, effective physical path exists, carrier is present, and every participating interface is administratively up. Router subinterfaces inherit parent state.
3. Evaluate Layer 2 second: active source/destination access VLAN, access-port assignment, every required trunk on both ends, allowed VLAN carriage, matching native VLAN, and suspended Dead VLAN behavior.
4. Evaluate Layer 3 last: endpoint address/mask, matching gateway subinterface/dot1q/mask, return path, operational WAN `/30`, and static default route to provider.
5. Return success, first failed layer, neutral diagnostic, source/destination roles, and reached hops. Do not disclose the correct VLAN, address, or command.

## Acceptance

- Missing cable and shut interface fail Layer 1.
- Access VLAN, allowed-list, native-VLAN, and suspended-VLAN faults fail Layer 2.
- Gateway, dot1q, mask, router parent, WAN, and default-route faults fail Layer 3.
- Unknown valid targets time out without simulated internet behavior.
- Evaluator tests cover each fault independently and the first-failure rule.
