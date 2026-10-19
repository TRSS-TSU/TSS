# Endpoint IP Address and Ping Validation Checkpoint

Purpose: validate that a connected desktop receives or accepts the correct IP settings, records those settings in session state, and reports successful or unsuccessful ping results. Complete this checkpoint before testing the AP uplink and laptop workflow.

Use the addressing values supplied by the active scenario. Do not assume that every VLAN uses DHCP or that every network uses the same gateway.

## 1 Prepare the topology

1. Load the active scenario and confirm the switch, router, demarc, desktop, and required patch-panel ports are present.
2. Place the desktop at its station.
3. Connect the desktop to switch `AN-1` `Fa0/2` with a straight-through cable.
4. Connect the switch uplink to the router with the required cable path.
5. Connect the router to the provider demarc with the required permanent cable path.
6. Open the switch CLI and record the starting result of:

```text
show vlan brief
show running-config
```

Expected starting condition: every unconfigured switch interface is shown as an access interface in VLAN 1, and the desktop-facing port is physically up after the cable is connected.

## 2 Configure the desktop access port

From the switch CLI, use the supported navigation path and either the full command or a supported alias:

```text
enable
configure terminal
interface Fa0/2
switchport mode access
switchport access vlan <endpoint-vlan>
no shutdown
end
show vlan brief
show running-config
```

Checkpoint:

- `Fa0/2` appears under the selected VLAN in `show vlan brief`.
- Other unconfigured access interfaces remain under VLAN 1.
- `show running-config` shows the configured mode and access VLAN under `interface Fa0/2`.
- The switch session state records the access VLAN and physical carrier state.

## 3 Configure the router path

Configure the router’s physical interface and the VLAN sub-interface required by the active scenario. The sub-interface number must match the VLAN number used for the endpoint.

Example pattern; use the scenario’s actual interface, VLAN, address, mask, and gateway values:

```text
enable
configure terminal
interface <router-physical-interface>
no shutdown
interface <router-physical-interface>.<endpoint-vlan>
encapsulation dot1q <endpoint-vlan>
ip address <router-subinterface-address> <mask>
no shutdown
end
show running-config
show ip interface brief
```

Checkpoint:

- The router accepts direct navigation from physical interface configuration into sub-interface configuration.
- The sub-interface is recorded with the matching 802.1Q VLAN number.
- The router’s physical path is up before testing Layer 3 behavior.

## 4 Static desktop address test

Use this test for a scenario network that requires a manually assigned address.

1. Open the desktop session.
2. Enter the scenario’s valid static IP address, subnet mask, default gateway, and DNS value.
3. Apply the settings.
4. Confirm the desktop reports the applied address.
5. Run `ipconfig` and confirm the displayed values match the applied settings.
6. Run a ping to the scenario’s router gateway.
7. Run a ping to a reachable server or provider target in the scenario.
8. Run a ping to an intentionally unreachable or incorrect address.

Expected results:

- Valid settings are accepted and recorded in the endpoint session state.
- The gateway ping succeeds only when Layer 1, Layer 2, the matching VLAN, the router sub-interface, and the address settings are correct.
- The reachable target ping succeeds when the scenario provides a complete path.
- The invalid or unreachable target ping fails with a clear diagnostic.
- A failed address apply does not overwrite the last committed valid settings.

## 5 Invalid static address tests

Repeat the apply operation with:

```text
999.999.999.999
```

Then test a valid-format address with the wrong subnet or gateway.

Expected results:

- The invalid IPv4 value is rejected with `Enter valid IPv4 settings.` or the current equivalent validation message.
- The wrong-network or wrong-gateway configuration is rejected or remains unable to reach Layer 3, according to the scenario’s validation rules.
- The UI and session state remain consistent after each failed attempt.

## 6 Dynamic desktop address test

Use this test only on a scenario network designated for DHCP.

1. Open the desktop session.
2. Select or enable DHCP.
3. Apply the setting.
4. Run `ipconfig`.
5. Record the assigned IP address, mask, gateway, and DNS values.
6. Ping the assigned gateway.
7. Ping a reachable scenario target.
8. Ping an unreachable or incorrect target.

Expected results:

- DHCP succeeds only after the endpoint has a usable physical path, the switch access VLAN is correct, the router sub-interface matches that VLAN, and the scenario permits DHCP on that network.
- The assigned values are written to the endpoint session state and reflected in the desktop UI.
- Successful and failed pings match the actual configured path, not just the presence of a cable.

## 7 Dynamic address failure test

Break one prerequisite at a time and repeat the DHCP apply operation:

- disconnect the desktop cable;
- place the switch port in the wrong VLAN;
- remove or disable the matching router sub-interface;
- use a VLAN that is not designated for DHCP.

Expected result: DHCP fails with a useful diagnostic, no false address is recorded as committed, and restoring the prerequisite allows a later DHCP attempt to succeed.

## 8 Session and UI consistency check

After each successful and failed operation, confirm:

- the desktop fields show the committed values;
- `ipconfig` shows the same values;
- the session state retains the same values after closing and reopening the desktop session;
- successful pings show success and failed pings show failure;
- the `TssDesktopSessionCanvas` can be closed with the Log Off / Disconnect button and with Escape;
- the runtime HUD controls remain exposed when the session canvas is open.

## 9 AP uplink and laptop follow-on test

Do not evaluate the AP browser until the desktop-side wired path is working.

1. Connect the AP uplink through the required permanent cable path.
2. Configure the switch AP-facing port as required by the scenario.
3. Configure the router sub-interface or routed path required by the AP network.
4. Verify the AP uplink condition.
5. Associate the laptop to the AP.
6. Repeat the static and DHCP address tests on the laptop.
7. Repeat successful gateway, reachable-target, and unreachable-target pings.

The AP result should distinguish a missing physical uplink from an incorrectly configured Layer 2 or Layer 3 path.
