using System;
using System.Collections.Generic;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AttachedBehaviours;
using SprocketAdvancedAPS.Core;
using UnityEngine;

namespace SprocketAdvancedAPS.Systems
{
    /// <summary>
    /// Enumeration of APS types/kinds
    /// </summary>
    public enum APSType
    {
        HardKill,
        SoftKill,
        Hybrid
    }

    /// <summary>
    /// Enumeration of threat classes that APS can engage
    /// </summary>
    [Flags]
    public enum ThreatClass
    {
        None = 0,
        RPG = 1 << 0,           // Unguided rockets (RPG-7, etc.)
        ATGM_SACLOS = 1 << 1,   // SACLOS guided missiles
        ATGM_MCLOS = 1 << 2,    // MCLOS guided missiles
        ATGM_FireAndForget = 1 << 3, // Fire-and-forget missiles (Javelin, etc.)
        HEAT = 1 << 4,          // HEAT rounds
        HE = 1 << 5,            // High explosive rounds
        APFSDS = 1 << 6,        // Kinetic penetrators
        CannonHE = 1 << 7,      // Cannon HE shells
        TopAttack = 1 << 8,     // Top-attack munitions
        UAV_FPV = 1 << 9,       // UAV/FPV drones
        LoiteringMunition = 1 << 10, // Loitering munitions
        All = ~0
    }

    /// <summary>
    /// APS module definition - describes a specific APS system type
    /// </summary>
    public sealed class APSDefinition
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string Country { get; init; } = "";
        public int YearIntroduced { get; init; } = 0;
        public APSType Type { get; init; } = APSType.HardKill;
        
        // Detection capabilities
        public float DetectionRange { get; init; } = 50f;
        public float DetectionFOVHorizontal { get; init; } = 360f; // degrees
        public float DetectionFOVVertical { get; init; } = 60f; // degrees
        public float MinDetectionSpeed { get; init; } = 50f; // m/s
        public float MaxDetectionSpeed { get; init; } = 2000f; // m/s
        public float ReactionTime { get; init; } = 0.05f; // seconds
        
        // Engagement capabilities
        public ThreatClass SupportedThreats { get; init; } = ThreatClass.RPG | ThreatClass.ATGM_SACLOS | ThreatClass.ATGM_MCLOS;
        public float EngagementRangeMin { get; init; } = 2f;
        public float EngagementRangeMax { get; init; } = 30f;
        public float EngagementFOVHorizontal { get; init; } = 360f;
        public float EngagementFOVVertical { get; init; } = 60f;
        public float MinEngagementSpeed { get; init; } = 50f;
        public float MaxEngagementSpeed { get; init; } = 1500f;
        
        // Countermeasures
        public int CountermeasureCapacity { get; init; } = 4;
        public float ReloadTime { get; init; } = 0f; // 0 = no reload (single use per battle)
        public float CooldownBetweenShots { get; init; } = 0.5f;
        public bool CanEngageMultiple { get; init; } = true;
        public int MaxSimultaneousEngagements { get; init; } = 2;
        
        // Effectiveness (gameplay approximations)
        public Dictionary<ThreatClass, float> InterceptProbability { get; init; } = new();
        public Dictionary<ThreatClass, float> SoftKillProbability { get; init; } = new();
        
        // Physical properties
        public float MassKg { get; init; } = 100f;
        public float Cost { get; init; } = 50000f;
        public float PowerConsumptionKw { get; init; } = 1f;
        
        // Module layout
        public string[] RequiredModuleTypes { get; init; } = Array.Empty<string>();
        public string[] OptionalModuleTypes { get; init; } = Array.Empty<string>();
        
        // Visual/audio
        public string RadarModelPath { get; init; } = "";
        public string LauncherModelPath { get; init; } = "";
        public string SensorModelPath { get; init; } = "";
        public string InterceptEffectPrefab { get; init; } = "";
        public string LaunchSound { get; init; } = "";
        public string RadarSound { get; init; } = "";
        
