using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using Sprocket.DamageModelling;
using Sprocket.ProjectilePhysicsSystems;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.Weapons.Cannons;
using SprocketAdvancedAPS.Core;
using UnityEngine;

namespace SprocketAdvancedAPS.Systems
{
    [HarmonyPatch]
    internal static class ProjectileTracker
    {
        private static readonly Dictionary<IntPtr, ProjectileRegister> _registers = new();
        private static readonly Dictionary<(IntPtr Register, int Id), IntPtr> _sources = new();

        internal static IEnumerable<ProjectileRegister> ActiveRegisters => _registers.Values;
        internal readonly record struct Sample(Vector3 Position, Vector3 Velocity, ProjectileInstance Native);
        internal static readonly Dictionary<(IntPtr Register, int Id), Sample> Snapshot = new();
        internal static int NativeReads;
        private static float _nextSnapshotWarning;

        internal static bool TryGetRegister(IntPtr pointer, out ProjectileRegister register) => _registers.TryGetValue(pointer, out register!);

        internal static void Capture()
        {
            Snapshot.Clear();
            NativeReads = 0;
            foreach (var register in _registers.Values)
            {
                try
                {
                    var map = register.activeIdMap;
                    var pool = register.pool;
                    foreach (var pair in map)
                    {
                        var projectile = pool[pair.Value];
                        NativeReads++;
                        if (!APSInterceptRules.IsLiveNative((int)projectile.flags)) continue;
                        Snapshot[(register.Pointer, pair.Key)] = new Sample(projectile.position, projectile.velocity, projectile);
                    }
                }
                catch (Exception ex)
                {
                    if (Time.time < _nextSnapshotWarning) continue;
                    _nextSnapshotWarning = Time.time + 5f;
                    Main.ModLog.LogWarning("[SAAPS] Projectile snapshot failed: " + ex.Message);
                }
            }
        }

        internal static void Clear()
        {
            Snapshot.Clear();
            _sources.Clear();
            _registers.Clear();
        }

        internal static IntPtr GetSource(IntPtr register, int projectileId) =>
            _sources.TryGetValue((register, projectileId), out var src) ? src : IntPtr.Zero;

