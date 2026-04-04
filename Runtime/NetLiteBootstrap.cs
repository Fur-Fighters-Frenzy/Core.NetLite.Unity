using System;
using LiteNetLib;
using UnityEngine;
using Validosik.Core.NetLite;
using Validosik.Core.NetLite.Session;
using Validosik.Core.NetLite.Stats;
using Validosik.Core.NetLite.Types;

namespace Validosik.Core.NetLite.Unity
{
    public sealed class NetLiteBootstrap : MonoBehaviour
    {
        [SerializeField] private NetLiteBootstrapPreset _preset;
        [SerializeField] private bool _applyPresetOnAwake = true;
        [SerializeField] private bool _autoStart = true;
        [SerializeField] private NetworkRole _autoStartRole = NetworkRole.Host;
        [SerializeField] private bool _autoUpdateNode = true;
        [SerializeField] private bool _useUnscaledTime = true;
        [SerializeField] private NetLiteStartupConfig _startup = new();
        [SerializeField] private NetLiteRemoteEndpointConfig _remote = new();
        [SerializeField] private NetLiteRuntimeDebugConfig _runtimeDebug = new();
        [SerializeField] private NetLiteReconnectConfig _reconnect = new();

        private Func<NetLiteConnectionApprovalContext, NetLiteConnectionApprovalResult> _connectionApproval;
        private float _nextReconnectAt = -1f;
        private int _reconnectCyclesStarted;
        private bool _sessionEstablished;
        private bool _stopping;

        public event Action<NetLiteNode> OnNodeCreated;
        public event Action OnNodeStopped;
        public event Action<NetworkRole> OnRoleChanged;
        public event Action<NetLiteSessionEstablishedInfo> OnSessionEstablished;
        public event Action<PlayerId> OnPeerConnected;
        public event Action<PlayerId> OnPeerDisconnected;
        public event Action<PlayerId, NetLitePeerMetrics> OnPeerMetricsUpdated;
        public event Action<NetLiteReconnectState> OnReconnectStateChanged;

        public NetLiteNode Node { get; private set; }
        public NetworkRole Role { get; private set; }
        public NetLiteReconnectState ReconnectState { get; private set; } = NetLiteReconnectState.Disabled;
        public int ReconnectCyclesStarted => _reconnectCyclesStarted;
        public NetLiteStartupConfig Startup => _startup;
        public NetLiteRemoteEndpointConfig Remote => _remote;
        public NetLiteRuntimeDebugConfig RuntimeDebug => _runtimeDebug;
        public NetLiteReconnectConfig Reconnect => _reconnect;
        public bool IsRunning => Node != null && Node.IsRunning;

        public Func<NetLiteConnectionApprovalContext, NetLiteConnectionApprovalResult> ConnectionApproval
        {
            get => _connectionApproval;
            set
            {
                _connectionApproval = value;
                if (Node != null)
                {
                    Node.ConnectionApproval = value;
                }
            }
        }

        private void Awake()
        {
            EnsureConfigs();
            if (_applyPresetOnAwake)
            {
                ApplyPreset();
            }
        }

        private void Start()
        {
            if (_autoStart && _autoStartRole != NetworkRole.None)
            {
                StartRole(_autoStartRole);
            }
        }

        private void Update()
        {
            if (_autoUpdateNode && Node != null)
            {
                var deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                Node.Update(deltaTime);
            }

            if (Role == NetworkRole.Client)
            {
                UpdateReconnectLoop();
            }
        }

        private void OnDestroy() => Stop();

        public void ApplyPreset()
        {
            EnsureConfigs();
            if (_preset == null)
            {
                return;
            }

            _startup.CopyFrom(_preset.Startup);
            _remote.CopyFrom(_preset.Remote);
            _runtimeDebug.CopyFrom(_preset.RuntimeDebug);
            _reconnect.CopyFrom(_preset.Reconnect);
        }

        public bool StartRole(NetworkRole role)
        {
            return role switch
            {
                NetworkRole.Host => StartAuthorityRole(NetworkRole.Host),
                NetworkRole.Server => StartAuthorityRole(NetworkRole.Server),
                NetworkRole.Client => StartClient(),
                _ => false
            };
        }

        public bool StartHost() => StartAuthorityRole(NetworkRole.Host);

        public bool StartServer() => StartAuthorityRole(NetworkRole.Server);

        public bool StartClient()
        {
            if (!PrepareNode(NetworkRole.Client, false, Math.Max(0, _startup.ClientListenPort)))
            {
                return false;
            }

            ResetReconnectTracking();
            if (!_reconnect.Enabled)
            {
                SetReconnectState(NetLiteReconnectState.Disabled);
            }

            return TryConnectCycle();
        }

