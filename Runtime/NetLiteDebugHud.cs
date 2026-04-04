using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Validosik.Core.NetLite;
using Validosik.Core.NetLite.Stats;
using Validosik.Core.NetLite.Types;

namespace Validosik.Core.NetLite.Unity
{
    [DisallowMultipleComponent]
    public sealed class NetLiteDebugHud : MonoBehaviour
    {
        private enum HudDetailLevel : byte
        {
            Off,
            LevelOne,
            LevelTwo
        }

        private const string OverlayRootName = "NetLiteDebugOverlay";
        private const string PanelName = "Panel";
        private const string TextName = "Text";
        private const float PanelWidth = 560f;
        private const float PanelPadding = 12f;
        private const float MinPanelHeight = 180f;
        private const float ScreenMargin = 24f;

        [SerializeField] private NetLiteBootstrap _bootstrap;
        [SerializeField] private bool _showWhenStopped = true;
        [SerializeField, Min(0f)] private float _refreshIntervalSeconds = 0.1f;

        private readonly StringBuilder _builder = new(512);
        private float _nextRefreshAt;
        private HudDetailLevel _detailLevel;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _panelRect;
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
        public bool OverlayEnabled => _detailLevel != HudDetailLevel.Off;

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
                CycleOverlayLevel();
            }

