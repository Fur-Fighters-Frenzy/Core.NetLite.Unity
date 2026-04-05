using System;
using LiteNetLib;
using UnityEngine;
using Validosik.Core.NetLite;
using Validosik.Core.NetLite.Session;
using Validosik.Core.NetLite.Stats;
using Validosik.Core.NetLite.Types;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Validosik.Core.NetLite.Unity
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetLiteBootstrapRunner))]
    [RequireComponent(typeof(NetLiteBootstrapStartup))]
    [RequireComponent(typeof(NetLiteBootstrapRemote))]
    [RequireComponent(typeof(NetLiteBootstrapRuntimeDebug))]
    [RequireComponent(typeof(NetLiteBootstrapReconnect))]
    public sealed class NetLiteBootstrap : MonoBehaviour
    {
        [SerializeField] private NetLiteBootstrapPreset _preset;

        private NetLiteBootstrapRunner _runner;
        private NetLiteBootstrapStartup _startup;
        private NetLiteBootstrapRemote _remote;
        private NetLiteBootstrapRuntimeDebug _runtimeDebug;
        private NetLiteBootstrapReconnect _reconnect;

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
        public NetLiteBootstrapRunner Runner
        {
            get
            {
                ResolveComponents(createIfMissing: true);
                return _runner;
            }
        }

        public NetLiteBootstrapStartup Startup
        {
            get
            {
                ResolveComponents(createIfMissing: true);
                return _startup;
            }
        }

        public NetLiteBootstrapRemote Remote
        {
            get
            {
                ResolveComponents(createIfMissing: true);
                return _remote;
            }
        }

        public NetLiteBootstrapRuntimeDebug RuntimeDebug
        {
            get
            {
                ResolveComponents(createIfMissing: true);
                return _runtimeDebug;
            }
        }

        public NetLiteBootstrapReconnect Reconnect
        {
            get
            {
                ResolveComponents(createIfMissing: true);
                return _reconnect;
            }
        }

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

        private void Reset()
        {
            ResolveComponents(createIfMissing: true);
        }

        private void Awake()
        {
            ResolveComponents(createIfMissing: true);
            if (_preset != null)
            {
                ApplyPreset();
            }
        }

        private void Start()
        {
            if (Runner.ShouldAutoStart)
            {
                StartRole(Runner.EffectiveStartRole);
            }
        }

        private void Update()
        {
            if (Runner.AutoUpdateNode && Node != null)
            {
                Node.Update(Runner.GetDeltaTime());
            }

            ApplyLiveNodeOptions();

            if (Role == NetworkRole.Client)
            {
                UpdateReconnectLoop();
            }
        }

        private void OnDestroy() => Stop();

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveComponents(createIfMissing: true);
        }