        public bool ConnectNow()
        {
            if (Role != NetworkRole.Client || Node == null || !Node.IsRunning)
            {
                return false;
            }

            ResetReconnectTracking();
            return TryConnectCycle();
        }

        public bool ReconnectNow()
        {
            if (Role != NetworkRole.Client || Node == null || !Node.IsRunning)
            {
                return false;
            }

            ScheduleReconnect(0f);
            return true;
        }

        public void Stop()
        {
            _stopping = true;
            try
            {
                _nextReconnectAt = -1f;
                _sessionEstablished = false;
                _reconnectCyclesStarted = 0;
                SetReconnectState(NetLiteReconnectState.Disabled);

                if (Node != null)
                {
                    var node = Node;
                    UnsubscribeNode(node);
                    Node = null;
                    node.Dispose();
                    OnNodeStopped?.Invoke();
                }

                if (Role != NetworkRole.None)
                {
                    Role = NetworkRole.None;
                    OnRoleChanged?.Invoke(Role);
                }
            }
            finally
            {
                _stopping = false;
            }
        }

        public void ManualUpdateNode(float deltaTime)
        {
            if (Node != null)
            {
                Node.Update(deltaTime);
            }
        }

        public void SetRemoteEndpoint(string host, int port)
        {
            _remote.Host = host;
            _remote.Port = port;
        }

        public void SetRuntimeDebugConfig(NetLiteRuntimeDebugConfig config)
        {
            EnsureConfigs();
            _runtimeDebug.CopyFrom(config);
            ApplyRuntimeDebugConfig();
        }

        public void SetLatencySimulation(bool enabled, int minLatencyMs, int maxLatencyMs)
        {
            _runtimeDebug.SimulateLatency = enabled;
            _runtimeDebug.MinLatencyMs = minLatencyMs;
            _runtimeDebug.MaxLatencyMs = maxLatencyMs;
            ApplyRuntimeDebugConfig();
        }

        public void SetPacketLossSimulation(bool enabled, int packetLossPercent)
        {
            _runtimeDebug.SimulatePacketLoss = enabled;
            _runtimeDebug.PacketLossPercent = packetLossPercent;
            ApplyRuntimeDebugConfig();
        }

        public bool TryGetAuthorityMetrics(out NetLitePeerMetrics metrics)
        {
            metrics = default;
            return Node != null
                && Node.AuthorityPlayerId != PlayerId.None
                && Node.TryGetMetrics(Node.AuthorityPlayerId, out metrics);
        }

        private bool StartAuthorityRole(NetworkRole role)
        {
            ResetReconnectTracking();
            SetReconnectState(NetLiteReconnectState.Disabled);
            return PrepareNode(role, true, Math.Max(0, _startup.ListenPort));
        }

        private bool PrepareNode(NetworkRole role, bool isTickAuthority, int port)
        {
            EnsureConfigs();
            if (Role == role && Node != null && Node.IsRunning)
            {
                ApplyRuntimeDebugConfig();
                return true;
            }

            if (Role != NetworkRole.None || Node != null)
            {
                Stop();
            }

            var node = new NetLiteNode(isTickAuthority, _startup.ToOptions(_runtimeDebug));
            node.ConnectionApproval = _connectionApproval;
            SubscribeNode(node);

            if (!node.Start(port))
            {
                UnsubscribeNode(node);
                node.Dispose();
                return false;
            }

            Node = node;
            Role = role;
            _sessionEstablished = false;
            ApplyRuntimeDebugConfig();
            OnRoleChanged?.Invoke(Role);
            OnNodeCreated?.Invoke(node);
            return true;
        }
        private void SubscribeNode(NetLiteNode node)
        {
            node.OnSessionEstablished += HandleSessionEstablished;
            node.OnPeerConnected += HandlePeerConnected;
            node.OnPeerDisconnected += HandlePeerDisconnected;
            node.OnPeerMetricsUpdated += HandlePeerMetricsUpdated;
            node.OnTransportPeerDisconnected += HandleTransportPeerDisconnected;
        }

        private void UnsubscribeNode(NetLiteNode node)
        {
            node.OnSessionEstablished -= HandleSessionEstablished;
            node.OnPeerConnected -= HandlePeerConnected;
            node.OnPeerDisconnected -= HandlePeerDisconnected;
            node.OnPeerMetricsUpdated -= HandlePeerMetricsUpdated;
            node.OnTransportPeerDisconnected -= HandleTransportPeerDisconnected;
        }

        private void HandleSessionEstablished(NetLiteSessionEstablishedInfo info)
        {
            _sessionEstablished = true;
            _nextReconnectAt = -1f;
            _reconnectCyclesStarted = 0;
            SetReconnectState(_reconnect.Enabled && Role == NetworkRole.Client
                ? NetLiteReconnectState.Idle
                : NetLiteReconnectState.Disabled);
            OnSessionEstablished?.Invoke(info);
        }