        // Balance flags
        public bool IsGameplayApproximation { get; init; } = true;
        public string[] Notes { get; init; } = Array.Empty<string>();
        
        // Preset modes
        public Dictionary<string, APSDefinition> Presets { get; init; } = new();
    }

    /// <summary>
    /// Individual APS module instance on a vehicle
    /// </summary>
    internal sealed class APSModuleState : IDisposable
    {
        public readonly string ModuleId;
        public readonly APSDefinition Definition;
        public readonly VehicleComponent Component;
        internal readonly Vehicle Vehicle;
        internal readonly APSVehicleState VehicleState;
        public readonly Transform Transform;
        
        // Runtime state
        public int RemainingCountermeasures;
        public float LastFireTime;
        public float CooldownRemaining;
        public bool IsEnabled = true;
        public bool IsOperational = true;
        public bool IsDestroyed = false;
        public float Health = 1f;
        public float MaxHealth = 1f;
        
        // Sensor state
        public bool IsScanning = false;
        public List<ThreatTrack> AssignedThreats = new();
        
        // Coverage sectors (for visualization)
        public List<CoverageSector> CoverageSectors = new();
        
        public APSModuleState(APSVehicleState vehicleState, VehicleComponent component, APSDefinition definition)
        {
            ModuleId = $"{definition.Id}_{component.Pointer.ToInt64():X}";
            Definition = definition;
            Component = component;
            Vehicle = vehicleState.Vehicle;
            VehicleState = vehicleState;
            Transform = component.transform;
            
            RemainingCountermeasures = definition.CountermeasureCapacity;
            MaxHealth = 1f;
            Health = 1f;
            
            // Initialize coverage sectors based on module position and definition
            InitializeCoverageSectors(vehicleState);
        }
        
        private void InitializeCoverageSectors(APSVehicleState vehicleState)
        {
            // Calculate coverage based on module position relative to vehicle center
            var localPos = vehicleState.Vehicle.transform.InverseTransformPoint(Transform.position);
            var forward = Vector3.forward;
            
            // Create a coverage sector for this module
            var sector = new CoverageSector
            {
                CenterDirection = forward,
                HorizontalFOV = Definition.EngagementFOVHorizontal,
                VerticalFOV = Definition.EngagementFOVVertical,
                MinRange = Definition.EngagementRangeMin,
                MaxRange = Definition.EngagementRangeMax,
                LocalPosition = localPos,
                Module = this
            };
            CoverageSectors.Add(sector);
        }
        
        public bool SupportsThreat(ThreatClass threatClass, float speed, bool detection = false)
        {
            bool speedBased = APSCore.SpeedBasedInterception(Definition);
            return APSInterceptRules.Eligible(speed,
                speedBased ? APSCore.MinInterceptSpeed(Definition) : detection ? Definition.MinDetectionSpeed : Definition.MinEngagementSpeed,
                speedBased ? APSCore.MaxInterceptSpeed(Definition) : detection ? Definition.MaxDetectionSpeed : Definition.MaxEngagementSpeed,
                (Definition.SupportedThreats & threatClass) != 0, speedBased);
        }

        public bool CanPrepare(ThreatTrack threat)
        {
            // Hard-kill systems must be able to release a round; soft-kill and hybrid ones may
            // also qualify for jamming, which the planner dispatches separately.
            if (!APSInterceptRules.Ready(Definition.Type != APSType.SoftKill, IsEnabled && APSCore.IsModuleEnabled(Definition.Id),
                IsOperational, IsDestroyed, Health, RemainingCountermeasures, CooldownRemaining, threat.IsFriendly) &&
                !APSInterceptRules.JamReady(Definition.Type != APSType.HardKill, IsEnabled && APSCore.IsModuleEnabled(Definition.Id),
                    IsOperational, IsDestroyed, Health, RemainingCountermeasures, CooldownRemaining, threat.IsFriendly))
                return false;
            if (!SupportsThreat(threat.ThreatClass, threat.Velocity.magnitude)) return false;
            var toModule = Transform.position - threat.Position;
            float rangeMultiplier = APSCore.ModuleRangeMultiplier(Definition.Id);
            float distance = toModule.magnitude;
            if (distance < Definition.EngagementRangeMin * rangeMultiplier ||
                distance > Definition.DetectionRange * APSCore.ConfigGlobalDetectionMultiplier.Value * rangeMultiplier) return false;
            if (!APSInterceptRules.Approaching(toModule.sqrMagnitude, Vector3.Dot(toModule, threat.RelativeVelocity),
                threat.RelativeVelocity.sqrMagnitude, Definition.EngagementRangeMax * rangeMultiplier)) return false;
            foreach (var sector in CoverageSectors)
                if (sector.ContainsDirection(threat.Position)) return true;
            return false;
        }

        internal bool IsInJamEnvelope(Vector3 position)
        {
            var direction = Transform.InverseTransformDirection(position - Transform.position);
            float distance = (position - Transform.position).magnitude;
            float multiplier = APSCore.ModuleRangeMultiplier(Definition.Id);
            return Definition.Type != APSType.HardKill && float.IsFinite(distance) &&
                distance >= Definition.EngagementRangeMin * multiplier &&
                distance <= Definition.EngagementRangeMax * multiplier &&
                APSInterceptRules.InCone(direction.x, direction.y, direction.z,
                    APSInterceptRules.JamHorizontalFOV, APSInterceptRules.JamVerticalFOV);
        }

        public bool CanEngage(ThreatTrack threat) => CanPrepare(threat) && IsThreatInEngagementEnvelope(threat);

        public bool IsThreatInEngagementEnvelope(ThreatTrack threat)
        {
            // Gate against projectile's current position; predicted impact usually lies inside vehicle hull.
            foreach (var sector in CoverageSectors)
            {
                if (sector.Contains(threat.Position))
                    return true;
            }
            // A round already inside the minimum stand-off is the most dangerous one there is.
            // Sector.Contains rejects it on range alone, but nothing is gained by waiting it out.
            if (CoverageSectors.Count == 0) return false;
            var toModule = threat.Position - Transform.position;
            if (APSInterceptRules.InsideMinRange(toModule.sqrMagnitude, Definition.EngagementRangeMin * APSCore.ModuleRangeMultiplier(Definition.Id)))
            {
                foreach (var sector in CoverageSectors)
                    if (sector.ContainsDirection(threat.Position)) return true;
            }
            return false;
        }
        
        public float GetInterceptProbability(ThreatClass threatClass)
        {
            // Speed mode must not keep a hidden zero-probability class veto.
            float fallback = threatClass switch
            {
                ThreatClass.RPG => 0.85f,
                ThreatClass.ATGM_SACLOS => 0.75f,
                ThreatClass.ATGM_MCLOS => 0.80f,
                ThreatClass.ATGM_FireAndForget => 0.40f,
                ThreatClass.HEAT => 0.70f,
                ThreatClass.HE => 0.60f,
                ThreatClass.APFSDS => 0.10f, // Very low, only for advanced systems
                ThreatClass.CannonHE => 0.50f,
                ThreatClass.TopAttack => 0.30f,
                ThreatClass.UAV_FPV => 0.60f,
                ThreatClass.LoiteringMunition => 0.50f,
                _ => 0.50f
                };
            float configured = Definition.InterceptProbability.TryGetValue(threatClass, out var value) ? value : fallback;
            float probability = APSInterceptRules.Probability(configured, fallback, APSCore.SpeedBasedInterception(Definition));
            return Mathf.Clamp01(probability * APSCore.ModuleInterceptMultiplier(Definition.Id));
        }

        public float GetSoftKillProbability(ThreatClass threatClass)
        {
            if (Definition.SoftKillProbability.TryGetValue(threatClass, out var prob))
                return prob;
            
            // Soft-kill only works on guided threats
            if (threatClass.HasFlag(ThreatClass.ATGM_SACLOS) || 
                threatClass.HasFlag(ThreatClass.ATGM_MCLOS) ||
                threatClass.HasFlag(ThreatClass.ATGM_FireAndForget) ||
                threatClass.HasFlag(ThreatClass.TopAttack) ||
                threatClass.HasFlag(ThreatClass.UAV_FPV) ||
                threatClass.HasFlag(ThreatClass.LoiteringMunition))
            {
                return 0.60f;
            }
            return 0f;
        }
        
        public void ConsumeCountermeasure()
        {
            RemainingCountermeasures = Math.Max(0, RemainingCountermeasures - 1);
            LastFireTime = Time.time;
            CooldownRemaining = Definition.CooldownBetweenShots;
        }
        
        public void Update(float deltaTime)
        {
            if (CooldownRemaining > 0)
                CooldownRemaining -= deltaTime;
            
            if (Health <= 0 && !IsDestroyed)
            {
                IsDestroyed = true;
                IsOperational = false;
            }
        }
        
        public void TakeDamage(float damage)
        {
            Health = Math.Max(0, Health - damage);
            if (Health < 0.3f)
                IsOperational = false;
        }
        
        public void Dispose()
        {
            AssignedThreats.Clear();
            CoverageSectors.Clear();
        }
    }

    /// <summary>
    /// Coverage sector for a module
    /// </summary>
    public sealed class CoverageSector
    {
        public Vector3 CenterDirection;
        public float HorizontalFOV;
        public float VerticalFOV;
        public float MinRange;
        public float MaxRange;
        public Vector3 LocalPosition;
        internal APSModuleState Module = null!;
        
        public bool Contains(Vector3 worldPoint)
        {
            float distance = Vector3.Distance(Module.Transform.position, worldPoint);
            float multiplier = APSCore.ModuleRangeMultiplier(Module.Definition.Id);
            return float.IsFinite(distance) && distance >= MinRange * multiplier && distance <= MaxRange * multiplier && ContainsDirection(worldPoint);
        }

        internal bool ContainsDirection(Vector3 worldPoint)
        {
            var localPoint = Module.Transform.InverseTransformDirection(worldPoint - Module.Transform.position);
            if (!float.IsFinite(localPoint.x) || !float.IsFinite(localPoint.y) || !float.IsFinite(localPoint.z)) return false;
            if (CenterDirection == Vector3.forward)
                return APSInterceptRules.InCone(localPoint.x, localPoint.y, localPoint.z, HorizontalFOV, VerticalFOV);

            // Check horizontal angle
            var horizontalDir = new Vector3(localPoint.x, 0, localPoint.z).normalized;
            var horizontalCenter = new Vector3(CenterDirection.x, 0, CenterDirection.z).normalized;
            float horizontalAngle = Vector3.Angle(horizontalDir, horizontalCenter);
            if (horizontalAngle > HorizontalFOV * 0.5f) return false;
            
            // Check vertical angle
            float pitch = Mathf.Atan2(localPoint.y, new Vector2(localPoint.x, localPoint.z).magnitude) * Mathf.Rad2Deg;
            float centerPitch = Mathf.Atan2(CenterDirection.y, new Vector2(CenterDirection.x, CenterDirection.z).magnitude) * Mathf.Rad2Deg;
            if (Mathf.Abs(pitch - centerPitch) > VerticalFOV * 0.5f) return false;
            
            return true;
        }
        
        public float GetCoverageFactor(Vector3 worldPoint)
        {
            if (!Contains(worldPoint)) return 0f;
            
            var moduleTransform = Module.Transform;
            var localPoint = moduleTransform.InverseTransformPoint(worldPoint);
            float distance = localPoint.magnitude;
            
            // Coverage decreases with distance
            float rangeFactor = 1f - (distance - MinRange) / (MaxRange - MinRange);
            rangeFactor = Mathf.Clamp01(rangeFactor);
            
            // Coverage decreases with angle from center
            var horizontalDir = new Vector3(localPoint.x, 0, localPoint.z).normalized;
            var horizontalCenter = new Vector3(CenterDirection.x, 0, CenterDirection.z).normalized;
            float horizontalAngle = Vector3.Angle(horizontalDir, horizontalCenter);
            float horizontalFactor = 1f - (horizontalAngle / (HorizontalFOV * 0.5f));
            
            var verticalDir = new Vector3(localPoint.x, localPoint.y, localPoint.z).normalized;
            float verticalAngle = Vector3.Angle(verticalDir, CenterDirection);
            float verticalFactor = 1f - (verticalAngle / (VerticalFOV * 0.5f));
            
            return rangeFactor * horizontalFactor * verticalFactor;
        }
    }

    /// <summary>
    /// Tracked threat (incoming projectile)
    /// </summary>
    public sealed class ThreatTrack
    {
        public int ProjectileId;
        public IntPtr ProjectilePtr;
        public ThreatClass ThreatClass;
        public string ProjectileType = "unknown";
        public Vector3 Position;
        public Vector3 Velocity;
        internal Vector3 RelativeVelocity;
        internal float NextDecisionLog;
        internal float NextWaitLog;
        public Vector3 PredictedImpactPoint;
        public float TimeToImpact;
        public float Distance;
        public float ThreatPriority;
        public bool IsEngaged = false;
        internal APSModuleState? EngagingModule = null;
        public float DetectionTime;
        public IntPtr SourceVehiclePtr;
        public bool IsFriendly = false;
        
        // Guidance info for soft-kill
        public bool HasGuidance = false;
        public GuidanceType GuidanceType = GuidanceType.None;
        public float GuidanceStrength = 1f;
        
        public void Update(Vector3 position, Vector3 velocity, Vector3 vehiclePosition)
        {
            Position = position;
            Velocity = velocity;
            Distance = Vector3.Distance(position, vehiclePosition);
            
            // Simple linear prediction
            if (velocity.magnitude > 0.1f)
            {
                TimeToImpact = Distance / velocity.magnitude;
                PredictedImpactPoint = position + velocity.normalized * Distance;
            }
            else
            {
                TimeToImpact = float.MaxValue;
                PredictedImpactPoint = position;
            }
            
            // Calculate threat priority (higher = more urgent)
            ThreatPriority = CalculatePriority();
        }
        
        internal float CalculatePriority()
        {
            float priority = 0f;
            
            // Closer = higher priority
            priority += 1000f / Mathf.Max(1f, Distance);
            
            // Faster time to impact = higher priority
            if (TimeToImpact > 0 && TimeToImpact < 10f)
                priority += 500f / TimeToImpact;
            
            // Threat class weighting
            priority *= ThreatClass switch
            {
                ThreatClass.APFSDS => 1.5f,
                ThreatClass.ATGM_FireAndForget => 1.3f,
                ThreatClass.TopAttack => 1.2f,
                ThreatClass.ATGM_SACLOS => 1.1f,
                ThreatClass.RPG => 1.0f,
                ThreatClass.HEAT => 1.0f,
                ThreatClass.HE => 0.8f,
                ThreatClass.CannonHE => 0.8f,
                ThreatClass.UAV_FPV => 0.9f,
                ThreatClass.LoiteringMunition => 1.0f,
                _ => 0.8f
            };
            
            return priority;
        }
    }

    /// <summary>
    /// Guidance types for soft-kill targeting
    /// </summary>
    public enum GuidanceType
    {
        None,
        SACLOS,         // Semi-Automatic Command to Line of Sight
        MCLOS,          // Manual Command to Line of Sight
        LaserBeamRiding,
        LaserHoming,
        IRHoming,
        RadarHoming,
        GPS_INS,
        ImageRecognition,
        WireGuided,
        RadioCommand
    }
}