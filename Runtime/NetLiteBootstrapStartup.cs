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
        public bool EnablePeerToPeer;
        public bool EnableNatPunch = true;
        public bool EnablePeerRelayFallback = true;
        public int PeerDirectConnectTimeoutMs = 1500;
        public int PeerDirectRetryIntervalMs = 3000;
        public int NatPunchRequestIntervalMs = 250;

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
            EnablePeerToPeer = other.EnablePeerToPeer;
            EnableNatPunch = other.EnableNatPunch;
            EnablePeerRelayFallback = other.EnablePeerRelayFallback;
            PeerDirectConnectTimeoutMs = other.PeerDirectConnectTimeoutMs;
            PeerDirectRetryIntervalMs = other.PeerDirectRetryIntervalMs;
            NatPunchRequestIntervalMs = other.NatPunchRequestIntervalMs;
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
                EnablePeerToPeer = EnablePeerToPeer,
                EnableNatPunch = EnableNatPunch,
                EnablePeerRelayFallback = EnablePeerRelayFallback,
                PeerDirectConnectTimeoutMs = Math.Max(250, PeerDirectConnectTimeoutMs),
                PeerDirectRetryIntervalMs = Math.Max(250, PeerDirectRetryIntervalMs),
                NatPunchRequestIntervalMs = Math.Max(50, NatPunchRequestIntervalMs),
                SimulateLatency = runtimeDebug != null && runtimeDebug.EffectiveSimulateLatency,
                SimulationMinLatencyMs = runtimeDebug != null ? Math.Max(0, runtimeDebug.EffectiveMinLatencyMs) : 30,
                SimulationMaxLatencyMs = runtimeDebug != null
                    ? Math.Max(Math.Max(0, runtimeDebug.EffectiveMinLatencyMs), runtimeDebug.EffectiveMaxLatencyMs)
                    : 100,
                SimulatePacketLoss = runtimeDebug != null && runtimeDebug.EffectiveSimulatePacketLoss,
                SimulationPacketLossChancePercent = runtimeDebug != null
                    ? runtimeDebug.EffectivePacketLossPercent
                    : 0
            };
        }

        public void ApplyTo(NetLiteNode node)
        {
            if (node == null)
            {
                return;
            }

            if (node.IsTickAuthority || node.AuthorityPlayerId == PlayerId.None)
            {
                node.Options.DisconnectTimeoutMs = Math.Max(1000, DisconnectTimeoutMs);
                node.Options.AllowPeerAddressChange = AllowPeerAddressChange;
                node.Options.TickRate = Math.Max(1, TickRate);
                node.Options.TickSyncInterval = Math.Max(1, TickSyncInterval);
                node.Options.EnableTickSystem = EnableTickSystem;
                node.Options.EnableTickSync = EnableTickSync;
                node.Options.TickSnapThreshold = Math.Max(0, TickSnapThreshold);
                node.Options.EnablePeerToPeer = EnablePeerToPeer;
                node.Options.EnableNatPunch = EnableNatPunch;
                node.Options.EnablePeerRelayFallback = EnablePeerRelayFallback;
                node.Options.PeerDirectConnectTimeoutMs = Math.Max(250, PeerDirectConnectTimeoutMs);
                node.Options.PeerDirectRetryIntervalMs = Math.Max(250, PeerDirectRetryIntervalMs);
                node.Options.NatPunchRequestIntervalMs = Math.Max(50, NatPunchRequestIntervalMs);
            }
        }
    }
}
