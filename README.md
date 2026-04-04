# Core NetLite Unity

Thin Unity-facing layer over [Core.NetLite](https://github.com/Fur-Fighters-Frenzy/Core.NetLite) and [Core.NetLite.Simulation](https://github.com/Fur-Fighters-Frenzy/Core.NetLite.Simulation).

What is inside:
- NetLiteBootstrap for host / client / server startup
- optional NetLiteBootstrapPreset for default values
- runtime reconnect policy for clients
- runtime network simulation controls for debug latency / packet loss
- NetLiteDebugHud
- NetLiteSimulationBridge

The package does not try to hide NetLiteNode. Bootstrap exposes the node directly so game code can still work with the transport layer without fighting wrappers.
