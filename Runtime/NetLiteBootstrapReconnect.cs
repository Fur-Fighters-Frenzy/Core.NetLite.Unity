using System;
using UnityEngine;
using Validosik.Core.NetLite;

namespace Validosik.Core.NetLite.Unity
{
    [AddComponentMenu("Validosik/NetLite/Bootstrap Reconnect")]
    [DisallowMultipleComponent]
    public sealed class NetLiteBootstrapReconnect : MonoBehaviour
    {
        public bool Enabled = true;
        public float InitialDelaySeconds = 0.5f;
        public float RetryDelaySeconds = 1f;
        public int MaxReconnectCycles;
        public int TransportReconnectDelayMs = 500;
        public int TransportMaxConnectAttempts = 10;

        public void Apply(NetLiteReconnectConfig other)
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

        public void ApplyTo(NetLiteNode node)
        {
            if (node == null)
            {
                return;
            }

            node.Options.ReconnectDelayMs = Math.Max(0, TransportReconnectDelayMs);
            node.Options.MaxConnectAttempts = Math.Max(1, TransportMaxConnectAttempts);
        }
    }
}
