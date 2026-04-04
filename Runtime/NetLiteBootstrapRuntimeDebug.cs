using UnityEngine;
using Validosik.Core.NetLite;

namespace Validosik.Core.NetLite.Unity
{
    [AddComponentMenu("Validosik/NetLite/Bootstrap Runtime Debug")]
    [DisallowMultipleComponent]
    public sealed class NetLiteBootstrapRuntimeDebug : MonoBehaviour
    {
        public bool SimulateLatency;
        public int MinLatencyMs = 30;
        public int MaxLatencyMs = 100;
        public bool SimulatePacketLoss;
        public int PacketLossPercent = 10;

        public void Apply(NetLiteRuntimeDebugConfig other)
        {
            if (other == null)
            {
                return;
            }

            SimulateLatency = other.SimulateLatency;
            MinLatencyMs = other.MinLatencyMs;
            MaxLatencyMs = other.MaxLatencyMs;
            SimulatePacketLoss = other.SimulatePacketLoss;
            PacketLossPercent = other.PacketLossPercent;
        }

        public void ApplyTo(NetLiteNode node)
        {
            if (node == null)
            {
                return;
            }

            node.ApplyNetworkSimulation(
                SimulateLatency,
                MinLatencyMs,
                MaxLatencyMs,
                SimulatePacketLoss,
                PacketLossPercent);
        }
    }
}
