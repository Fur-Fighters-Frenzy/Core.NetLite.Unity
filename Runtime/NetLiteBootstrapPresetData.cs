using System;
using UnityEngine;
using UnityEngine.Serialization;
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
        public bool AllowPeerAddressChange;

        [HideInInspector, FormerlySerializedAs("ReconnectDelayMs")]
        public int LegacyReconnectDelayMs = 500;

        [HideInInspector, FormerlySerializedAs("MaxConnectAttempts")]
        public int LegacyMaxConnectAttempts = 10;

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
            AllowPeerAddressChange = other.AllowPeerAddressChange;
            LegacyReconnectDelayMs = other.LegacyReconnectDelayMs;
            LegacyMaxConnectAttempts = other.LegacyMaxConnectAttempts;
        }

        internal bool TryGetLegacyReconnectOverrides(out int reconnectDelayMs, out int maxConnectAttempts)
        {
            reconnectDelayMs = Math.Max(0, LegacyReconnectDelayMs);
            maxConnectAttempts = Math.Max(1, LegacyMaxConnectAttempts);
            return LegacyReconnectDelayMs != 500 || LegacyMaxConnectAttempts != 10;
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
        public int TransportReconnectDelayMs = 500;
        public int TransportMaxConnectAttempts = 10;

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
            TransportReconnectDelayMs = other.TransportReconnectDelayMs;
            TransportMaxConnectAttempts = other.TransportMaxConnectAttempts;
        }
    }
}