        [HarmonyPostfix, HarmonyPatch(typeof(ProjectileRegister), nameof(ProjectileRegister.Launch))]
        private static void OnLaunch(ProjectileRegister __instance, Il2CppSystem.Object source, ref ProjectileFireInfo fireInfo)
        {
            if (__instance == null) return;
            _registers[__instance.Pointer] = __instance;
            try
            {
                var weapon = source?.TryCast<CannonBehaviour>();
                var vehicle = weapon?.mount?.TryCast<Cannon>()?.Vehicle;
                var vehiclePtr = vehicle?.Behaviour?.Pointer ?? IntPtr.Zero;
                _sources[(__instance.Pointer, fireInfo.ProjectileID)] = vehiclePtr;
            }
            catch { }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ProjectileRegister), nameof(ProjectileRegister.FixedUpdate))]
        private static void OnFixedUpdate(ProjectileRegister __instance)
        {
            if (__instance != null) _registers[__instance.Pointer] = __instance;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ProjectileRegister), nameof(ProjectileRegister.Release))]
        private static void OnRelease(ProjectileRegister __instance, int __0)
        {
            if (__instance != null)
            {
                _sources.Remove((__instance.Pointer, __0));
                Snapshot.Remove((__instance.Pointer, __0));
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ProjectileRegister), nameof(ProjectileRegister.DestroyAll))]
        private static void OnDestroyAll(ProjectileRegister __instance)
        {
            if (__instance == null) return;
            foreach (var key in _sources.Keys.Where(key => key.Register == __instance.Pointer).ToArray()) _sources.Remove(key);
            _registers.Remove(__instance.Pointer);
        }
    }

    /// <summary>
    /// Classifies projectiles into threat classes. Adapter to the native
    /// Sprocket projectile system. Determines owner/faction, velocity,
    /// trajectory, and projectile type from the ProjectileInstance.
    /// </summary>
    internal static class APSThreatClassifier
    {
        /// <summary>
        /// Classify a native projectile into a threat class.
        /// </summary>
        internal static bool Classify(ProjectileInstance projectile, out ThreatClass threatClass, out GuidanceType guidance, out bool isFriendly, IntPtr selfVehiclePtr, IntPtr registerPtr)
        {
            threatClass = ThreatClass.None;
            guidance = GuidanceType.None;
            isFriendly = false;

            try
            {
                var definition = projectile.Definition;
                if (definition == null) return false;

                string defName = definition.Name;

                if (IsAtgmBehaviour(defName))
                {
                    threatClass = DetectGuidance(defName) switch
                    {
                        GuidanceType.IRHoming or GuidanceType.RadarHoming or GuidanceType.GPS_INS or GuidanceType.ImageRecognition => ThreatClass.ATGM_FireAndForget,
                        GuidanceType.MCLOS => ThreatClass.ATGM_MCLOS,
                        _ => ThreatClass.ATGM_SACLOS
                    };
                    guidance = DetectGuidance(defName);
                }
                else if (IsApfsdsBehaviour(defName))
                {
                    threatClass = ThreatClass.APFSDS;
                    guidance = GuidanceType.None;
                }
                else if (IsHeatBehaviour(defName))
                {
                    threatClass = ThreatClass.HEAT;
                    guidance = GuidanceType.None;
                }
                else if (IsHighExplosiveBehaviour(defName))
                {
                    threatClass = ThreatClass.HE | ThreatClass.CannonHE;
                    guidance = GuidanceType.None;
                }
                else if (IsRocketBehaviour(defName))
                {
                    threatClass = ThreatClass.RPG;
                    guidance = GuidanceType.None;
                }
                else
                {
                    threatClass = ThreatClass.HE;
                    guidance = GuidanceType.None;
                }

                // Determine ownership
                isFriendly = IsFriendlyProjectile(registerPtr, projectile.ID, selfVehiclePtr);

                return threatClass != ThreatClass.None;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsAtgmBehaviour(string name)
        {
            var s = name.ToLowerInvariant();
            return s.Contains("atgm") || s.Contains("missile") || s.Contains("tog") ||
                   s.Contains("hellfire") || s.Contains("javelin") || s.Contains("spike") ||
                   s.Contains("metis") || s.Contains("konkurs") || s.Contains("kornet") ||
                   s.Contains("malyutka") || s.Contains("dragon") || s.Contains("brimstone");
        }

        private static bool IsApfsdsBehaviour(string name)
        {
            var s = name.ToLowerInvariant();
            return s.Contains("apfsds") || s.Contains("apds") || s.Contains("kinetic") ||
                   s.Contains("penetrator") || s.Contains("sabot");
        }

        private static bool IsHeatBehaviour(string name)
        {
            var s = name.ToLowerInvariant();
            return s.Contains("heat") || s.Contains("hoh") || s.Contains("shaped");
        }

        private static bool IsHighExplosiveBehaviour(string name)
        {
            var s = name.ToLowerInvariant();
            return s.Contains("high explosive") || s.Contains("frag") || s.Contains("saphe") || s.Contains("hesh");
        }

        private static bool IsRocketBehaviour(string name)
        {
            var s = name.ToLowerInvariant();
            return s.Contains("rpg") || s.Contains("rocket") || s.Contains("panzerfaust") ||
                   s.Contains("bazooka") || s.Contains("rpg-7") || s.Contains("rpz");
        }

        private static GuidanceType DetectGuidance(string name)
        {
            var s = name.ToLowerInvariant();
            if (s.Contains("saclos") || s.Contains("semi-auto")) return GuidanceType.SACLOS;
            if (s.Contains("mcloc") || s.Contains("manual")) return GuidanceType.MCLOS;
            if (s.Contains("fire") && s.Contains("forget")) return GuidanceType.IRHoming;
            if (s.Contains("laser")) return GuidanceType.LaserHoming;
            if (s.Contains("radar")) return GuidanceType.RadarHoming;
            if (s.Contains("wire")) return GuidanceType.WireGuided;
            if (s.Contains("gps") || s.Contains("ins")) return GuidanceType.GPS_INS;
            // Default ATGM guidance is SACLOS in Sprocket
            return GuidanceType.SACLOS;
        }

        internal static bool IsFriendlyProjectile(IntPtr registerPtr, int projectileId, IntPtr selfVehiclePtr)
        {
            try
            {
                var sourceVehiclePtr = ProjectileTracker.GetSource(registerPtr, projectileId);
                return sourceVehiclePtr != IntPtr.Zero && sourceVehiclePtr == selfVehiclePtr;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a projectile is guided (soft-kill targetable).
        /// </summary>
        internal static bool IsGuided(ThreatClass tc)
        {
            return (tc & (ThreatClass.ATGM_SACLOS | ThreatClass.ATGM_MCLOS | ThreatClass.ATGM_FireAndForget |
                          ThreatClass.TopAttack | ThreatClass.UAV_FPV | ThreatClass.LoiteringMunition)) != 0;
        }

        /// <summary>
        /// Estimate projectile speed from native velocity.
        /// </summary>
        internal static float GetSpeed(ProjectileInstance projectile)
        {
            try
            {
                return projectile.velocity.magnitude;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Estimate projectile position from native instance.
        /// </summary>
        internal static Vector3 GetPosition(ProjectileInstance projectile)
        {
            try
            {
                return projectile.position;
            }
            catch
            {
                return Vector3.zero;
            }
        }
    }

    /// <summary>
    /// Sensor array: scans for projectiles in the world and builds
    /// detection reports for the threat tracker. Uses the native
    /// ProjectileRegister so detection is frame-rate independent.
    /// </summary>
    internal sealed class APSSensorArray : IDisposable
    {
        private readonly APSVehicleState _vehicleState;
        private readonly ManualLogSource _log;
        private readonly List<ThreatTrack> _detectedThreats = new();
        private readonly HashSet<(IntPtr Register, int Id)> _knownProjectiles = new();
        private readonly HashSet<(IntPtr Register, int Id)> _visibleProjectiles = new();
        private readonly List<APSModuleState> _detectingModules = new();
        private const float _scanInterval = 0.02f;
        private float _nextRejectLog;
        private float _nextScanTime;
        private bool _disposed;

        internal APSSensorArray(APSVehicleState vehicleState, ManualLogSource log)
        {
            _vehicleState = vehicleState;
            _log = log;
        }

        internal IReadOnlyList<ThreatTrack> DetectedThreats => _detectedThreats;

        internal void Update()
        {
            if (_disposed) return;
            if (!APSCore.ConfigEnabled.Value) return;

            // Only scan at fixed interval to avoid per-frame allocation
            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + _scanInterval;

            _visibleProjectiles.Clear();
            ScanProjectiles();
            PruneStaleTracks();
            _knownProjectiles.RemoveWhere(id => !_visibleProjectiles.Contains(id));
        }

        private void ScanProjectiles()
        {
            var vehicle = _vehicleState.Vehicle;
            if (vehicle == null || vehicle.Released) return;

            var vehiclePos = _vehicleState.Position;
            float maxRange = 0f;
            foreach (var module in _vehicleState.Modules)
                if (module.IsEnabled && module.IsOperational && !module.IsDestroyed && APSCore.IsModuleEnabled(module.Definition.Id))
                    maxRange = Mathf.Max(maxRange, module.Definition.DetectionRange * APSCore.ConfigGlobalDetectionMultiplier.Value * APSCore.ModuleRangeMultiplier(module.Definition.Id));

            foreach (var pair in ProjectileTracker.Snapshot)
            {
                    var projectileKey = pair.Key;
                    int projectileId = projectileKey.Id;
                    _visibleProjectiles.Add(projectileKey);
                    if (_knownProjectiles.Contains(projectileKey)) continue;
                    var sample = pair.Value;
                    if ((sample.Position - vehiclePos).sqrMagnitude > maxRange * maxRange) continue;
                    var projectile = sample.Native;

                    // Classify
                    if (!APSThreatClassifier.Classify(projectile, out var threatClass, out var guidance, out var isFriendly, vehicle.Pointer, projectileKey.Register))
                    {
                        _knownProjectiles.Add(projectileKey);
                        continue;
                    }

                    // Skip friendly projectiles (our own shots)
                    if (isFriendly)
                    {
                        _knownProjectiles.Add(projectileKey);
                        continue;
                    }

                    float speed = sample.Velocity.magnitude;
                    // Eligibility uses each system's configured speed window.
                    bool detectable = false;
                    foreach (var module in _vehicleState.Modules)
                    {
                        if (!module.IsEnabled || !APSCore.IsModuleEnabled(module.Definition.Id) || !module.IsOperational || module.IsDestroyed) continue;
                        if (!module.SupportsThreat(threatClass, speed, detection: true)) continue;
                        detectable = true;
                        break;
                    }
                    if (!detectable)
                    {
                        if (APSCore.ConfigDebugMode.Value && Time.time >= _nextRejectLog)
                        {
                            _nextRejectLog = Time.time + 5f;
                            _log.LogInfo($"[SAAPS] REJECT speed/class/inactive '{projectile.Definition?.Name}' class={threatClass} speed={speed:F1}m/s vehicle=0x{vehicle.Pointer:X}");
                        }
                        // Retry: deceleration or a config change can make this projectile eligible.
                        continue;
                    }

                    // Check detection range
                    var position = sample.Position;
                    float distance = Vector3.Distance(position, vehiclePos);

                    // Determine which modules can see it
                    _detectingModules.Clear();
                    foreach (var module in _vehicleState.Modules)
                    {
                        if (!module.IsEnabled || !APSCore.IsModuleEnabled(module.Definition.Id) || !module.IsOperational || module.IsDestroyed) continue;
                        if (!module.SupportsThreat(threatClass, speed, detection: true)) continue;
                        if (distance > module.Definition.DetectionRange * APSCore.ConfigGlobalDetectionMultiplier.Value * APSCore.ModuleRangeMultiplier(module.Definition.Id)) continue;

                        // Check FOV
                        if (!IsInModuleFOV(module, position, vehicle)) continue;

                        _detectingModules.Add(module);
                    }

                    if (_detectingModules.Count == 0) continue;

                    // Create threat track
                    var track = new ThreatTrack
                    {
                        ProjectileId = projectileId,
                        ProjectilePtr = projectileKey.Register,
                        ThreatClass = threatClass,
                        ProjectileType = projectile.Definition?.Name ?? "unknown",
                        Position = position,
                        Velocity = sample.Velocity,
                        Distance = distance,
                        DetectionTime = Time.time,
                        HasGuidance = guidance != GuidanceType.None,
                        GuidanceType = guidance,
                        GuidanceStrength = 1f,
                        SourceVehiclePtr = ProjectileTracker.GetSource(projectileKey.Register, projectileId),
                        IsFriendly = false
                    };
                    track.Update(position, sample.Velocity, vehiclePos);

                    _detectedThreats.Add(track);
                    _knownProjectiles.Add(projectileKey);

                    if (APSCore.ConfigDebugMode.Value)
                        _log.LogInfo($"[SAAPS] DETECTED {threatClass} '{track.ProjectileType}' id={projectileId} speed={speed:F1}m/s dist={distance:F1}m tti={track.TimeToImpact:F2}s");
            }
        }

        private bool IsInModuleFOV(APSModuleState module, Vector3 worldPos, Vehicle vehicle)
        {
            // Transform to module local space
            var moduleTransform = module.Transform;
            var localPoint = moduleTransform.InverseTransformDirection(worldPos - moduleTransform.position);
            if (!float.IsFinite(localPoint.x) || !float.IsFinite(localPoint.y) || !float.IsFinite(localPoint.z)) return false;

            // Horizontal FOV check
            var horizontalDir = new Vector3(localPoint.x, 0, localPoint.z).normalized;
            float horizontalAngle = Vector3.Angle(horizontalDir, Vector3.forward);

            float pitch = Mathf.Atan2(localPoint.y, new Vector2(localPoint.x, localPoint.z).magnitude) * Mathf.Rad2Deg;
            return horizontalAngle <= module.Definition.DetectionFOVHorizontal * 0.5f &&
                Mathf.Abs(pitch) <= module.Definition.DetectionFOVVertical * 0.5f;
        }

        private void PruneStaleTracks()
        {
            // Remove tracks whose projectile is no longer active
            var vehicle = _vehicleState.Vehicle;
            if (vehicle == null || vehicle.Released) return;

            for (int i = _detectedThreats.Count - 1; i >= 0; i--)
            {
                var track = _detectedThreats[i];
                if (IsProjectileGone(track))
                {
                    // Remove from module assignments
                    foreach (var module in _vehicleState.Modules)
                        module.AssignedThreats.Remove(track);
                    _detectedThreats.RemoveAt(i);
                }
            }
        }

        private bool IsProjectileGone(ThreatTrack track) => !ProjectileTracker.Snapshot.ContainsKey((track.ProjectilePtr, track.ProjectileId));

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _detectedThreats.Clear();
            _knownProjectiles.Clear();
        }
    }
}