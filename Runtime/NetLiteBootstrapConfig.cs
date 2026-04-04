using System;
using UnityEngine;
using Validosik.Core.NetLite.Session;
using Validosik.Core.NetLite.Types;

namespace Validosik.Core.NetLite.Unity
{
    [Serializable]
    public sealed class NetLiteStartupConfig
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
        public int ReconnectDelayMs = 500;
        public int MaxConnectAttempts = 10;
        public bool AllowPeerAddressChange;

        public void CopyFrom(NetLiteStartupConfig other)
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
            ReconnectDelayMs = other.ReconnectDelayMs;
            MaxConnectAttempts = other.MaxConnectAttempts;
            AllowPeerAddressChange = other.AllowPeerAddressChange;
        }

        internal NetLiteOptions ToOptions(NetLiteRuntimeDebugConfig runtimeDebug)
        {
            runtimeDebug ??= new NetLiteRuntimeDebugConfig();
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
                ReconnectDelayMs = Math.Max(0, ReconnectDelayMs),
                MaxConnectAttempts = Math.Max(1, MaxConnectAttempts),
                AllowPeerAddressChange = AllowPeerAddressChange,
                SimulateLatency = runtimeDebug.SimulateLatency,
                SimulationMinLatencyMs = Math.Max(0, runtimeDebug.MinLatencyMs),
                SimulationMaxLatencyMs = Math.Max(Math.Max(0, runtimeDebug.MinLatencyMs), runtimeDebug.MaxLatencyMs),
                SimulatePacketLoss = runtimeDebug.SimulatePacketLoss,
                SimulationPacketLossChancePercent = Mathf.Clamp(runtimeDebug.PacketLossPercent, 0, 100)
            };
        }
    }

    [Serializable]
    public sealed class NetLiteRemoteEndpointConfig
    {
        public string Host = "127.0.0.1";
        public int Port = 9050;
        public byte RequestedPlayerId = PlayerId.None.Value;
        public bool UseStoredReconnectIdentity = true;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && Port > 0;

        public void CopyFrom(NetLiteRemoteEndpointConfig other)
        {
            if (other == null)
            {
                return;
            }

            Host = other.Host;
            Port = other.Port;
            RequestedPlayerId = other.RequestedPlayerId;
            UseStoredReconnectIdentity = other.UseStoredReconnectIdentity;
        }
    }

    [Serializable]
    public sealed class NetLiteRuntimeDebugConfig
    {
        public bool SimulateLatency;
        public int MinLatencyMs = 30;
        public int MaxLatencyMs = 100;
        public bool SimulatePacketLoss;
        public int PacketLossPercent = 10;

        public void CopyFrom(NetLiteRuntimeDebugConfig other)
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
    }

    [Serializable]
    public sealed class NetLiteReconnectConfig
    {
        public bool Enabled = true;
        public float InitialDelaySeconds = 0.5f;
        public float RetryDelaySeconds = 1f;
        public int MaxReconnectCycles;

        public void CopyFrom(NetLiteReconnectConfig other)
        {
            if (other == null)
            {
                return;
            }

            Enabled = other.Enabled;
            InitialDelaySeconds = other.InitialDelaySeconds;
            RetryDelaySeconds = other.RetryDelaySeconds;
            MaxReconnectCycles = other.MaxReconnectCycles;
        }
    }
}
