using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AttachedBehaviours;
using SprocketAdvancedAPS.Systems;
using UnityEngine.InputSystem;
using UnityEngine;

namespace SprocketAdvancedAPS.Core
{
    internal sealed class APSCore
    {
        internal static APSCore? Instance { get; private set; }
        private readonly ManualLogSource _log;
        
        // Configuration
        internal static ConfigEntry<bool> ConfigEnabled { get; private set; } = null!;
        internal static ConfigEntry<bool> ConfigDebugMode { get; private set; } = null!;
        internal static ConfigEntry<bool> ConfigShowHUD { get; private set; } = null!;
        internal static ConfigEntry<bool> ConfigPerformanceLog { get; private set; } = null!;
        internal static ConfigEntry<float> ConfigLaunchVolume { get; private set; } = null!;
        internal static ConfigEntry<float> ConfigGlobalDetectionMultiplier { get; private set; } = null!;
        internal static ConfigEntry<float> ConfigGlobalReactionMultiplier { get; private set; } = null!;
        internal static ConfigEntry<Key> ConfigToggleHUD { get; private set; } = null!;
        internal static ConfigEntry<Key> ConfigToggleDebug { get; private set; } = null!;
        private static readonly Dictionary<string, ConfigEntry<bool>> ModuleEnabled = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ConfigEntry<float>> ModuleRange = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ConfigEntry<float>> ModuleReaction = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ConfigEntry<float>> ModuleIntercept = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ConfigEntry<bool>> ModuleSpeedBased = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ConfigEntry<float>> ModuleMinSpeed = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ConfigEntry<float>> ModuleMaxSpeed = new(StringComparer.OrdinalIgnoreCase);

        internal static bool SpeedBasedInterception(APSDefinition definition) => definition.Type != APSType.SoftKill &&
            (definition.CustomSettings?.SpeedBased ?? (!ModuleSpeedBased.TryGetValue(definition.Id, out var entry) || entry.Value));
        internal static float MinInterceptSpeed(APSDefinition definition)
        {
            float fallback = definition.CustomSettings != null ? definition.MinEngagementSpeed : Math.Min(50f, definition.MinEngagementSpeed);
            float value = ModuleMinSpeed.TryGetValue(definition.Id, out var entry) ? entry.Value : fallback;
            return float.IsFinite(value) && value >= 0f && value <= MaxInterceptSpeed(definition) ? value : Math.Min(fallback, MaxInterceptSpeed(definition));
        }
        internal static float MaxInterceptSpeed(APSDefinition definition)
        {
            float value = ModuleMaxSpeed.TryGetValue(definition.Id, out var entry) ? entry.Value : definition.MaxEngagementSpeed;
            return float.IsFinite(value) && value > 0f ? value : definition.MaxEngagementSpeed;
        }
        
        // Per-vehicle APS state
        private readonly Dictionary<IntPtr, APSVehicleState> _vehicleStates = new();
        private readonly HashSet<IntPtr> _reportedEmptyVehicles = new();
        private float _nextVehicleDiscovery;
        private readonly Dictionary<IntPtr, Vehicle> _dirtyVehicles = new();
        internal void ModulesChanged(Vehicle vehicle) => _dirtyVehicles[vehicle.Pointer] = vehicle;
        
        internal APSCore(ManualLogSource log)
        {
            Instance = this;
            _log = log;
        }
        
