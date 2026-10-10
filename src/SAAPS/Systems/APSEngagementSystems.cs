using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Sprocket.DamageModelling;
using Sprocket.ProjectilePhysicsSystems;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Cannons;
using Sprocket.Vehicles.Weapons.Cannons;
using SprocketAdvancedAPS.Core;
using UnityEngine;

namespace SprocketAdvancedAPS.Systems
{
    /// <summary>
    /// Threat tracker: predicts impact points, tracks projectile
    /// trajectories, and computes time-to-impact for each threat.
    /// </summary>
    internal sealed class APSThreatTracker : IDisposable
    {
        private readonly APSVehicleState _vehicleState;
        private readonly ManualLogSource _log;
        private readonly List<ThreatTrack> _activeThreats = new();
        private bool _disposed;

        internal APSThreatTracker(APSVehicleState vehicleState, ManualLogSource log)
        {
            _vehicleState = vehicleState;
            _log = log;
        }

        internal IReadOnlyList<ThreatTrack> ActiveThreats => _activeThreats;

        internal void Update(IReadOnlyList<ThreatTrack> detectedThreats)
        {
            if (_disposed) return;

            _activeThreats.Clear();
            var vehicle = _vehicleState.Vehicle;
            if (vehicle == null || vehicle.Released) return;

            var vehiclePos = _vehicleState.Position;
            var vehicleVelocity = _vehicleState.Velocity;

            foreach (var track in detectedThreats)
            {
                if (!ProjectileTracker.Snapshot.TryGetValue((track.ProjectilePtr, track.ProjectileId), out var sample)) continue;
                track.Position = sample.Position;
                track.Velocity = sample.Velocity;
                track.RelativeVelocity = sample.Velocity - vehicleVelocity;

                // Update prediction with vehicle motion
                PredictImpact(track, vehiclePos, vehicleVelocity);

                // Recalculate priority
                track.ThreatPriority = track.CalculatePriority();

                _activeThreats.Add(track);
            }

            // Sort by priority descending
            _activeThreats.Sort((a, b) => b.ThreatPriority.CompareTo(a.ThreatPriority));
        }

        internal static bool Refresh(ThreatTrack track, Vehicle vehicle, Vector3 vehicleVelocity)
        {
            if (vehicle == null || vehicle.Released || track.IsFriendly ||
                APSThreatClassifier.IsFriendlyProjectile(track.ProjectilePtr, track.ProjectileId, vehicle.Pointer)) return false;
            if (!ProjectileTracker.TryGetRegister(track.ProjectilePtr, out var register)) return false;
            {
                if (!register.activeIdMap.TryGetValue(track.ProjectileId, out int index)) return false;

                ProjectileInstance projectile;
                try
                {
                    projectile = register.pool[index];
                }
                catch { return false; }

                if (!APSInterceptRules.IsLiveNative((int)projectile.flags)) return false;

                track.Position = projectile.position;
                track.Velocity = projectile.velocity;
                track.RelativeVelocity = track.Velocity - vehicleVelocity;
                return true;
            }
        }

        private void PredictImpact(ThreatTrack track, Vector3 vehiclePos, Vector3 vehicleVelocity)
        {
            // Relative velocity (projectile relative to vehicle)
            Vector3 relativeVelocity = track.Velocity - vehicleVelocity;
            Vector3 toVehicle = vehiclePos - track.Position;

            float closingSpeed = Vector3.Dot(toVehicle.normalized, relativeVelocity);

            // Time to closest approach
            if (closingSpeed > 1f)
            {
                float distanceToVehicle = toVehicle.magnitude;
                track.TimeToImpact = distanceToVehicle / closingSpeed;
            }
            else
            {
                // Not closing - estimate from speed
                float speed = track.Velocity.magnitude;
                if (speed > 1f)
                    track.TimeToImpact = Vector3.Distance(track.Position, vehiclePos) / speed;
                else
                    track.TimeToImpact = float.MaxValue;
            }

            // Predicted impact point: where projectile will be at TTI
            // Use the projectile's current velocity (not relative) for the world prediction
            if (float.IsFinite(track.TimeToImpact) && track.TimeToImpact < 60f)
            {
                track.PredictedImpactPoint = track.Position + track.Velocity * track.TimeToImpact;
            }
            else
            {
                track.PredictedImpactPoint = vehiclePos;
                track.TimeToImpact = float.MaxValue;
            }

            track.Distance = Vector3.Distance(track.Position, vehiclePos);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _activeThreats.Clear();
        }
    }