        private void HandlePeerConnected(PlayerId playerId) => OnPeerConnected?.Invoke(playerId);

        private void HandlePeerDisconnected(PlayerId playerId) => OnPeerDisconnected?.Invoke(playerId);

        private void HandlePeerMetricsUpdated(PlayerId playerId, NetLitePeerMetrics metrics) =>
            OnPeerMetricsUpdated?.Invoke(playerId, metrics);

        private void HandleTransportPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (_stopping || Role != NetworkRole.Client || !_reconnect.Enabled || Node == null || !Node.IsRunning)
            {
                return;
            }

            var hadSession = _sessionEstablished;
            _sessionEstablished = false;
            ScheduleReconnect(hadSession ? _reconnect.InitialDelaySeconds : _reconnect.RetryDelaySeconds);
        }

        private void UpdateReconnectLoop()
        {
            if (Node == null || !Node.IsRunning || ReconnectState != NetLiteReconnectState.Waiting || _nextReconnectAt < 0f)
            {
                return;
            }

            if (Time.unscaledTime >= _nextReconnectAt)
            {
                TryConnectCycle();
            }
        }

        private bool TryConnectCycle()
        {
            if (Node == null || !Node.IsRunning || !_remote.IsConfigured)
            {
                if (_reconnect.Enabled)
                {
                    SetReconnectState(NetLiteReconnectState.Failed);
                }

                return false;
            }

            if (_reconnect.Enabled && _reconnect.MaxReconnectCycles > 0 && _reconnectCyclesStarted >= _reconnect.MaxReconnectCycles)
            {
                SetReconnectState(NetLiteReconnectState.Failed);
                return false;
            }

            if (_reconnect.Enabled)
            {
                ++_reconnectCyclesStarted;
                SetReconnectState(NetLiteReconnectState.Connecting);
            }
            else
            {
                SetReconnectState(NetLiteReconnectState.Disabled);
            }

            _nextReconnectAt = -1f;
            var peer = Node.Connect(_remote.Host, _remote.Port, BuildConnectOptions());
            if (peer != null)
            {
                return true;
            }

            if (_reconnect.Enabled)
            {
                ScheduleReconnect(_reconnect.RetryDelaySeconds);
            }

            return false;
        }

        private void ScheduleReconnect(float delaySeconds)
        {
            if (!_reconnect.Enabled)
            {
                SetReconnectState(NetLiteReconnectState.Disabled);
                return;
            }

            if (_reconnect.MaxReconnectCycles > 0 && _reconnectCyclesStarted >= _reconnect.MaxReconnectCycles)
            {
                SetReconnectState(NetLiteReconnectState.Failed);
                return;
            }

            _nextReconnectAt = Time.unscaledTime + Mathf.Max(0f, delaySeconds);
            SetReconnectState(NetLiteReconnectState.Waiting);
        }

        private NetLiteConnectOptions BuildConnectOptions()
        {
            var requestedPlayerId = PlayerId.None;
            var reconnectToken = Guid.Empty;

            if (_remote.UseStoredReconnectIdentity && Node != null && Node.LocalReconnectToken != Guid.Empty)
            {
                requestedPlayerId = Node.LocalPlayerId;
                reconnectToken = Node.LocalReconnectToken;
            }
            else if (_remote.RequestedPlayerId != PlayerId.None.Value)
            {
                requestedPlayerId = new PlayerId(_remote.RequestedPlayerId);
            }

            return new NetLiteConnectOptions(requestedPlayerId, reconnectToken);
        }

        private void ApplyRuntimeDebugConfig()
        {
            if (Node == null)
            {
                return;
            }

            Node.ApplyNetworkSimulation(
                _runtimeDebug.SimulateLatency,
                _runtimeDebug.MinLatencyMs,
                _runtimeDebug.MaxLatencyMs,
                _runtimeDebug.SimulatePacketLoss,
                _runtimeDebug.PacketLossPercent);
        }

        private void ResetReconnectTracking()
        {
            _nextReconnectAt = -1f;
            _reconnectCyclesStarted = 0;
            _sessionEstablished = false;
        }

        private void SetReconnectState(NetLiteReconnectState state)
        {
            if (ReconnectState == state)
            {
                return;
            }

            ReconnectState = state;
            OnReconnectStateChanged?.Invoke(state);
        }

        private void EnsureConfigs()
        {
            _startup ??= new NetLiteStartupConfig();
            _remote ??= new NetLiteRemoteEndpointConfig();
            _runtimeDebug ??= new NetLiteRuntimeDebugConfig();
            _reconnect ??= new NetLiteReconnectConfig();
        }
    }
}