        internal static void InitializeConfig(ConfigFile config)
        {
            ConfigEnabled = config.Bind("General", "Enabled", true, "Enable the Advanced APS system globally.");
            ConfigDebugMode = config.Bind("General", "DebugMode", false, "Enable debug logging and visualizations.");
            ConfigShowHUD = config.Bind("General", "ShowHUD", true, "Show APS HUD during gameplay.");
            ConfigLaunchVolume = config.Bind("Audio", "LaunchVolume", .8f, "Hard-kill launch volume, 0..1; 0 mutes. Original synthetic SFX, not real APS recordings.");
            ConfigLaunchVolume.Value = float.IsFinite(ConfigLaunchVolume.Value) ? Mathf.Clamp01(ConfigLaunchVolume.Value) : .8f;
            ConfigPerformanceLog = config.Bind("General", "PerformanceLog", false, "Report physics core time and native projectile snapshot reads every 5 seconds.");
            ConfigGlobalDetectionMultiplier = config.Bind("Balance", "GlobalDetectionMultiplier", 1.0f, "Global multiplier for all APS detection ranges.");
            ConfigGlobalReactionMultiplier = config.Bind("Balance", "GlobalReactionMultiplier", 1.0f, "Global multiplier for all APS reaction times (lower = faster).");
            ConfigToggleHUD = config.Bind("Keys", "ToggleHUD", Key.H, "Toggle APS HUD visibility.");
            ConfigToggleDebug = config.Bind("Keys", "ToggleDebug", Key.F12, "Toggle debug mode.");
            foreach (var definition in APSDefinitions.All.Values)
            {
                string section = "Modules." + definition.Id;
                ModuleEnabled[definition.Id] = config.Bind(section, "Enabled", true, "Enable this APS system.");
                ModuleRange[definition.Id] = config.Bind(section, "RangeMultiplier", 1f, "Multiplier for this system's detection and engagement range.");
                ModuleRange[definition.Id].Value = Mathf.Clamp(ModuleRange[definition.Id].Value, 0.1f, 3f);
                ModuleReaction[definition.Id] = config.Bind(section, "ReactionMultiplier", 1f, "Multiplier for this system's reaction time.");
                ModuleReaction[definition.Id].Value = Mathf.Clamp(ModuleReaction[definition.Id].Value, 0.1f, 5f);
                ModuleIntercept[definition.Id] = config.Bind(section, "InterceptMultiplier", 1f, "Multiplier for this system's intercept probability.");
                ModuleIntercept[definition.Id].Value = Mathf.Clamp01(ModuleIntercept[definition.Id].Value);
                if (definition.Type == APSType.SoftKill) continue;
                ModuleSpeedBased[definition.Id] = config.Bind(section, "SpeedBasedInterception", true, "Use projectile speed instead of threat-class eligibility for hard-kill. Intercept probability still applies.");
                ModuleMaxSpeed[definition.Id] = config.Bind(section, "MaxInterceptSpeed", definition.MaxEngagementSpeed, "Maximum hard-kill projectile speed in m/s; must be finite and positive.");
                ModuleMaxSpeed[definition.Id].Value = MaxInterceptSpeed(definition);
                ModuleMinSpeed[definition.Id] = config.Bind(section, "MinInterceptSpeed", Math.Min(50f, definition.MinEngagementSpeed), "Minimum hard-kill projectile speed in m/s; must be finite, nonnegative and no greater than maximum.");
                ModuleMinSpeed[definition.Id].Value = Math.Min(MinInterceptSpeed(definition), MaxInterceptSpeed(definition));
                if (ConfigDebugMode.Value)
                    Main.ModLog.LogInfo($"[SAAPS] {definition.Name}: speedBased={SpeedBasedInterception(definition)} speed={MinInterceptSpeed(definition):F0}..{MaxInterceptSpeed(definition):F0}m/s capacity={definition.CountermeasureCapacity}");
            }
        }

        internal static bool IsModuleEnabled(string id) => !ModuleEnabled.TryGetValue(id, out var entry) || entry.Value;
        internal static float ModuleRangeMultiplier(string id) => ModuleRange.TryGetValue(id, out var entry) ? entry.Value : 1f;
        internal static float ModuleReactionMultiplier(string id) => ModuleReaction.TryGetValue(id, out var entry) ? entry.Value : 1f;
        internal static float ModuleInterceptMultiplier(string id) => ModuleIntercept.TryGetValue(id, out var entry) ? entry.Value : 1f;

        internal void Update()
        {
            if (!ConfigEnabled.Value) return;
            
            // Handle keybinds
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current[ConfigToggleHUD.Value].wasPressedThisFrame)
                {
                    ConfigShowHUD.Value = !ConfigShowHUD.Value;
                    _log.LogInfo($"[SAAPS] HUD toggled: {ConfigShowHUD.Value}");
                }
                
