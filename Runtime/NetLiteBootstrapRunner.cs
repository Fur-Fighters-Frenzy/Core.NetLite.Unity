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

        public NetworkRole EffectiveStartRole =>
            StartRole == NetworkRole.Client
                ? NetworkRole.Client
                : StartRole == NetworkRole.None
                    ? NetworkRole.None
                    : NetworkRole.Host;

        public bool ShouldAutoStart => EffectiveStartRole != NetworkRole.None;

        public float GetDeltaTime() => UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        private void OnValidate() => StartRole = EffectiveStartRole;
    }
}
