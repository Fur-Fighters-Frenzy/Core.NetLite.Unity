using System;
using UnityEngine;
using Validosik.Core.NetLite;
using Validosik.Core.NetLite.Session;
using Validosik.Core.NetLite.Types;

namespace Validosik.Core.NetLite.Unity
{
    [AddComponentMenu("Validosik/NetLite/Bootstrap Startup")]
    [DisallowMultipleComponent]
    public sealed class NetLiteBootstrapStartup : MonoBehaviour
    {
        public string ConnectionKey = "netlite";
        public ulong SessionId;
        public byte AdvertisedPlayerId = PlayerId.None.Value;
        public int ListenPort = 9050;
        public int ClientListenPort;
        public int TickRate = 30;
        public int TickSyncInterval = 1;
        public bool EnableTickSystem = true;
        public bool EnableTickSync = true;
        public int TickSnapThreshold = 1;
        public int DisconnectTimeoutMs = 5000;
        public bool AllowPeerAddressChange;

        public void Apply(NetLiteStartupConfig other)
        {
            if (other == null)
            {
                return;
            }

            ConnectionKey = other.ConnectionKey;
            SessionId = other.SessionId;
            AdvertisedPlayerId = other.AdvertisedPlayerId;
            ListenPort = other.ListenPort;
            ClientListenPort = other.ClientListenPort;
            TickRate = other.TickRate;
            TickSyncInterval = other.TickSyncInterval;
            EnableTickSystem = other.EnableTickSystem;
            EnableTickSync = other.EnableTickSync;
            TickSnapThreshold = other.TickSnapThreshold;
            DisconnectTimeoutMs = other.DisconnectTimeoutMs;
            AllowPeerAddressChange = other.AllowPeerAddressChange;
        }

        public NetLiteOptions ToOptions(NetLiteBootstrapRuntimeDebug runtimeDebug, NetLiteBootstrapReconnect reconnect)
        {
            runtimeDebug ??= GetComponent<NetLiteBootstrapRuntimeDebug>();
            reconnect ??= GetComponent<NetLiteBootstrapReconnect>();

            return new NetLiteOptions
            {
                ConnectionKey = ConnectionKey ?? string.Empty,
                SessionId = SessionId,
                AdvertisedPlayerId = new PlayerId(AdvertisedPlayerId),
                TickRate = Math.Max(1, TickRate),
                TickSyncInterval = Math.Max(1, TickSyncInterval),
                EnableTickSystem = EnableTickSystem,
                EnableTickSync = EnableTickSync,
                TickSnapThreshold = Math.Max(0, TickSnapThreshold),
                DisconnectTimeoutMs = Math.Max(1000, DisconnectTimeoutMs),
                ReconnectDelayMs = reconnect != null ? Math.Max(0, reconnect.TransportReconnectDelayMs) : 500,
                MaxConnectAttempts = reconnect != null ? Math.Max(1, reconnect.TransportMaxConnectAttempts) : 10,
                AllowPeerAddressChange = AllowPeerAddressChange,
                SimulateLatency = runtimeDebug != null && runtimeDebug.SimulateLatency,
                SimulationMinLatencyMs = runtimeDebug != null ? Math.Max(0, runtimeDebug.MinLatencyMs) : 30,
                SimulationMaxLatencyMs = runtimeDebug != null
                    ? Math.Max(Math.Max(0, runtimeDebug.MinLatencyMs), runtimeDebug.MaxLatencyMs)
                    : 100,
                SimulatePacketLoss = runtimeDebug != null && runtimeDebug.SimulatePacketLoss,
                SimulationPacketLossChancePercent = runtimeDebug != null
                    ? Mathf.Clamp(runtimeDebug.PacketLossPercent, 0, 100)
                    : 10
            };
        }

        public void ApplyTo(NetLiteNode node)
        {
            if (node == null)
            {
                return;
            }

            node.Options.DisconnectTimeoutMs = Math.Max(1000, DisconnectTimeoutMs);
            node.Options.AllowPeerAddressChange = AllowPeerAddressChange;
        }
    }
}