                if (UnityEngine.InputSystem.Keyboard.current[ConfigToggleDebug.Value].wasPressedThisFrame)
                {
                    ConfigDebugMode.Value = !ConfigDebugMode.Value;
                    _log.LogInfo($"[SAAPS] Debug mode toggled: {ConfigDebugMode.Value}");
                }
            }

            foreach (var vehicle in _dirtyVehicles.Values)
            {
                if (vehicle == null || vehicle.Released) continue;
                if (_vehicleStates.TryGetValue(vehicle.Pointer, out var state)) state.SyncModules();
                else GetOrCreateVehicleState(vehicle);
            }
            _dirtyVehicles.Clear();

            if (Time.time >= _nextVehicleDiscovery)
            {
                _nextVehicleDiscovery = Time.time + 2f;
                foreach (var pair in _vehicleStates.ToArray())
                    if (pair.Value.Vehicle == null || pair.Value.Vehicle.Released) RemoveVehicleState(pair.Key);
                foreach (var vehicle in UnityEngine.Object.FindObjectsOfType<Vehicle>())
                {
                    if (vehicle == null || vehicle.Released) continue;
                    var ptr = vehicle.Pointer;
                    if (_vehicleStates.TryGetValue(ptr, out var current))
                    {
                        if (current.Modules.Count > 0) continue;
                        RemoveVehicleState(ptr);
                    }
                    var state = new APSVehicleState(vehicle, _log);
                    if (ConfigDebugMode.Value && (state.Modules.Count > 0 || _reportedEmptyVehicles.Add(ptr)))
                        _log.LogInfo($"[SAAPS] Vehicle scan 0x{ptr:X}: found {state.Modules.Count} APS module(s); {SprocketAdvancedAPS.APSPartHooks.DescribeComponents(vehicle)}.");
                    if (state.Modules.Count > 0)
                    {
                        _vehicleStates[ptr] = state;
                        _reportedEmptyVehicles.Remove(ptr);
                    }
                    else state.Dispose();
                }
            }
        }

        internal void Shutdown()
        {
            foreach (var state in _vehicleStates.Values)
            {
                state.Dispose();
            }
            _vehicleStates.Clear();
            _reportedEmptyVehicles.Clear();
            _dirtyVehicles.Clear();
            Instance = null;
        }
        
        // Runtime APS stays inactive until native weapon-component discovery is verified.
        internal APSVehicleState GetOrCreateVehicleState(Vehicle vehicle)
        {
            var ptr = vehicle.Pointer;
            if (!_vehicleStates.TryGetValue(ptr, out var state))
            {
                state = new APSVehicleState(vehicle, _log);
                _vehicleStates[ptr] = state;
            }
            return state;
        }
        
        internal bool TryGetVehicleState(Vehicle vehicle, out APSVehicleState state)
        {
            return _vehicleStates.TryGetValue(vehicle.Pointer, out state!);
        }

        internal void RemoveVehicleState(IntPtr ptr)
        {
            if (_vehicleStates.Remove(ptr, out var state)) state.Dispose();
        }
        
        internal bool TryGetVehicleState(IntPtr ptr, out APSVehicleState state)
        {
            return _vehicleStates.TryGetValue(ptr, out state!);
        }
        
        internal IReadOnlyDictionary<IntPtr, APSVehicleState> VehicleStates => _vehicleStates;
    }
    
    internal sealed class APSVehicleState : IDisposable
    {
        internal readonly Vehicle Vehicle;
        internal readonly List<APSModuleState> Modules = new();
        internal readonly APSSensorArray SensorArray;
        internal readonly APSThreatTracker ThreatTracker;
        internal readonly APSEngagementPlanner EngagementPlanner;
        internal readonly APSCountermeasureManager CountermeasureManager;
        internal bool IsActive => Vehicle != null && !Vehicle.Released && Modules.Count > 0;
        
        private readonly ManualLogSource _log;
        
        internal APSVehicleState(Vehicle vehicle, ManualLogSource log)
        {
            Vehicle = vehicle;
            _log = log;
            
            SensorArray = new APSSensorArray(this, log);
            ThreatTracker = new APSThreatTracker(this, log);
            EngagementPlanner = new APSEngagementPlanner(this, log);
            CountermeasureManager = new APSCountermeasureManager(this, log);
            
            DiscoverModules();
            
            if (Modules.Count > 0)
            {
                _log.LogInfo($"[SAAPS] Vehicle 0x{vehicle.Pointer:X} initialized with {Modules.Count} APS module(s).");
            }
        }

        private void DiscoverModules()
        {
            foreach (var component in SprocketAdvancedAPS.APSPartHooks.SelectedComponents(Vehicle))
            {
                var definition = SprocketAdvancedAPS.Parts.APSPartRegistry.GetModuleDefinition(component);
                if (definition != null)
                    Modules.Add(new APSModuleState(this, component, definition));
            }
        }
        
        internal void SyncModules()
        {
            var components = SprocketAdvancedAPS.APSPartHooks.SelectedComponents(Vehicle).ToArray();
            for (int i = Modules.Count - 1; i >= 0; i--)
                if (!components.Any(c => c.Pointer == Modules[i].Component.Pointer &&
                    ReferenceEquals(SprocketAdvancedAPS.Parts.APSPartRegistry.GetModuleDefinition(c), Modules[i].Definition)))
                {
                    var old = Modules[i];
                    foreach (var track in old.AssignedThreats) { track.IsEngaged = false; track.EngagingModule = null; }
                    var component = components.FirstOrDefault(c => c.Pointer == old.Component.Pointer);
                    var definition = component == null ? null : SprocketAdvancedAPS.Parts.APSPartRegistry.GetModuleDefinition(component);
                    if (component != null && definition != null)
                    {
                        var replacement = new APSModuleState(this, component, definition);
                        replacement.KeepRuntimeState(old);
                        Modules[i] = replacement;
                    }
                    else Modules.RemoveAt(i);
                    old.Dispose();
                }
            foreach (var component in components)
                if (!Modules.Any(m => m.Component.Pointer == component.Pointer))
                {
                    var definition = SprocketAdvancedAPS.Parts.APSPartRegistry.GetModuleDefinition(component);
                    if (definition != null) Modules.Add(new APSModuleState(this, component, definition));
                }
        }

        private Rigidbody? _body;
        private bool _bodyResolved;
        internal Vector3 Velocity;
        internal Vector3 Position;

        internal void RefreshMotion()
        {
            Position = Vehicle.transform.position;
            // Resolve once per vehicle; a body destroyed later is re-resolved on the next tick.
            if (!_bodyResolved)
            {
                _body = Vehicle.GetComponent<Rigidbody>() ?? Vehicle.GetComponentInParent<Rigidbody>();
                _bodyResolved = true;
            }
            if (_body == null)
            {
                Velocity = Vector3.zero;
                return;
            }
            try
            {
                Velocity = _body.velocity;
            }
            catch
            {
                _body = null;
                _bodyResolved = false;
                Velocity = Vector3.zero;
            }
        }

        internal void Update()
        {
            if (!IsActive) return;
            // Render frames only recheck pending launches; scan, sort and assignments stay in physics.
            EngagementPlanner.FixedUpdate();
        }
        
        internal void FixedUpdate()
        {
            if (!IsActive) return;
            
            RefreshMotion();
            foreach (var module in Modules) module.Update(Time.fixedDeltaTime);
            SensorArray.Update();
            ThreatTracker.Update(SensorArray.DetectedThreats);
            EngagementPlanner.Update(ThreatTracker.ActiveThreats);
            EngagementPlanner.FixedUpdate();
            CountermeasureManager.FixedUpdate();
        }
        
        public void Dispose()
        {
            CountermeasureManager?.Dispose();
            EngagementPlanner?.Dispose();
            ThreatTracker?.Dispose();
            SensorArray?.Dispose();
            
            foreach (var module in Modules)
            {
                module.Dispose();
            }
            Modules.Clear();
        }
    }
}