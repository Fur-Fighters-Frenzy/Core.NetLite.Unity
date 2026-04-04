using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Validosik.Core.NetLite.Types;

namespace Validosik.Core.NetLite.Unity
{
    [DisallowMultipleComponent]
    public sealed class NetLiteDebugHud : MonoBehaviour
    {
        private const string OverlayRootName = "NetLiteDebugOverlay";
        private const string PanelName = "Panel";
        private const string TextName = "Text";

        [SerializeField] private NetLiteBootstrap _bootstrap;
        [SerializeField] private bool _showWhenStopped = true;
        [SerializeField, Min(0f)] private float _refreshIntervalSeconds = 0.1f;

        private readonly StringBuilder _builder = new(512);
        private float _nextRefreshAt;
        private bool _overlayEnabled = true;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private Text _text;

        public event Action<string> OnTextChanged;
        public event Action<bool> OnVisibilityChanged;

        public NetLiteBootstrap Bootstrap
        {
            get
            {
                ResolveBootstrap();
                return _bootstrap;
            }
        }

        public string CurrentText { get; private set; } = string.Empty;
        public bool IsVisible { get; private set; }
        public bool OverlayEnabled => _overlayEnabled;

        private void Awake()
        {
            ResolveBootstrap();
            EnsureOverlay();
            RefreshNow();
        }

        private void OnEnable()
        {
            _nextRefreshAt = 0f;
            EnsureOverlay();
            RefreshNow();
        }

        private void OnDisable()
        {
            ApplyOverlayVisibility(false);
            if (IsVisible)
            {
                IsVisible = false;
                OnVisibilityChanged?.Invoke(false);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3))
            {
                ToggleOverlay();
            }

            if (_refreshIntervalSeconds <= 0f || Time.unscaledTime >= _nextRefreshAt)
            {
                RefreshNow();
                _nextRefreshAt = _refreshIntervalSeconds <= 0f
                    ? Time.unscaledTime
                    : Time.unscaledTime + _refreshIntervalSeconds;
            }
        }

        public void ToggleOverlay() => SetOverlayEnabled(!_overlayEnabled);

        public void SetOverlayEnabled(bool enabled)
        {
            if (_overlayEnabled == enabled)
            {
                return;
            }

            _overlayEnabled = enabled;
            RefreshNow();
        }

        public void RefreshNow()
        {
            ResolveBootstrap();
            EnsureOverlay();

            var hasData = _bootstrap != null && (_showWhenStopped || _bootstrap.IsRunning);
            var nextText = hasData ? BuildText() : string.Empty;
            if (!string.Equals(CurrentText, nextText, StringComparison.Ordinal))
            {
                CurrentText = nextText;
                if (_text != null)
                {
                    _text.text = nextText;
                }

                OnTextChanged?.Invoke(nextText);
            }
            else if (_text != null && _text.text != nextText)
            {
                _text.text = nextText;
            }

            var isVisible = isActiveAndEnabled && _overlayEnabled && hasData;
            ApplyOverlayVisibility(isVisible);
            if (IsVisible == isVisible)
            {
                return;
            }

            IsVisible = isVisible;
            OnVisibilityChanged?.Invoke(isVisible);
        }

