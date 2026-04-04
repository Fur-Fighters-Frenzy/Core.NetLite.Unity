using System;
using System.Collections.Generic;
using UnityEngine;
using Validosik.Core.NetLite;
using Validosik.Core.NetLite.Session;
using Validosik.Core.NetLite.Simulation;

namespace Validosik.Core.NetLite.Unity
{
    public sealed class NetLiteSimulationBridge : MonoBehaviour
    {
        [SerializeField] private NetLiteBootstrap _bootstrap;
        [SerializeField] private bool _autoDiscoverSystems = true;
        [SerializeField] private bool _includeInactiveChildren = true;
        [SerializeField] private bool _rebuildOnSessionEstablished = true;
        [SerializeField] private MonoBehaviour[] _systems = Array.Empty<MonoBehaviour>();

        private NetLiteNode _boundNode;
        private readonly List<object> _registeredSystems = new();

        public NetLiteSimulationRunner Runner { get; private set; }

        private void OnEnable()
        {
            ResolveBootstrap();
            BindBootstrap();
            if (_bootstrap != null)
            {
                RebuildRunner(_bootstrap.Node != null ? _bootstrap.Node.Tick : (ushort)0);
            }
        }

        private void OnDisable()
        {
            UnbindNode();
            UnbindBootstrap();
            Runner?.Clear();
            Runner = null;
            _registeredSystems.Clear();
        }

        public void RebuildRunner() => RebuildRunner(_bootstrap != null && _bootstrap.Node != null ? _bootstrap.Node.Tick : (ushort)0);

        public void Register(object system)
        {
            if (system == null)
            {
                return;
            }

            Runner?.Register(system);
            if (!_registeredSystems.Contains(system))
            {
                _registeredSystems.Add(system);
            }
        }

        public void Unregister(object system)
        {
            if (system == null)
            {
                return;
            }

            Runner?.Unregister(system);
            _registeredSystems.Remove(system);
        }

        private void BindBootstrap()
        {
            if (_bootstrap == null)
            {
                return;
            }

            _bootstrap.OnNodeCreated += HandleNodeCreated;
            _bootstrap.OnNodeStopped += HandleNodeStopped;
            _bootstrap.OnSessionEstablished += HandleSessionEstablished;
        }

        private void UnbindBootstrap()
        {
            if (_bootstrap == null)
            {
                return;
            }

            _bootstrap.OnNodeCreated -= HandleNodeCreated;
            _bootstrap.OnNodeStopped -= HandleNodeStopped;
            _bootstrap.OnSessionEstablished -= HandleSessionEstablished;
        }

        private void HandleNodeCreated(NetLiteNode node)
        {
            BindNode(node);
            RebuildRunner(node.Tick);
        }

        private void HandleNodeStopped()
        {
            UnbindNode();
            Runner?.Clear();
            Runner = null;
        }

        private void HandleSessionEstablished(NetLiteSessionEstablishedInfo info)
        {
            if (_rebuildOnSessionEstablished)
            {
                RebuildRunner(info.InitialTick);
            }
        }

        private void HandleTick(ushort tick) => Runner?.ExecuteTick(tick);

        private void RebuildRunner(ushort initialTick)
        {
            ResolveBootstrap();
            if (_bootstrap == null)
            {
                return;
            }

            BindNode(_bootstrap.Node);
            Runner?.Clear();
            Runner = new NetLiteSimulationRunner(GetFixedDelta(), initialTick);
            _registeredSystems.Clear();

            var systems = CollectSystems();
            for (var i = 0; i < systems.Count; ++i)
            {
                Register(systems[i]);
            }
        }

        private float GetFixedDelta()
        {
            var tickRate = _bootstrap != null ? Mathf.Max(1, _bootstrap.Startup.TickRate) : 30;
            return 1f / tickRate;
        }

        private void BindNode(NetLiteNode node)
        {
            if (ReferenceEquals(_boundNode, node))
            {
                return;
            }

            UnbindNode();
            _boundNode = node;
            if (_boundNode != null)
            {
                _boundNode.OnTick += HandleTick;
            }
        }

        private void UnbindNode()
        {
            if (_boundNode != null)
            {
                _boundNode.OnTick -= HandleTick;
                _boundNode = null;
            }
        }

        private List<object> CollectSystems()
        {
            var result = new List<object>();

            if (_systems != null)
            {
                for (var i = 0; i < _systems.Length; ++i)
                {
                    TryAddSystem(result, _systems[i]);
                }
            }

            if (_autoDiscoverSystems)
            {
                var discovered = GetComponentsInChildren<MonoBehaviour>(_includeInactiveChildren);
                for (var i = 0; i < discovered.Length; ++i)
                {
                    TryAddSystem(result, discovered[i]);
                }
            }

            return result;
        }

        private static void TryAddSystem(List<object> systems, MonoBehaviour candidate)
        {
            if (candidate == null
                || candidate is NetLiteSimulationBridge
                || systems.Contains(candidate)
                || candidate is not ISimulationSystem
                    && candidate is not IRollbackSystem
                    && candidate is not ISimulationPostTickSync)
            {
                return;
            }

            systems.Add(candidate);
        }

        private void ResolveBootstrap()
        {
            if (_bootstrap == null)
            {
                _bootstrap = GetComponent<NetLiteBootstrap>() ?? FindObjectOfType<NetLiteBootstrap>();
            }
        }
    }
}
