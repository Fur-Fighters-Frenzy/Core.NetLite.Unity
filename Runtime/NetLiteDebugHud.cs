using System.Text;
using UnityEngine;
using Validosik.Core.NetLite.Types;

namespace Validosik.Core.NetLite.Unity
{
    public sealed class NetLiteDebugHud : MonoBehaviour
    {
        [SerializeField] private NetLiteBootstrap _bootstrap;
        [SerializeField] private bool _autoFindBootstrap = true;
        [SerializeField] private bool _showWhenStopped = true;
        [SerializeField] private Vector2 _position = new(10f, 10f);
        [SerializeField] private int _width = 340;
        [SerializeField] private int _fontSize = 13;

        private readonly StringBuilder _builder = new(512);
        private GUIStyle _style;

        private void Awake() => ResolveBootstrap();

        private void OnGUI()
        {
            ResolveBootstrap();
            if (_bootstrap == null)
            {
                return;
            }

            if (!_showWhenStopped && !_bootstrap.IsRunning)
            {
                return;
            }

            EnsureStyle();
            _builder.Clear();
            AppendLine($"Role: {_bootstrap.Role}");
            AppendLine($"Reconnect: {_bootstrap.ReconnectState} cycles={_bootstrap.ReconnectCyclesStarted}");

            var node = _bootstrap.Node;
            if (node == null)
            {
                AppendLine("Node: null");
            }
            else
            {
                AppendLine($"Running: {node.IsRunning}");
                AppendLine($"Tick: {node.Tick}");
                AppendLine($"LocalPid: {FormatPlayerId(node.LocalPlayerId)}");
                AppendLine($"AuthorityPid: {FormatPlayerId(node.AuthorityPlayerId)}");
                AppendLine($"Peers: {node.ConnectedPeerCount}");
                AppendLine($"ListenPort: {node.LocalPort}");
                AppendLine($"Remote: {_bootstrap.Remote.Host}:{_bootstrap.Remote.Port}");
                AppendLine($"SimLatency: {_bootstrap.RuntimeDebug.SimulateLatency} {_bootstrap.RuntimeDebug.MinLatencyMs}-{_bootstrap.RuntimeDebug.MaxLatencyMs} ms");
                AppendLine($"SimLoss: {_bootstrap.RuntimeDebug.SimulatePacketLoss} {_bootstrap.RuntimeDebug.PacketLossPercent}%");

                if (_bootstrap.TryGetAuthorityMetrics(out var metrics) && node.AuthorityPlayerId != PlayerId.None)
                {
                    AppendLine($"Ping: {metrics.PingMs} ms");
                    AppendLine($"Jitter: {metrics.EstimatedJitterMs:F1} ms");
                    AppendLine($"Up: {metrics.UploadBytesPerSecond:F0} B/s");
                    AppendLine($"Down: {metrics.DownloadBytesPerSecond:F0} B/s");
                }
            }

            var rect = new Rect(_position.x, _position.y, _width, Screen.height);
            GUI.Label(rect, _builder.ToString(), _style);
        }

        private void AppendLine(string value) => _builder.AppendLine(value);

        private void EnsureStyle()
        {
            if (_style != null)
            {
                return;
            }

            _style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = Mathf.Max(10, _fontSize),
                richText = false,
                wordWrap = false,
                padding = new RectOffset(8, 8, 6, 6)
            };
        }

        private void ResolveBootstrap()
        {
            if (_bootstrap == null && _autoFindBootstrap)
            {
                _bootstrap = GetComponent<NetLiteBootstrap>() ?? FindObjectOfType<NetLiteBootstrap>();
            }
        }

        private static string FormatPlayerId(PlayerId playerId) => playerId == PlayerId.None ? "None" : playerId.Value.ToString();
    }
}
