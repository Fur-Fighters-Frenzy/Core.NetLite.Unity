# Core NetLite Unity

Thin Unity-facing layer over [Core.NetLite](https://github.com/Fur-Fighters-Frenzy/Core.NetLite) and [Core.NetLite.Simulation](https://github.com/Fur-Fighters-Frenzy/Core.NetLite.Simulation).

What is inside:
- NetLiteBootstrap coordinator plus sibling config components
- NetLiteBootstrapRunner for auto-start and node update behaviour
- NetLiteBootstrapStartup / Remote / RuntimeDebug / Reconnect
- optional NetLiteBootstrapPreset for default values
- runtime reconnect policy for clients
- runtime network simulation controls for debug latency / packet loss
- NetLiteDebugHud overlay canvas with `F3` toggle
- NetLiteSimulationBridge

The package does not try to hide NetLiteNode. Bootstrap exposes the node directly so game code can still work with the transport layer without fighting wrappers.

`NetLiteBootstrap` now keeps only orchestration logic. If sibling config components are missing on the same GameObject, it creates them automatically with default values and then uses their settings.

If a preset is assigned, bootstrap applies it on `Awake` without an extra toggle. If a dependent component has no bootstrap reference assigned, it resolves it automatically.

`NetLiteDebugHud` no longer depends on IMGUI or sample presenter scripts. It creates its own screen-space overlay canvas, renders the current runtime state there, and toggles visibility with `F3`.
