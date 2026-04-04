using UnityEngine;
using UnityEngine.Serialization;
using Validosik.Core.NetLite;

namespace Validosik.Core.NetLite.Unity
{
    [DefaultExecutionOrder(-110)]
    [AddComponentMenu("Validosik/NetLite/Bootstrap Runtime Debug")]
    [DisallowMultipleComponent]
    public sealed class NetLiteBootstrapRuntimeDebug : MonoBehaviour
    {
        private enum DebugPresetSlot : byte
        {
            None,
            F4,
            F5
        }

        public bool EnablePresetOneOnStart;

        [FormerlySerializedAs("DelayPresetF4")]
        public NetLiteLatencyHotkeyConfig PresetF4 = new();

        [FormerlySerializedAs("DelayPresetF5")]
        public NetLiteLatencyHotkeyConfig PresetF5 = new() { MinLatencyMs = 250, MaxLatencyMs = 250 };

        [SerializeField, HideInInspector, FormerlySerializedAs("SimulateLatency")]
        private bool _legacySimulateLatency;

        [SerializeField, HideInInspector, FormerlySerializedAs("MinLatencyMs")]
        private int _legacyMinLatencyMs = 30;

        [SerializeField, HideInInspector, FormerlySerializedAs("MaxLatencyMs")]
        private int _legacyMaxLatencyMs = 100;

        [SerializeField, HideInInspector, FormerlySerializedAs("SimulatePacketLoss")]
        private bool _legacySimulatePacketLoss;

        [SerializeField, HideInInspector, FormerlySerializedAs("PacketLossPercent")]
        private int _legacyPacketLossPercent = 10;

        [SerializeField, HideInInspector] private bool _legacySettingsMigrated;

        private DebugPresetSlot _activePreset;

        public bool HasActivePreset => _activePreset != DebugPresetSlot.None;
        public bool EffectiveSimulateLatency => HasActivePreset && EffectiveMaxLatencyMs > 0;
        public int EffectiveMinLatencyMs => HasActivePreset ? Mathf.Max(0, GetActivePreset().MinLatencyMs) : 0;
        public int EffectiveMaxLatencyMs => HasActivePreset ? Mathf.Max(EffectiveMinLatencyMs, GetActivePreset().MaxLatencyMs) : 0;
        public bool EffectiveSimulatePacketLoss => HasActivePreset && EffectivePacketLossPercent > 0;
        public int EffectivePacketLossPercent => HasActivePreset ? Mathf.Clamp(GetActivePreset().PacketLossPercent, 0, 100) : 0;
        public int PresetF4MinLatencyMs => GetConfiguredPreset(DebugPresetSlot.F4).MinLatencyMs;
        public int PresetF4MaxLatencyMs => GetConfiguredPreset(DebugPresetSlot.F4).MaxLatencyMs;
        public int PresetF4PacketLossPercent => Mathf.Clamp(GetConfiguredPreset(DebugPresetSlot.F4).PacketLossPercent, 0, 100);
        public int PresetF5MinLatencyMs => GetConfiguredPreset(DebugPresetSlot.F5).MinLatencyMs;
        public int PresetF5MaxLatencyMs => GetConfiguredPreset(DebugPresetSlot.F5).MaxLatencyMs;
        public int PresetF5PacketLossPercent => Mathf.Clamp(GetConfiguredPreset(DebugPresetSlot.F5).PacketLossPercent, 0, 100);
        public string ActivePresetLabel => _activePreset switch
        {
            DebugPresetSlot.F4 => "F4",
            DebugPresetSlot.F5 => "F5",
            _ => "None"
        };

        private void Reset()
        {
            EnsurePresetConfigs();
            MigrateLegacySettingsIfNeeded();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsurePresetConfigs();
            MigrateLegacySettingsIfNeeded();
        }
#endif

        private void OnEnable()
        {
            EnsurePresetConfigs();
            MigrateLegacySettingsIfNeeded();
            if (EnablePresetOneOnStart && _activePreset == DebugPresetSlot.None)
            {
                _activePreset = DebugPresetSlot.F4;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F4))
            {
                TogglePresetOne();
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                TogglePresetTwo();
            }
        }

        public void Apply(NetLiteRuntimeDebugConfig other)
        {
            EnsurePresetConfigs();
            if (other == null)
            {
                return;
            }

            EnablePresetOneOnStart = other.EnablePresetOneOnStart;
            PresetF4.CopyFrom(other.PresetF4);
            PresetF5.CopyFrom(other.PresetF5);

            if (other.TryGetLegacyPresetOneOverride(out var legacyPreset, out var enableOnStart))
            {
                PresetF4.CopyFrom(legacyPreset);
                if (enableOnStart)
                {
                    EnablePresetOneOnStart = true;
                }
            }

            _activePreset = EnablePresetOneOnStart
                ? DebugPresetSlot.F4
                : DebugPresetSlot.None;
        }

        public void TogglePresetOne() => TogglePreset(DebugPresetSlot.F4);

        public void TogglePresetTwo() => TogglePreset(DebugPresetSlot.F5);

        public void DisablePreset() => _activePreset = DebugPresetSlot.None;

        public void ApplyTo(NetLiteNode node)
        {
            EnsurePresetConfigs();
            if (node == null)
            {
                return;
            }

            node.ApplyNetworkSimulation(
                EffectiveSimulateLatency,
                EffectiveMinLatencyMs,
                EffectiveMaxLatencyMs,
                EffectiveSimulatePacketLoss,
                EffectivePacketLossPercent);
        }

        private void TogglePreset(DebugPresetSlot preset)
        {
            _activePreset = _activePreset == preset
                ? DebugPresetSlot.None
                : preset;
        }

        private NetLiteLatencyHotkeyConfig GetActivePreset() => GetConfiguredPreset(_activePreset);

        private NetLiteLatencyHotkeyConfig GetConfiguredPreset(DebugPresetSlot preset)
        {
            EnsurePresetConfigs();
            return preset switch
            {
                DebugPresetSlot.F4 => PresetF4,
                DebugPresetSlot.F5 => PresetF5,
                _ => PresetF4
            };
        }

        private void EnsurePresetConfigs()
        {
            PresetF4 ??= new NetLiteLatencyHotkeyConfig();
            PresetF5 ??= new NetLiteLatencyHotkeyConfig { MinLatencyMs = 250, MaxLatencyMs = 250 };
        }

        private void MigrateLegacySettingsIfNeeded()
        {
            if (_legacySettingsMigrated)
            {
                return;
            }

            EnsurePresetConfigs();
            var hasLegacyValues = _legacySimulateLatency
                || _legacySimulatePacketLoss
                || _legacyMinLatencyMs != 30
                || _legacyMaxLatencyMs != 100
                || _legacyPacketLossPercent != 10;

            if (hasLegacyValues)
            {
                PresetF4.MinLatencyMs = _legacyMinLatencyMs;
                PresetF4.MaxLatencyMs = _legacyMaxLatencyMs;
                PresetF4.PacketLossPercent = _legacyPacketLossPercent;
                if (_legacySimulateLatency || _legacySimulatePacketLoss)
                {
                    EnablePresetOneOnStart = true;
                    _activePreset = DebugPresetSlot.F4;
                }
            }

            _legacySettingsMigrated = true;
        }
    }
}
