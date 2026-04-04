using UnityEngine;
using Validosik.Core.NetLite;

namespace Validosik.Core.NetLite.Unity
{
    public sealed class NetLiteLagSimulation : MonoBehaviour
    {
        [SerializeField] private NetLiteBootstrap _bootstrap;
        [SerializeField] private bool _autoFindBootstrap = true;
        [SerializeField] private bool _applyOnEnable = true;
        [SerializeField] private NetLiteRuntimeDebugConfig _config = new();

        public NetLiteRuntimeDebugConfig Config => _config;

        private void OnEnable()
        {
            ResolveBootstrap();
            if (_bootstrap != null)
            {
                _bootstrap.OnNodeCreated += HandleNodeCreated;
            }

            if (_applyOnEnable)
            {
                Apply();
            }
        }

        private void OnDisable()
        {
            if (_bootstrap != null)
            {
                _bootstrap.OnNodeCreated -= HandleNodeCreated;
            }
        }

        public void Apply()
        {
            ResolveBootstrap();
            if (_bootstrap != null)
            {
                _bootstrap.SetRuntimeDebugConfig(_config);
            }
        }

        public void DisableSimulation()
        {
            _config.SimulateLatency = false;
            _config.SimulatePacketLoss = false;
            Apply();
        }

        private void HandleNodeCreated(NetLiteNode node) => Apply();

        private void ResolveBootstrap()
        {
            if (_bootstrap == null && _autoFindBootstrap)
            {
                _bootstrap = GetComponent<NetLiteBootstrap>() ?? FindObjectOfType<NetLiteBootstrap>();
            }
        }
    }
}
