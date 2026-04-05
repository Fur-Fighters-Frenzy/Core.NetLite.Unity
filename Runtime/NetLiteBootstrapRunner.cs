using UnityEngine;

namespace Validosik.Core.NetLite.Unity
{
    [AddComponentMenu("Validosik/NetLite/Bootstrap Runner")]
    [DisallowMultipleComponent]
    public sealed class NetLiteBootstrapRunner : MonoBehaviour
    {
        public NetworkRole StartRole = NetworkRole.Host;
        public bool AutoUpdateNode = true;
        public bool UseUnscaledTime = true;

        public bool ShouldAutoStart => EffectiveStartRole != NetworkRole.None;

        public float GetDeltaTime() => UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