        private string BuildText()
        {
            _builder.Clear();
            AppendLine("NetLite HUD  F3 overlay  F4/F5 delay presets");
            AppendLine($"Role: {_bootstrap.Role}");
            AppendLine($"Reconnect: {_bootstrap.ReconnectState} cycles={_bootstrap.ReconnectCyclesStarted}");
            AppendLine($"ReconnectCfg: {_bootstrap.Reconnect.Enabled} init={_bootstrap.Reconnect.InitialDelaySeconds:0.##} retry={_bootstrap.Reconnect.RetryDelaySeconds:0.##} max={_bootstrap.Reconnect.MaxReconnectCycles}");
            AppendLine($"ConnectRetry: {_bootstrap.Reconnect.TransportReconnectDelayMs} ms attempts={_bootstrap.Reconnect.TransportMaxConnectAttempts}");
            AppendLine($"Session: {_bootstrap.Startup.SessionId}");
            AppendLine($"Key: {_bootstrap.Startup.ConnectionKey}");
            AppendLine($"Preset1 AutoStart: {_bootstrap.RuntimeDebug.EnablePresetOneOnStart}");

            var node = _bootstrap.Node;
            if (node == null)
            {
                AppendLine("Node: null");
                return _builder.ToString();
            }

            AppendLine($"Running: {node.IsRunning}");
            AppendLine($"Tick: {node.Tick}");
            AppendLine($"TickRate: {_bootstrap.Startup.TickRate}");
            AppendLine($"LocalPid: {FormatPlayerId(node.LocalPlayerId)}");
            AppendLine($"AuthorityPid: {FormatPlayerId(node.AuthorityPlayerId)}");
            AppendLine($"Peers: {node.ConnectedPeerCount}");
            AppendLine($"ListenPort: {node.LocalPort}");
            AppendLine($"Remote: {_bootstrap.Remote.Host}:{_bootstrap.Remote.Port}");
            AppendLine($"Preset1: {_bootstrap.RuntimeDebug.PresetF4MinLatencyMs}-{_bootstrap.RuntimeDebug.PresetF4MaxLatencyMs} ms loss={_bootstrap.RuntimeDebug.PresetF4PacketLossPercent}%");
            AppendLine($"Preset2: {_bootstrap.RuntimeDebug.PresetF5MinLatencyMs}-{_bootstrap.RuntimeDebug.PresetF5MaxLatencyMs} ms loss={_bootstrap.RuntimeDebug.PresetF5PacketLossPercent}%");
            AppendLine($"ActivePreset: {_bootstrap.RuntimeDebug.ActivePresetLabel}");
            AppendLine($"SimLatency: {_bootstrap.RuntimeDebug.EffectiveSimulateLatency} {_bootstrap.RuntimeDebug.EffectiveMinLatencyMs}-{_bootstrap.RuntimeDebug.EffectiveMaxLatencyMs} ms");
            AppendLine($"SimLoss: {_bootstrap.RuntimeDebug.EffectiveSimulatePacketLoss} {_bootstrap.RuntimeDebug.EffectivePacketLossPercent}%");

            if (_bootstrap.TryGetAuthorityMetrics(out var metrics) && node.AuthorityPlayerId != PlayerId.None)
            {
                AppendLine($"Ping: {metrics.PingMs} ms");
                AppendLine($"Jitter: {metrics.EstimatedJitterMs:F1} ms");
                AppendLine($"Up: {metrics.UploadBytesPerSecond:F0} B/s");
                AppendLine($"Down: {metrics.DownloadBytesPerSecond:F0} B/s");
            }

            return _builder.ToString();
        }

        private void AppendLine(string value) => _builder.AppendLine(value);

        private void ApplyOverlayVisibility(bool isVisible)
        {
            if (_canvas != null)
            {
                _canvas.enabled = isVisible;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = isVisible ? 1f : 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        private void EnsureOverlay()
        {
            if (_canvas != null && _canvasGroup != null && _text != null)
            {
                return;
            }

            var overlayRoot = transform.Find(OverlayRootName) as RectTransform;
            if (overlayRoot == null)
            {
                overlayRoot = new GameObject(
                    OverlayRootName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster),
                    typeof(CanvasGroup)).GetComponent<RectTransform>();
                overlayRoot.SetParent(transform, false);
            }

            overlayRoot.anchorMin = Vector2.zero;
            overlayRoot.anchorMax = Vector2.one;
            overlayRoot.offsetMin = Vector2.zero;
            overlayRoot.offsetMax = Vector2.zero;

            _canvas = overlayRoot.GetComponent<Canvas>() ?? overlayRoot.gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = short.MaxValue;

            var scaler = overlayRoot.GetComponent<CanvasScaler>() ?? overlayRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            var raycaster = overlayRoot.GetComponent<GraphicRaycaster>() ?? overlayRoot.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            _canvasGroup = overlayRoot.GetComponent<CanvasGroup>() ?? overlayRoot.gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            var panel = overlayRoot.Find(PanelName) as RectTransform;
            if (panel == null)
            {
                panel = new GameObject(PanelName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                panel.SetParent(overlayRoot, false);
            }

            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(12f, -12f);
            panel.sizeDelta = new Vector2(560f, 360f);

            var background = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.04f, 0.05f, 0.06f, 0.78f);
            background.raycastTarget = false;

            var textRoot = panel.Find(TextName) as RectTransform;
            if (textRoot == null)
            {
                textRoot = new GameObject(TextName, typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<RectTransform>();
                textRoot.SetParent(panel, false);
            }

            textRoot.anchorMin = Vector2.zero;
            textRoot.anchorMax = Vector2.one;
            textRoot.offsetMin = new Vector2(12f, 12f);
            textRoot.offsetMax = new Vector2(-12f, -12f);

            _text = textRoot.GetComponent<Text>() ?? textRoot.gameObject.AddComponent<Text>();
            _text.font = LoadDefaultFont();
            _text.fontSize = 15;
            _text.alignment = TextAnchor.UpperLeft;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.supportRichText = false;
            _text.raycastTarget = false;
            _text.color = new Color(0.93f, 0.96f, 0.89f, 1f);

            var outline = textRoot.GetComponent<Outline>() ?? textRoot.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private void ResolveBootstrap()
        {
            if (_bootstrap == null)
            {
                _bootstrap = GetComponent<NetLiteBootstrap>() ?? FindObjectOfType<NetLiteBootstrap>();
            }
        }

        private static Font LoadDefaultFont()
        {
#if UNITY_6000_0_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        private static string FormatPlayerId(PlayerId playerId) => playerId == PlayerId.None ? "None" : playerId.Value.ToString();
    }
}
