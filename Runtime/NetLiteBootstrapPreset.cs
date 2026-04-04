using UnityEngine;

namespace Validosik.Core.NetLite.Unity
{
    [CreateAssetMenu(menuName = "Validosik/NetLite/Bootstrap Preset", fileName = "NetLiteBootstrapPreset")]
    public sealed class NetLiteBootstrapPreset : ScriptableObject
    {
        public NetLiteStartupConfig Startup = new();
        public NetLiteRemoteEndpointConfig Remote = new();
        public NetLiteRuntimeDebugConfig RuntimeDebug = new();
        public NetLiteReconnectConfig Reconnect = new();
    }
}
