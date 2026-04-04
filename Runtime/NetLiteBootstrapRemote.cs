using UnityEngine;
using Validosik.Core.NetLite.Types;

namespace Validosik.Core.NetLite.Unity
{
    [AddComponentMenu("Validosik/NetLite/Bootstrap Remote")]
    [DisallowMultipleComponent]
    public sealed class NetLiteBootstrapRemote : MonoBehaviour
    {
        public string Host = "127.0.0.1";
        public int Port = 9050;
        public byte RequestedPlayerId = PlayerId.None.Value;
        public bool UseStoredReconnectIdentity = true;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && Port > 0;

        public void Apply(NetLiteRemoteEndpointConfig other)
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
}