    /// <summary>
    /// Engagement planner: decides which module engages which threat,
    /// when to fire, applying reaction delay and engagement rules.
    /// </summary>
    internal sealed class APSEngagementPlanner : IDisposable
    {
        private readonly APSVehicleState _vehicleState;
        private readonly ManualLogSource _log;
        private bool _disposed;

        private float _nextRejectLog;

        internal APSEngagementPlanner(APSVehicleState vehicleState, ManualLogSource log)
        {
            _vehicleState = vehicleState;
            _log = log;
        }

        internal void Update(IReadOnlyList<ThreatTrack> activeThreats)
        {
            if (_disposed) return;
            if (!APSCore.ConfigEnabled.Value) return;

            // Rebuild in priority order so pending distant targets cannot reserve a launcher indefinitely.
            foreach (var module in _vehicleState.Modules)
            {
                foreach (var assigned in module.AssignedThreats)
                {
                    assigned.IsEngaged = false;
                    assigned.EngagingModule = null;
                }
                module.AssignedThreats.Clear();
            }

            foreach (var threat in activeThreats)
            {

                // Find best module for this threat. A soft-kill system carries no intercept
                // probability to score with, so it competes on its jam probability instead;
                // a hard-kill module that is free still wins and removes the round outright.
                APSModuleState? bestModule = null;
                float bestScore = float.MinValue;
                APSModuleState? bestJamModule = null;
                float bestJamScore = float.MinValue;
                APSModuleState? bestInEnvelope = null;
                float bestInEnvelopeScore = float.MinValue;
                APSModuleState? bestInEnvelopeJam = null;
                float bestInEnvelopeJamScore = float.MinValue;

                foreach (var module in _vehicleState.Modules)
                {
                    if (!module.CanPrepare(threat) || CountModuleEngagements(module) >= module.Definition.MaxSimultaneousEngagements) continue;

                    // Simultaneous-engagement slots are scarce: a round already inside the
                    // envelope can be shot this tick, one still 90 m out cannot. Reserve a
                    // slot for the former only when no module can fire right now.
                    bool inEnvelope = module.IsThreatInEngagementEnvelope(threat);

                    if (module.Definition.Type == APSType.SoftKill)
                    {
                        float jamScore = module.GetSoftKillProbability(threat.ThreatClass) * 1000f + module.RemainingCountermeasures * 10f;
                        if (jamScore > bestJamScore)
                        {
                            bestJamScore = jamScore;
                            bestJamModule = module;
                        }
                        if (inEnvelope && jamScore > bestInEnvelopeJamScore)
                        {
                            bestInEnvelopeJamScore = jamScore;
                            bestInEnvelopeJam = module;
                        }
                    }
                    else
                    {
                        float score = ScoreModuleForThreat(module, threat);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestModule = module;
                        }
                        if (inEnvelope && score > bestInEnvelopeScore)
                        {
                            bestInEnvelopeScore = score;
                            bestInEnvelope = module;
                        }
                    }
                }
                bestModule = bestInEnvelope ?? bestModule;
                bestJamModule = bestInEnvelopeJam ?? bestJamModule;
                bestModule ??= bestJamModule;

                if (bestModule == null && APSCore.ConfigDebugMode.Value && Time.time >= _nextRejectLog)
                {
                    _nextRejectLog = Time.time + 5f;
                    var status = string.Join(";", _vehicleState.Modules.Select(m => $"{m.Definition.Id}:{m.Definition.Type},eligible={m.SupportsThreat(threat.ThreatClass, threat.Velocity.magnitude)},ammo={m.RemainingCountermeasures},cooldown={m.CooldownRemaining:F2},operational={m.IsOperational && !m.IsDestroyed}"));
                    _log.LogInfo($"[SAAPS] NO ENGAGEMENT id={threat.ProjectileId} class={threat.ThreatClass} speed={threat.Velocity.magnitude:F1}m/s dist={threat.Distance:F1}m {status}");
                }
                if (bestModule != null)
                {
                    // Check simultaneous engagement limit
                    int moduleEngagements = CountModuleEngagements(bestModule);
                    if (moduleEngagements < bestModule.Definition.MaxSimultaneousEngagements)
                    {
                        threat.EngagingModule = bestModule;
                        threat.IsEngaged = true;
                        bestModule.AssignedThreats.Add(threat);

                        if (APSCore.ConfigDebugMode.Value && Time.time >= threat.NextDecisionLog)
                        {
                            threat.NextDecisionLog = Time.time + 2f;
                            _log.LogInfo($"[SAAPS] ENGAGE {threat.ThreatClass} -> {bestModule.Definition.Name} id={threat.ProjectileId} speed={threat.Velocity.magnitude:F1}m/s limit={APSCore.MaxInterceptSpeed(bestModule.Definition):F0}m/s detectedAgo={Time.time - threat.DetectionTime:F3}s tti={threat.TimeToImpact:F2}s");
                        }
                    }
                }
            }
        }