#endif

        public void ApplyPreset()
        {
            ResolveComponents(createIfMissing: true);
            if (_preset == null)
            {
                return;
            }

            var startupPreset = _preset.Startup;
            _startup.Apply(startupPreset);
            _remote.Apply(_preset.Remote);
            _runtimeDebug.Apply(_preset.RuntimeDebug);
            _reconnect.Apply(_preset.Reconnect);

            ApplyLiveNodeOptions();
        }

        public bool StartRole(NetworkRole role)
        {
            return NormalizeRole(role) switch
            {
                NetworkRole.Host => StartAuthorityRole(NetworkRole.Host),
                NetworkRole.Client => StartClient(),
                _ => false
            };
        }

        public bool StartHost() => StartAuthorityRole(NetworkRole.Host);

        public bool StartClient()
        {
            if (!PrepareNode(NetworkRole.Client, false, Math.Max(0, Startup.ClientListenPort)))
            {
                return false;
            }

            ResetReconnectTracking();
            if (!Reconnect.Enabled)
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
            Remote.Host = host;
            Remote.Port = port;
        }

        public void SetRuntimeDebugConfig(NetLiteRuntimeDebugConfig config)
        {
            ResolveComponents(createIfMissing: true);
            _runtimeDebug.Apply(config);
            ApplyLiveNodeOptions();
        }

        public void SetLatencySimulation(bool enabled, int minLatencyMs, int maxLatencyMs)
        {
            RuntimeDebug.PresetF4.MinLatencyMs = enabled ? minLatencyMs : 0;
            RuntimeDebug.PresetF4.MaxLatencyMs = enabled ? maxLatencyMs : 0;
            SyncPresetOneActivation();

            ApplyLiveNodeOptions();
        }

        public void SetPacketLossSimulation(bool enabled, int packetLossPercent)
        {
            RuntimeDebug.PresetF4.PacketLossPercent = enabled ? packetLossPercent : 0;
            SyncPresetOneActivation();

            ApplyLiveNodeOptions();
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
            return PrepareNode(role, true, Math.Max(0, Startup.ListenPort));
        }

        private bool PrepareNode(NetworkRole role, bool isTickAuthority, int port)
        {
            ResolveComponents(createIfMissing: true);
            if (Role == role && Node != null && Node.IsRunning)
            {
                ApplyLiveNodeOptions();
                return true;
            }

            if (Role != NetworkRole.None || Node != null)
            {
                Stop();
            }

            var node = new NetLiteNode(isTickAuthority, Startup.ToOptions(RuntimeDebug, Reconnect));
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
            ApplyLiveNodeOptions();
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
            SetReconnectState(Reconnect.Enabled && Role == NetworkRole.Client
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
            if (_stopping || Role != NetworkRole.Client || !Reconnect.Enabled || Node == null || !Node.IsRunning)
            {
                return;
            }

            var hadSession = _sessionEstablished;
            _sessionEstablished = false;
            ScheduleReconnect(hadSession ? Reconnect.InitialDelaySeconds : Reconnect.RetryDelaySeconds);
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
            if (Node == null || !Node.IsRunning || !Remote.IsConfigured)
            {
                if (Reconnect.Enabled)
                {
                    SetReconnectState(NetLiteReconnectState.Failed);
                }

                return false;
            }

            if (Reconnect.Enabled && Reconnect.MaxReconnectCycles > 0 && _reconnectCyclesStarted >= Reconnect.MaxReconnectCycles)
            {
                SetReconnectState(NetLiteReconnectState.Failed);
                return false;
            }

            if (Reconnect.Enabled)
            {
                ++_reconnectCyclesStarted;
                SetReconnectState(NetLiteReconnectState.Connecting);
            }
            else
            {
                SetReconnectState(NetLiteReconnectState.Disabled);
            }

            _nextReconnectAt = -1f;
            var peer = Node.Connect(Remote.Host, Remote.Port, BuildConnectOptions());
            if (peer != null)
            {
                return true;
            }

            if (Reconnect.Enabled)
            {
                ScheduleReconnect(Reconnect.RetryDelaySeconds);
            }

            return false;
        }

        private void ScheduleReconnect(float delaySeconds)
        {
            if (!Reconnect.Enabled)
            {
                SetReconnectState(NetLiteReconnectState.Disabled);
                return;
            }

            if (Reconnect.MaxReconnectCycles > 0 && _reconnectCyclesStarted >= Reconnect.MaxReconnectCycles)
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

            if (Remote.UseStoredReconnectIdentity && Node != null && Node.LocalReconnectToken != Guid.Empty)
            {
                requestedPlayerId = Node.LocalPlayerId;
                reconnectToken = Node.LocalReconnectToken;
            }
            else if (Remote.RequestedPlayerId != PlayerId.None.Value)
            {
                requestedPlayerId = new PlayerId(Remote.RequestedPlayerId);
            }

            return new NetLiteConnectOptions(requestedPlayerId, reconnectToken);
        }

        private void ApplyLiveNodeOptions()
        {
            if (Node == null)
            {
                return;
            }

            Startup.ApplyTo(Node);
            Reconnect.ApplyTo(Node);
            RuntimeDebug.ApplyTo(Node);
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

        private void ResolveComponents(bool createIfMissing = false)
        {
            _runner = ResolveComponent(_runner, createIfMissing);
            _startup = ResolveComponent(_startup, createIfMissing);
            _remote = ResolveComponent(_remote, createIfMissing);
            _runtimeDebug = ResolveComponent(_runtimeDebug, createIfMissing);
            _reconnect = ResolveComponent(_reconnect, createIfMissing);
        }

        private T ResolveComponent<T>(T current, bool createIfMissing) where T : Component
        {
            if (current != null && current.gameObject == gameObject)
            {
                return current;
            }

            if (TryGetComponent(out T component))
            {
                return component;
            }

            if (!createIfMissing)
            {
                return null;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return Undo.AddComponent<T>(gameObject);
            }
#endif

            return gameObject.AddComponent<T>();
        }

        private static NetworkRole NormalizeRole(NetworkRole role) =>
            role == NetworkRole.Client
                ? NetworkRole.Client
                : role == NetworkRole.None
                    ? NetworkRole.None
                    : NetworkRole.Host;

        private void SyncPresetOneActivation()
        {
            var shouldEnablePreset = RuntimeDebug.PresetF4.MaxLatencyMs > 0 || RuntimeDebug.PresetF4.PacketLossPercent > 0;
            if (shouldEnablePreset)
            {
                if (RuntimeDebug.ActivePresetLabel != "F4")
                {
                    RuntimeDebug.TogglePresetOne();
                }

                return;
            }

            if (RuntimeDebug.ActivePresetLabel == "F4")
            {
                RuntimeDebug.DisablePreset();
            }
        }
    }
}