            if (_refreshIntervalSeconds <= 0f || Time.unscaledTime >= _nextRefreshAt)
            {
                RefreshNow();
                _nextRefreshAt = _refreshIntervalSeconds <= 0f
                    ? Time.unscaledTime
                    : Time.unscaledTime + _refreshIntervalSeconds;
            }
        }

        public void ToggleOverlay() => CycleOverlayLevel();

        public void SetOverlayEnabled(bool enabled)
        {
            var targetLevel = enabled ? HudDetailLevel.LevelOne : HudDetailLevel.Off;
            if (_detailLevel == targetLevel)
            {
                return;
            }

            _detailLevel = targetLevel;
            RefreshNow();
        }

        public void RefreshNow()
        {
            ResolveBootstrap();
            EnsureOverlay();

            var hasData = _bootstrap != null && (_showWhenStopped || _bootstrap.IsRunning);
            var nextText = hasData && OverlayEnabled ? BuildText() : string.Empty;
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

            UpdateOverlayLayout();

            var isVisible = isActiveAndEnabled && OverlayEnabled && hasData;
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

            var node = _bootstrap.Node;
            var hasMetrics = TryGetAuthorityMetrics(node, out var metrics);

            AppendLine($"Ping: {FormatPing(hasMetrics ? metrics.PingMs : -1)}");
            AppendLine($"Bandwidth Out: {FormatBandwidth(hasMetrics ? metrics.UploadBytesPerSecond : -1d)}");
            AppendLine($"Bandwidth In: {FormatBandwidth(hasMetrics ? metrics.DownloadBytesPerSecond : -1d)}");
            AppendLine($"Jitter: {FormatJitter(hasMetrics ? metrics.EstimatedJitterMs : -1d)}");
            AppendLine($"Delay: {FormatActivePresetSummary(_bootstrap.RuntimeDebug)}");

            if (_detailLevel != HudDetailLevel.LevelTwo)
            {
                return _builder.ToString();
            }

            AppendLine(string.Empty);
            AppendLine("Keys:");
            AppendLine($"F3: {GetNextLevelLabel()}");
            AppendLine($"F4: preset1 {FormatPresetConfig(_bootstrap.RuntimeDebug.PresetF4MinLatencyMs, _bootstrap.RuntimeDebug.PresetF4MaxLatencyMs, _bootstrap.RuntimeDebug.PresetF4PacketLossPercent)}");
            AppendLine($"F5: preset2 {FormatPresetConfig(_bootstrap.RuntimeDebug.PresetF5MinLatencyMs, _bootstrap.RuntimeDebug.PresetF5MaxLatencyMs, _bootstrap.RuntimeDebug.PresetF5PacketLossPercent)}");
            AppendLine($"Tick: {FormatTick(node)}");
            AppendLine($"Peers: {FormatPeers(node)}");

            if (_bootstrap.RuntimeDebug.HasActivePreset)
            {
                AppendLine($"Preset: {_bootstrap.RuntimeDebug.ActivePresetLabel}");
                AppendLine($"Preset Latency: {FormatLatency(_bootstrap.RuntimeDebug.EffectiveMinLatencyMs, _bootstrap.RuntimeDebug.EffectiveMaxLatencyMs)}");
                AppendLine($"Preset Loss: {FormatPacketLoss(_bootstrap.RuntimeDebug.EffectivePacketLossPercent)}");
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

        private void UpdateOverlayLayout()
        {
            if (_panelRect == null || _text == null)
            {
                return;
            }

            var preferredHeight = _text.preferredHeight + (PanelPadding * 2f);
            var maxHeight = Mathf.Max(MinPanelHeight, Screen.height - ScreenMargin);
            _panelRect.sizeDelta = new Vector2(
                PanelWidth,
                Mathf.Clamp(preferredHeight, MinPanelHeight, maxHeight));
        }

        private void CycleOverlayLevel()
        {
            _detailLevel = _detailLevel switch
            {
                HudDetailLevel.Off => HudDetailLevel.LevelOne,
                HudDetailLevel.LevelOne => HudDetailLevel.LevelTwo,
                _ => HudDetailLevel.Off
            };

            RefreshNow();
        }

        private string GetNextLevelLabel()
        {
            return _detailLevel switch
            {
                HudDetailLevel.LevelOne => "show more",
                HudDetailLevel.LevelTwo => "hide HUD",
                _ => "show HUD"
            };
        }

        private void EnsureOverlay()
        {
            if (_canvas != null && _canvasGroup != null && _text != null && _panelRect != null)
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
            panel.sizeDelta = new Vector2(PanelWidth, MinPanelHeight);
            _panelRect = panel;

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
            textRoot.offsetMin = new Vector2(PanelPadding, PanelPadding);
            textRoot.offsetMax = new Vector2(-PanelPadding, -PanelPadding);

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

        private bool TryGetAuthorityMetrics(NetLiteNode node, out NetLitePeerMetrics metrics)
        {
            metrics = default;
            return node != null
                && node.AuthorityPlayerId != PlayerId.None
                && _bootstrap.TryGetAuthorityMetrics(out metrics);
        }

        private static Font LoadDefaultFont()
        {
#if UNITY_6000_0_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        private static string FormatActivePresetSummary(NetLiteBootstrapRuntimeDebug runtimeDebug)
        {
            if (runtimeDebug == null || !runtimeDebug.EffectiveSimulateLatency)
            {
                return "off";
            }

            return runtimeDebug.ActivePresetLabel;
        }

        private static string FormatPresetConfig(int minLatencyMs, int maxLatencyMs, int packetLossPercent)
        {
            return $"{FormatLatency(minLatencyMs, maxLatencyMs)} loss={FormatPacketLoss(packetLossPercent)}";
        }

        private static string FormatLatency(int minLatencyMs, int maxLatencyMs)
        {
            if (maxLatencyMs <= 0)
            {
                return "off";
            }

            return $"{minLatencyMs}-{Mathf.Max(minLatencyMs, maxLatencyMs)} ms";
        }

        private static string FormatPacketLoss(int packetLossPercent) =>
            packetLossPercent > 0 ? $"{packetLossPercent}%" : "off";

        private static string FormatPing(int pingMs) => pingMs >= 0 ? $"{pingMs} ms" : "n/a";

        private static string FormatJitter(double jitterMs) => jitterMs >= 0d ? $"{jitterMs:F1} ms" : "n/a";

        private static string FormatBandwidth(double bytesPerSecond)
        {
            if (bytesPerSecond < 0d)
            {
                return "n/a";
            }

            const double kib = 1024d;
            const double mib = kib * 1024d;

            if (bytesPerSecond >= mib)
            {
                return $"{bytesPerSecond / mib:F2} MiB/s";
            }

            if (bytesPerSecond >= kib)
            {
                return $"{bytesPerSecond / kib:F2} KiB/s";
            }

            return $"{bytesPerSecond:F0} B/s";
        }

        private static string FormatTick(NetLiteNode node) => node != null ? node.Tick.ToString() : "n/a";

        private static string FormatPeers(NetLiteNode node) => node != null ? node.ConnectedPeerCount.ToString() : "n/a";
    }
}