        private float ScoreModuleForThreat(APSModuleState module, ThreatTrack threat)
        {
            float score = 0f;

            // Coverage factor
            foreach (var sector in module.CoverageSectors)
            {
                score += sector.GetCoverageFactor(threat.PredictedImpactPoint) * 100f;
            }

            // Intercept probability
            score += module.GetInterceptProbability(threat.ThreatClass) * 1000f;

            // Prefer modules with more countermeasures
            score += module.RemainingCountermeasures * 10f;

            // Prefer modules with lower cooldown
            score -= module.CooldownRemaining * 100f;

            // Prefer closer engagement
            if (threat.Distance > 0f)
                score += (1f / threat.Distance) * 10f;

            return score;
        }

        private int CountModuleEngagements(APSModuleState module)
        {
            int count = 0;
            foreach (var track in module.AssignedThreats)
            {
                if (track.IsEngaged) count++;
            }
            return count;
        }

        internal void FixedUpdate()
        {
            if (_disposed || !APSCore.ConfigEnabled.Value) return;

            // Apply reaction delay and fire countermeasures
            foreach (var module in _vehicleState.Modules)
            {
                for (int i = module.AssignedThreats.Count - 1; i >= 0; i--)
                {
                    var threat = module.AssignedThreats[i];
                    if (!threat.IsEngaged || threat.EngagingModule != module) continue;

                    if (!APSThreatTracker.Refresh(threat, _vehicleState.Vehicle, _vehicleState.Velocity) || !module.CanPrepare(threat))
                    {
                        module.AssignedThreats.RemoveAt(i);
                        threat.IsEngaged = false;
                        threat.EngagingModule = null;
                        continue;
                    }
                    float reactionTime = module.Definition.ReactionTime * APSCore.ConfigGlobalReactionMultiplier.Value * APSCore.ModuleReactionMultiplier(module.Definition.Id);
                    // Report the failing gate separately: a shared throttle with ENGAGE hid both of these.
                    string? reason = !module.IsThreatInEngagementEnvelope(threat) ? "envelope" :
                        !APSInterceptRules.ReactionSatisfied(Time.time, threat.DetectionTime, reactionTime, threat.TimeToImpact) ? "reaction" : null;
                    if (reason != null)
                    {
                        if (APSCore.ConfigDebugMode.Value && Time.time >= threat.NextWaitLog)
                        {
                            threat.NextWaitLog = Time.time + 2f;
                            _log.LogInfo($"[SAAPS] WAIT {module.Definition.Name} id={threat.ProjectileId} reason={reason} speed={threat.Velocity.magnitude:F1}m/s dist={threat.Distance:F1}m elapsed={Time.time - threat.DetectionTime:F3}s reaction={reactionTime:F3}s tti={threat.TimeToImpact:F2}s");
                        }
                        continue;
                    }
                    if (module.Definition.Type == APSType.HardKill)
                        APSHardKillSystem.Instance?.FireCountermeasure(module, threat);
                    else
                    {
                        // A hybrid system launches first; the round is gone on success, so jam only
                        // whatever survives. A soft-kill-only system just jams.
                        if (module.Definition.Type == APSType.Hybrid)
                            APSHardKillSystem.Instance?.FireCountermeasure(module, threat);
                        if (module.RemainingCountermeasures > 0)
                            APSSoftKillSystem.Instance?.ApplySoftKill(module, threat);
                    }
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// Hard-kill system: fires countermeasures that physically
    /// affect projectiles in the world.
    /// </summary>
    internal sealed class APSHardKillSystem : IDisposable
    {
        internal static APSHardKillSystem? Instance { get; private set; }
        private readonly ManualLogSource _log;
        private bool _disposed;

        internal APSHardKillSystem(ManualLogSource log)
        {
            Instance = this;
            _log = log;
        }

        internal void FireCountermeasure(APSModuleState module, ThreatTrack threat)
        {
            if (_disposed) return;

            // Refresh native state immediately before consuming ammo or releasing a projectile.
            if (!APSCore.ConfigEnabled.Value || !APSThreatTracker.Refresh(threat, module.Vehicle, module.VehicleState.Velocity) || !module.CanEngage(threat) ||
                !APSInterceptRules.ReactionSatisfied(Time.time, threat.DetectionTime,
                    module.Definition.ReactionTime * APSCore.ConfigGlobalReactionMultiplier.Value * APSCore.ModuleReactionMultiplier(module.Definition.Id),
                    threat.TimeToImpact)) return;

            if (module.RemainingCountermeasures <= 0)
            {
                if (APSCore.ConfigDebugMode.Value)
                    _log.LogWarning($"[SAAPS] {module.Definition.Name} out of countermeasures!");
                return;
            }

            // Consume countermeasure
            module.ConsumeCountermeasure();

            // Compute intercept probability
            float interceptProb = module.GetInterceptProbability(threat.ThreatClass);
            float roll = UnityEngine.Random.value;

            bool success = roll < interceptProb;

            // Fire visual/audio effects from module position
            APSVisualManager.Instance?.FireInterceptEffect(module, threat, success);
            APSAudioManager.Instance?.PlayLaunchSound(module);

            if (success)
            {
                // Physically affect the projectile
                bool affected = InterceptProjectile(module, threat);

                if (affected)
                {
                    if (APSCore.ConfigDebugMode.Value)
                        _log.LogInfo($"[SAAPS] INTERCEPT SUCCESS {module.Definition.Name} destroyed {threat.ThreatClass} id={threat.ProjectileId}");
                }
                else
                {
                    // Could not affect the projectile - treat as failed intercept
                    if (APSCore.ConfigDebugMode.Value)
                        _log.LogWarning($"[SAAPS] INTERCEPT FAILED - could not affect projectile id={threat.ProjectileId}");
                }
            }
            else
            {
                if (APSCore.ConfigDebugMode.Value)
                    _log.LogInfo($"[SAAPS] INTERCEPT MISS {module.Definition.Name} probability={interceptProb:P0} roll={roll:P0}");
            }

            // Remove from module assignments after engagement
            module.AssignedThreats.Remove(threat);
            threat.IsEngaged = false;
            threat.EngagingModule = null;
        }

        private bool InterceptProjectile(APSModuleState module, ThreatTrack threat)
        {
            // Release through the native register, the same path the game uses for
            // expired projectiles, so collision/damage state stays consistent.
            if (!ProjectileTracker.TryGetRegister(threat.ProjectilePtr, out var register) || register == null)
                return false;

            if (!register.activeIdMap.TryGetValue(threat.ProjectileId, out int index))
                return false;

            ProjectileInstance projectile;
            try
            {
                projectile = register.pool[index];
            }
            catch
            {
                return false;
            }

            if (!APSInterceptRules.IsLiveNative((int)projectile.flags)) return false;

            var interceptPosition = projectile.position;
            var interceptVelocity = projectile.velocity;

            try
            {
                register.Release(threat.ProjectileId);
                bool released = !register.activeIdMap.TryGetValue(threat.ProjectileId, out int remainingIndex);
                if (!released) released = !APSInterceptRules.IsLiveNative((int)register.pool[remainingIndex].flags);
                if (released) APSVisualManager.Instance?.SpawnInterceptExplosion(register, interceptPosition, interceptVelocity);
                return released;
            }
            catch (Exception ex)
            {
                _log.LogError($"[SAAPS] Failed to release projectile: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            Instance = null;
        }
    }

    /// <summary>
    /// Soft-kill system: jams guidance of guided projectiles,
    /// reducing their accuracy rather than destroying them.
    /// </summary>
    internal sealed class APSSoftKillSystem : IDisposable
    {
        internal static APSSoftKillSystem? Instance { get; private set; }
        private readonly ManualLogSource _log;
        private readonly Dictionary<(IntPtr Register, int Id), JammingState> _jammedProjectiles = new();
        private bool _disposed;

        internal APSSoftKillSystem(ManualLogSource log)
        {
            Instance = this;
            _log = log;
        }

        internal void ApplySoftKill(APSModuleState module, ThreatTrack threat)
        {
            if (_disposed) return;

            // Only soft-kill/hybrid systems jam, and only guided rounds can lose a lock.
            if (!APSCore.ConfigEnabled.Value || !APSThreatClassifier.IsGuided(threat.ThreatClass) ||
                !APSThreatTracker.Refresh(threat, module.Vehicle, module.VehicleState.Velocity) ||
                !module.CanEngage(threat) || !module.IsInJamEnvelope(threat.Position)) return;
            if (!APSInterceptRules.JamReady(module.Definition.Type != APSType.HardKill, module.IsEnabled && APSCore.IsModuleEnabled(module.Definition.Id),
                module.IsOperational, module.IsDestroyed, module.Health, module.RemainingCountermeasures, module.CooldownRemaining, threat.IsFriendly)) return;

            float jamProb = module.GetSoftKillProbability(threat.ThreatClass);
            if (jamProb <= 0f) return;

            if (UnityEngine.Random.value >= jamProb) return;

            module.ConsumeCountermeasure();

            // Keyed by register + id: the native ID counter restarts per register.
            _jammedProjectiles[(threat.ProjectilePtr, threat.ProjectileId)] = new JammingState
            {
                ProjectileId = threat.ProjectileId,
                ProjectilePtr = threat.ProjectilePtr,
                Module = module,
                Threat = threat,
                StartTime = Time.time,
                // Hold the jam long enough to walk the round off the aim line.
                Duration = module.Definition.ReactionTime + 3f,
                Strength = jamProb,
                VehiclePosition = module.VehicleState.Position,
                GuidanceType = threat.GuidanceType
            };

            // Visual/audio feedback
            APSAudioManager.Instance?.PlayJamSound(module);

            if (APSCore.ConfigDebugMode.Value)
                _log.LogInfo($"[SAAPS] SOFT-KILL JAM {module.Definition.Name} on {threat.ThreatClass} guidance={threat.GuidanceType} prob={jamProb:P0}");
        }

        internal void FixedUpdate()
        {
            if (_disposed) return;

            // Apply jamming to projectiles
            var toRemove = new List<(IntPtr Register, int Id)>();

            foreach (var kvp in _jammedProjectiles)
            {
                var state = kvp.Value;

                // Check duration
                if (Time.time - state.StartTime > state.Duration)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Track the protected vehicle: a round is walked off the line to the vehicle, not a fixed point.
                var vehicle = state.Module.Vehicle;
                if (vehicle == null || vehicle.Released || !APSCore.ConfigEnabled.Value ||
                    !APSInterceptRules.JamHeld(state.Module.IsEnabled && APSCore.IsModuleEnabled(state.Module.Definition.Id),
                        state.Module.IsOperational, state.Module.IsDestroyed, state.Module.Health, state.Threat.IsFriendly))
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }
                state.VehiclePosition = state.Module.VehicleState.Position;

                if (!APSThreatTracker.Refresh(state.Threat, vehicle, state.Module.VehicleState.Velocity) ||
                    !state.Module.IsInJamEnvelope(state.Threat.Position))
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }
                // Apply guidance disruption to the projectile
                DisruptGuidance(state);
            }

            foreach (var id in toRemove)
            {
                _jammedProjectiles.Remove(id);
            }
        }

        private void DisruptGuidance(JammingState state)
        {
            if (!ProjectileTracker.TryGetRegister(state.ProjectilePtr, out var register) || register == null)
                return;
            if (!register.activeIdMap.TryGetValue(state.ProjectileId, out int index))
                return;

            ProjectileInstance projectile;
            try
            {
                projectile = register.pool[index];
            }
            catch
            {
                return;
            }

            if (!APSInterceptRules.IsLiveNative((int)projectile.flags)) return;

            // The native instance carries no guidance state, only position/velocity, so jamming
            // is modelled the way real soft-kill behaves: walk the round off its aim line until
            // SACLOS loses the lock. Speed is preserved; the round stays a real projectile.
            Vector3 currentVelocity = projectile.velocity;
            float speed = currentVelocity.magnitude;
            if (speed < 1f) return;

            Vector3 toVehicle = state.VehiclePosition - projectile.position;
            if (toVehicle.sqrMagnitude < 0.01f) return;

            Vector3 currentDirection = currentVelocity / speed;

            // Walk the round onto a line that no longer intersects the vehicle. A sideways
            // component keeps it curving off the aim line instead of snapping straight back.
            Vector3 lineOfSight = toVehicle.normalized;
            Vector3 sideways = Vector3.Cross(currentDirection, lineOfSight);
            if (sideways.sqrMagnitude < 1e-4f) sideways = Vector3.Cross(currentDirection, Vector3.up);
            sideways.Normalize();

            Vector3 escapeDirection = (-lineOfSight + sideways * 0.6f).normalized;
            float weight = APSInterceptRules.DeflectionWeight(APSInterceptRules.DeflectionRate(state.Strength), Time.fixedDeltaTime);
            if (weight <= 0f) return;

            Vector3 deflected = Vector3.Slerp(currentDirection, escapeDirection, weight).normalized;

            // A jam must never aim the round back at the vehicle it is trying to escape.
            if (Vector3.Dot(deflected, lineOfSight) < 0f) return;

            projectile.velocity = deflected * speed;
            register.pool[index] = projectile;
        }

        public void Dispose()
        {
            _disposed = true;
            _jammedProjectiles.Clear();
            Instance = null;
        }

        private sealed class JammingState
        {
            internal int ProjectileId;
            internal IntPtr ProjectilePtr;
            internal APSModuleState Module = null!;
            internal ThreatTrack Threat = null!;
            internal float StartTime;
            internal float Duration;
            internal float Strength;
            internal Vector3 VehiclePosition;
            internal GuidanceType GuidanceType;
        }
    }

    /// <summary>
    /// Countermeasure manager: tracks countermeasure inventory,
    /// reload state, and module health.
    /// </summary>
    internal sealed class APSCountermeasureManager : IDisposable
    {
        private readonly APSVehicleState _vehicleState;
        private readonly ManualLogSource _log;
        private bool _disposed;

        internal APSCountermeasureManager(APSVehicleState vehicleState, ManualLogSource log)
        {
            _vehicleState = vehicleState;
            _log = log;
        }

        internal void Update()
        {
            if (_disposed) return;

            foreach (var module in _vehicleState.Modules)
            {
                module.Update(Time.deltaTime);
            }
        }

        internal void FixedUpdate()
        {
            if (_disposed) return;

            // Reload countermeasures where applicable
            foreach (var module in _vehicleState.Modules)
            {
                if (module.IsDestroyed || !module.IsOperational || !module.IsEnabled) continue;

                var def = module.Definition;
                if (def.ReloadTime > 0f && module.RemainingCountermeasures < def.CountermeasureCapacity)
                {
                    float timeSinceLastFire = Time.time - module.LastFireTime;
                    if (timeSinceLastFire >= def.ReloadTime)
                    {
                        module.RemainingCountermeasures = def.CountermeasureCapacity;
                        if (APSCore.ConfigDebugMode.Value)
                            _log.LogInfo($"[SAAPS] RELOAD {def.Name} to {def.CountermeasureCapacity} countermeasures");
                    }
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}