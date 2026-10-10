using System;
using System.Collections.Generic;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AttachedBehaviours;
using UnityEngine;

namespace SprocketAdvancedAPS.Systems
{
    /// <summary>
    /// Registry of all APS definitions. Each system has distinct detection,
    /// engagement, and countermeasure characteristics based on public sources.
    /// All effectiveness values marked IsGameplayApproximation are gameplay
    /// approximations, not confirmed real-world performance.
    /// </summary>
    internal static class APSDefinitions
    {
        private static readonly Dictionary<string, APSDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        internal static IReadOnlyDictionary<string, APSDefinition> All => _definitions;

        internal static void LoadAll()
        {
            if (_loaded) return;
            _loaded = true;

            RegisterDrozd1();
            RegisterDrozd2();
            RegisterArena();
            RegisterArenaM();
            RegisterAfganit();
            RegisterShtora();
            RegisterTrophy();
            RegisterIronFist();
            RegisterAmapAds();
            RegisterZaslin();
        }

        internal static bool TryGet(string id, out APSDefinition definition)
        {
            return _definitions.TryGetValue(id, out definition!);
        }

        internal static bool TryGetForComponent(string componentId, out APSDefinition definition)
        {
            var found = SprocketAdvancedAPS.Parts.APSPartRegistry.GetModuleDefinition(componentId);
            if (found != null)
            {
                definition = found;
                return true;
            }
            definition = null!;
            return false;
        }

        /// <summary>
        /// Register a custom player-created preset.
        /// </summary>
        internal static void RegisterCustomPreset(APSDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id)) return;
            _definitions[definition.Id] = definition;
        }

        // --- DROZD-1 (Soviet, hard-kill) ---
        private static void RegisterDrozd1()
        {
            var d = new APSDefinition
            {
                Id = "drozd1",
                Name = "Drozd-1",
                Description = "Soviet hard-kill APS with forward-facing launchers. Protects the turret front arc only.",
                Country = "USSR",
                YearIntroduced = 1982,
                Type = APSType.HardKill,
                DetectionRange = 60f,
                DetectionFOVHorizontal = 120f,
                DetectionFOVVertical = 40f,
                MinDetectionSpeed = 70f,
                MaxDetectionSpeed = 600f,
                ReactionTime = 0.09f,
                SupportedThreats = ThreatClass.RPG | ThreatClass.HEAT | ThreatClass.ATGM_SACLOS | ThreatClass.ATGM_MCLOS,
                EngagementRangeMin = 3f,
                EngagementRangeMax = 12f,
                EngagementFOVHorizontal = 100f,
                EngagementFOVVertical = 35f,
                MinEngagementSpeed = 70f,
                MaxEngagementSpeed = 600f,
                CountermeasureCapacity = 4,
                ReloadTime = 0f,
                CooldownBetweenShots = 0.8f,
                CanEngageMultiple = false,
                MaxSimultaneousEngagements = 1,
                MassKg = 110f,
                Cost = 42000f,
                PowerConsumptionKw = 0.8f,
                RadarModelPath = "Models/drozd_radar",
                LauncherModelPath = "Models/drozd_launcher",
                SensorModelPath = "Models/drozd_sensor",
                InterceptEffectPrefab = "Effects/drozd_intercept",
                LaunchSound = "Audio/drozd_launch",
                RadarSound = "Audio/drozd_radar",
                IsGameplayApproximation = true,
                Notes = new[]
                {
                    "Coverage depends on launcher placement; rear arc unprotected.",
                    "Finite interceptor supply; no reload during battle.",
                    "Effective against WWII-era and Cold War RPGs/ATGM.",
                    "All effectiveness values are gameplay approximations."
                }
            };
            d.InterceptProbability[ThreatClass.RPG] = 0.88f;
            d.InterceptProbability[ThreatClass.HEAT] = 0.82f;
            d.InterceptProbability[ThreatClass.ATGM_SACLOS] = 0.70f;
            d.InterceptProbability[ThreatClass.ATGM_MCLOS] = 0.72f;
            d.InterceptProbability[ThreatClass.APFSDS] = 0f;
            _definitions[d.Id] = d;
        }

        // --- DROZD-2 (Soviet/Russian, hard-kill) ---
        private static void RegisterDrozd2()
        {
            var d = new APSDefinition
            {
                Id = "drozd2",
                Name = "Drozd-2",
                Description = "Improved Drozd with wider coverage, better target management and multiple launcher clusters.",
                Country = "USSR/Russia",
                YearIntroduced = 1995,
                Type = APSType.HardKill,
                DetectionRange = 80f,
                DetectionFOVHorizontal = 180f,
                DetectionFOVVertical = 45f,
                MinDetectionSpeed = 60f,
                MaxDetectionSpeed = 900f,
                ReactionTime = 0.07f,
                SupportedThreats = ThreatClass.RPG | ThreatClass.HEAT | ThreatClass.ATGM_SACLOS | ThreatClass.ATGM_MCLOS | ThreatClass.CannonHE,
                EngagementRangeMin = 3f,
                EngagementRangeMax = 18f,
                EngagementFOVHorizontal = 170f,
                EngagementFOVVertical = 40f,
                MinEngagementSpeed = 60f,
                MaxEngagementSpeed = 900f,
                CountermeasureCapacity = 6,
                ReloadTime = 0f,
                CooldownBetweenShots = 0.55f,
                CanEngageMultiple = true,
                MaxSimultaneousEngagements = 2,
                MassKg = 145f,
                Cost = 58000f,
                PowerConsumptionKw = 1.1f,
                RadarModelPath = "Models/drozd2_radar",
                LauncherModelPath = "Models/drozd2_launcher",
                SensorModelPath = "Models/drozd2_sensor",
                InterceptEffectPrefab = "Effects/drozd2_intercept",
                LaunchSound = "Audio/drozd2_launch",
                RadarSound = "Audio/drozd2_radar",
                IsGameplayApproximation = true,
                Notes = new[]
                {
                    "Supports multiple launcher clusters on the turret.",
                    "Improved target tracking over Drozd-1.",
                    "Rear coverage still depends on module placement.",
                    "All effectiveness values are gameplay approximations."
                }
            };
            d.InterceptProbability[ThreatClass.RPG] = 0.90f;
            d.InterceptProbability[ThreatClass.HEAT] = 0.86f;
            d.InterceptProbability[ThreatClass.ATGM_SACLOS] = 0.78f;
            d.InterceptProbability[ThreatClass.ATGM_MCLOS] = 0.80f;
            d.InterceptProbability[ThreatClass.CannonHE] = 0.55f;
            d.InterceptProbability[ThreatClass.APFSDS] = 0f;
            _definitions[d.Id] = d;
        }

        // --- ARENA (Russian, hard-kill) ---
        private static void RegisterArena()
        {
            var d = new APSDefinition
            {
                Id = "arena",
                Name = "Arena",
                Description = "Russian hard-kill APS with multi-direction radar and interceptor boxes around the turret.",
                Country = "Russia",
                YearIntroduced = 1996,
                Type = APSType.HardKill,
                DetectionRange = 100f,
                DetectionFOVHorizontal = 300f,
                DetectionFOVVertical = 50f,
                MinDetectionSpeed = 70f,
                MaxDetectionSpeed = 1200f,
                ReactionTime = 0.06f,
                SupportedThreats = ThreatClass.RPG | ThreatClass.HEAT | ThreatClass.ATGM_SACLOS | ThreatClass.ATGM_MCLOS | ThreatClass.HE,
                EngagementRangeMin = 4f,
                EngagementRangeMax = 25f,
                EngagementFOVHorizontal = 290f,
                EngagementFOVVertical = 45f,
                MinEngagementSpeed = 70f,
                MaxEngagementSpeed = 1200f,
                CountermeasureCapacity = 8,
                ReloadTime = 0f,
                CooldownBetweenShots = 0.45f,
                CanEngageMultiple = true,
                MaxSimultaneousEngagements = 2,
                MassKg = 180f,
                Cost = 75000f,
                PowerConsumptionKw = 1.6f,
                RadarModelPath = "Models/arena_radar",
                LauncherModelPath = "Models/arena_launcher",
                SensorModelPath = "Models/arena_sensor",
                InterceptEffectPrefab = "Effects/arena_intercept",
                LaunchSound = "Audio/arena_launch",
                RadarSound = "Audio/arena_radar",
                IsGameplayApproximation = true,
                Notes = new[]
                {
                    "Radar tracks multiple approach directions.",
                    "Distinguishes incoming munitions from non-threats.",
                    "Blind spot depends on launcher installation.",
                    "All effectiveness values are gameplay approximations."
                }
            };
            d.InterceptProbability[ThreatClass.RPG] = 0.92f;
            d.InterceptProbability[ThreatClass.HEAT] = 0.88f;
            d.InterceptProbability[ThreatClass.ATGM_SACLOS] = 0.82f;
            d.InterceptProbability[ThreatClass.ATGM_MCLOS] = 0.84f;
            d.InterceptProbability[ThreatClass.HE] = 0.65f;
            d.InterceptProbability[ThreatClass.APFSDS] = 0f;
            _definitions[d.Id] = d;
        }

        // --- ARENA-M (Russian, hard-kill, modular) ---
        private static void RegisterArenaM()
        {
            var d = new APSDefinition
            {
                Id = "arenam",
                Name = "Arena-M",
                Description = "Modernized modular Arena with independent modules, flexible engagement angles and limited top-attack capability.",
                Country = "Russia",
                YearIntroduced = 2018,
                Type = APSType.HardKill,
                DetectionRange = 120f,
                DetectionFOVHorizontal = 340f,
                DetectionFOVVertical = 60f,
                MinDetectionSpeed = 50f,
                MaxDetectionSpeed = 1500f,
                ReactionTime = 0.045f,
                SupportedThreats = ThreatClass.RPG | ThreatClass.HEAT | ThreatClass.ATGM_SACLOS | ThreatClass.ATGM_MCLOS | ThreatClass.HE | ThreatClass.TopAttack | ThreatClass.UAV_FPV,
                EngagementRangeMin = 4f,
                EngagementRangeMax = 30f,
                EngagementFOVHorizontal = 330f,
                EngagementFOVVertical = 55f,
                MinEngagementSpeed = 50f,
                MaxEngagementSpeed = 1500f,
                CountermeasureCapacity = 10,
                // Magazine reload: without it 10 rounds end APS for the rest of the battle.
                ReloadTime = 45f,
                CooldownBetweenShots = 0.35f,
                CanEngageMultiple = true,
                MaxSimultaneousEngagements = 3,
                MassKg = 220f,
                Cost = 95000f,
                PowerConsumptionKw = 2.2f,
                RadarModelPath = "Models/arenam_radar",
                LauncherModelPath = "Models/arenam_launcher",
                SensorModelPath = "Models/arenam_sensor",
                InterceptEffectPrefab = "Effects/arenam_intercept",
                LaunchSound = "Audio/arenam_launch",
                RadarSound = "Audio/arenam_radar",
                IsGameplayApproximation = true,
                Notes = new[]
                {
                    "Modules install independently for flexible coverage.",
                    "Top-attack protection depends on module placement.",
                    "UAV/FPV interception requires the threat to be a flying object.",
                    "All effectiveness values are gameplay approximations."
                }
            };
            d.InterceptProbability[ThreatClass.RPG] = 0.93f;
            d.InterceptProbability[ThreatClass.HEAT] = 0.90f;
            d.InterceptProbability[ThreatClass.ATGM_SACLOS] = 0.86f;
            d.InterceptProbability[ThreatClass.ATGM_MCLOS] = 0.88f;
            d.InterceptProbability[ThreatClass.HE] = 0.72f;
            d.InterceptProbability[ThreatClass.TopAttack] = 0.35f;
            d.InterceptProbability[ThreatClass.UAV_FPV] = 0.45f;
            d.InterceptProbability[ThreatClass.APFSDS] = 0f;
            _definitions[d.Id] = d;
        }

        // --- AFGANIT (Russian, hybrid) ---
        private static void RegisterAfganit()
        {
            var d = new APSDefinition
            {
                Id = "afganit",
                Name = "Afganit",
                Description = "Hybrid Russian APS combining radar/EO interceptors with jamming. T-14 Armata style.",
                Country = "Russia",
                YearIntroduced = 2015,
                Type = APSType.Hybrid,
                DetectionRange = 150f,
                DetectionFOVHorizontal = 360f,
                DetectionFOVVertical = 70f,
                MinDetectionSpeed = 40f,
                MaxDetectionSpeed = 1800f,
                ReactionTime = 0.04f,
                SupportedThreats = ThreatClass.RPG | ThreatClass.HEAT | ThreatClass.ATGM_SACLOS | ThreatClass.ATGM_MCLOS | ThreatClass.ATGM_FireAndForget | ThreatClass.HE | ThreatClass.CannonHE | ThreatClass.TopAttack | ThreatClass.APFSDS | ThreatClass.UAV_FPV | ThreatClass.LoiteringMunition,
                EngagementRangeMin = 5f,
                EngagementRangeMax = 35f,
                EngagementFOVHorizontal = 350f,
                EngagementFOVVertical = 65f,
                MinEngagementSpeed = 40f,
                MaxEngagementSpeed = 1800f,
                CountermeasureCapacity = 12,
                ReloadTime = 0f,
                CooldownBetweenShots = 0.3f,
                CanEngageMultiple = true,
                MaxSimultaneousEngagements = 4,
                MassKg = 310f,
                Cost = 145000f,
                PowerConsumptionKw = 4.0f,
                RadarModelPath = "Models/afganit_radar",
                LauncherModelPath = "Models/afganit_launcher",
                SensorModelPath = "Models/afganit_sensor",
                InterceptEffectPrefab = "Effects/afganit_intercept",
                LaunchSound = "Audio/afganit_launch",
                RadarSound = "Audio/afganit_radar",
                IsGameplayApproximation = true,
                Notes = new[]
                {
                    "Classifies threats by projectile signature before engaging.",
                    "APFSDS effect is a simulated capability based on public claims, not confirmed performance.",
                    "Combines hard-kill interceptors with soft-kill jamming.",
                    "All effectiveness values are gameplay approximations."
                }
            };
            d.InterceptProbability[ThreatClass.RPG] = 0.95f;
            d.InterceptProbability[ThreatClass.HEAT] = 0.92f;
            d.InterceptProbability[ThreatClass.ATGM_SACLOS] = 0.90f;
            d.InterceptProbability[ThreatClass.ATGM_MCLOS] = 0.90f;
            d.InterceptProbability[ThreatClass.ATGM_FireAndForget] = 0.55f;
            d.InterceptProbability[ThreatClass.HE] = 0.78f;
            d.InterceptProbability[ThreatClass.CannonHE] = 0.60f;
            d.InterceptProbability[ThreatClass.TopAttack] = 0.45f;
            d.InterceptProbability[ThreatClass.APFSDS] = 0.15f; // Simulated claim
            d.InterceptProbability[ThreatClass.UAV_FPV] = 0.55f;
            d.InterceptProbability[ThreatClass.LoiteringMunition] = 0.50f;
            d.SoftKillProbability[ThreatClass.ATGM_SACLOS] = 0.70f;
            d.SoftKillProbability[ThreatClass.ATGM_MCLOS] = 0.72f;
            d.SoftKillProbability[ThreatClass.ATGM_FireAndForget] = 0.40f;
            d.SoftKillProbability[ThreatClass.TopAttack] = 0.30f;
            d.SoftKillProbability[ThreatClass.UAV_FPV] = 0.45f;
            d.SoftKillProbability[ThreatClass.LoiteringMunition] = 0.40f;
            _definitions[d.Id] = d;
        }

        // --- SHTORA-1 (Soviet/Russian, soft-kill) ---
        private static void RegisterShtora()
        {
            var d = new APSDefinition
            {
                Id = "shtora1",
                Name = "Shtora-1",
                Description = "Soviet/Russian soft-kill APS with electro-optical dazzlers and laser warning. No hard-kill interceptors.",
                Country = "USSR/Russia",
                YearIntroduced = 1988,
                Type = APSType.SoftKill,
                DetectionRange = 90f,
                DetectionFOVHorizontal = 360f,
                DetectionFOVVertical = 50f,
                MinDetectionSpeed = 0f,
                MaxDetectionSpeed = 1500f,
                ReactionTime = 0.05f,
                SupportedThreats = ThreatClass.ATGM_SACLOS | ThreatClass.ATGM_MCLOS | ThreatClass.ATGM_FireAndForget | ThreatClass.TopAttack | ThreatClass.UAV_FPV | ThreatClass.LoiteringMunition,
                EngagementRangeMin = 10f,
                EngagementRangeMax = 80f,
                EngagementFOVHorizontal = APSInterceptRules.JamHorizontalFOV,
                EngagementFOVVertical = APSInterceptRules.JamVerticalFOV,
                MinEngagementSpeed = 0f,
                MaxEngagementSpeed = 1500f,
                CountermeasureCapacity = 20,
                ReloadTime = 40f,
                CooldownBetweenShots = 1.2f,
                CanEngageMultiple = true,
                MaxSimultaneousEngagements = 2,
                MassKg = 85f,
                Cost = 38000f,
                PowerConsumptionKw = 1.2f,
                RadarModelPath = "Models/shtora_sensor",
                LauncherModelPath = "Models/shtora_dazzler",
                SensorModelPath = "Models/shtora_laserwarn",
                InterceptEffectPrefab = "Effects/shtora_dazzle",
                LaunchSound = "Audio/shtora_jam",
                RadarSound = "Audio/shtora_warn",
                IsGameplayApproximation = true,
                Notes = new[]
                {
                    "Electro-optical interference; no interceptor rounds.",
                    "Cannot disable APFSDS or unguided rounds.",
                    "Jams SACLOS/MCLOS guidance within range.",
                    "All effectiveness values are gameplay approximations."
                }
            };
            d.InterceptProbability[ThreatClass.ATGM_SACLOS] = 0f;
            d.InterceptProbability[ThreatClass.ATGM_MCLOS] = 0f;
            d.InterceptProbability[ThreatClass.APFSDS] = 0f;
            d.SoftKillProbability[ThreatClass.ATGM_SACLOS] = 0.78f;
            d.SoftKillProbability[ThreatClass.ATGM_MCLOS] = 0.82f;
            d.SoftKillProbability[ThreatClass.ATGM_FireAndForget] = 0.35f;
            d.SoftKillProbability[ThreatClass.TopAttack] = 0.30f;
            d.SoftKillProbability[ThreatClass.UAV_FPV] = 0.40f;
            d.SoftKillProbability[ThreatClass.LoiteringMunition] = 0.38f;
            _definitions[d.Id] = d;
        }

        // --- TROPHY / WINDBREAKER (Israeli, hard-kill) ---
        private static void RegisterTrophy()
        {
            var d = new APSDefinition
            {
                Id = "trophy",
                Name = "Trophy (Windbreaker)",
                Description = "Israeli hard-kill APS with multi-radar arrangement and flat interceptors. Merkava/Abrams/Leopard 2 fit.",
                Country = "Israel",
                YearIntroduced = 2009,
                Type = APSType.HardKill,
                DetectionRange = 110f,
                DetectionFOVHorizontal = 340f,
                DetectionFOVVertical = 55f,
                MinDetectionSpeed = 60f,
                MaxDetectionSpeed = 1300f,
                ReactionTime = 0.05f,
                SupportedThreats = ThreatClass.RPG | ThreatClass.HEAT | ThreatClass.ATGM_SACLOS | ThreatClass.ATGM_MCLOS | ThreatClass.ATGM_FireAndForget | ThreatClass.HE,
                EngagementRangeMin = 5f,
                EngagementRangeMax = 28f,
                EngagementFOVHorizontal = 330f,
                EngagementFOVVertical = 50f,
                MinEngagementSpeed = 60f,
                MaxEngagementSpeed = 1300f,
                CountermeasureCapacity = 8,
                ReloadTime = 0f,
                CooldownBetweenShots = 0.4f,
                CanEngageMultiple = true,
                MaxSimultaneousEngagements = 2,
                MassKg = 240f,
                Cost = 110000f,
                PowerConsumptionKw = 2.8f,
                RadarModelPath = "Models/trophy_radar",
                LauncherModelPath = "Models/trophy_launcher",
                SensorModelPath = "Models/trophy_sensor",
                InterceptEffectPrefab = "Effects/trophy_intercept",
                LaunchSound = "Audio/trophy_launch",
                RadarSound = "Audio/trophy_radar",
                IsGameplayApproximation = true,
                Notes = new[]
                {
                    "Radar arrangement tracks approach direction.",
                    "Does not intercept kinetic penetrators (APFSDS).",
                    "Dead zones depend on module installation.",
                    "All effectiveness values are gameplay approximations."
                }
            };
            d.InterceptProbability[ThreatClass.RPG] = 0.94f;
            d.InterceptProbability[ThreatClass.HEAT] = 0.90f;
            d.InterceptProbability[ThreatClass.ATGM_SACLOS] = 0.85f;
            d.InterceptProbability[ThreatClass.ATGM_MCLOS] = 0.87f;
            d.InterceptProbability[ThreatClass.ATGM_FireAndForget] = 0.45f;
            d.InterceptProbability[ThreatClass.HE] = 0.70f;
            d.InterceptProbability[ThreatClass.APFSDS] = 0f;
            _definitions[d.Id] = d;
        }

        // --- IRON FIST (Israeli, hard-kill) ---
        private static void RegisterIronFist()
        {
            var d = new APSDefinition
            {
                Id = "ironfist",
                Name = "Iron Fist",
                Description = "Israeli hard-kill APS combining radar and EO sensors with proximity-fused interceptors.",
                Country = "Israel",
                YearIntroduced = 2010,
                Type = APSType.HardKill,
                DetectionRange = 100f,
                DetectionFOVHorizontal = 330f,
                DetectionFOVVertical = 55f,
                MinDetectionSpeed = 60f,
                MaxDetectionSpeed = 1200f,
                ReactionTime = 0.06f,
                SupportedThreats = ThreatClass.RPG | ThreatClass.HEAT | ThreatClass.ATGM_SACLOS | ThreatClass.ATGM_MCLOS | ThreatClass.ATGM_FireAndForget | ThreatClass.HE,
                EngagementRangeMin = 4f,
                EngagementRangeMax = 26f,
                EngagementFOVHorizontal = 320f,
                EngagementFOVVertical = 50f,
                MinEngagementSpeed = 60f,
                MaxEngagementSpeed = 1200f,
                CountermeasureCapacity = 8,
                ReloadTime = 30f,
                CooldownBetweenShots = 0.6f,
                CanEngageMultiple = true,
                MaxSimultaneousEngagements = 2,
                MassKg = 195f,
                Cost = 88000f,
                PowerConsumptionKw = 2.0f,
                RadarModelPath = "Models/ironfist_radar",
                LauncherModelPath = "Models/ironfist_launcher",
                SensorModelPath = "Models/ironfist_sensor",
                InterceptEffectPrefab = "Effects/ironfist_intercept",
                LaunchSound = "Audio/ironfist_launch",
                RadarSound = "Audio/ironfist_radar",
                IsGameplayApproximation = true,
                Notes = new[]
                {
                    "Proximity-fused interceptors disrupt or deflect projectiles.",
                    "Reload time applies between interceptor volleys.",
                    "Does not intercept kinetic penetrators (APFSDS).",
                    "All effectiveness values are gameplay approximations."
                }
            };
            d.InterceptProbability[ThreatClass.RPG] = 0.92f;
            d.InterceptProbability[ThreatClass.HEAT] = 0.88f;
            d.InterceptProbability[ThreatClass.ATGM_SACLOS] = 0.84f;
            d.InterceptProbability[ThreatClass.ATGM_MCLOS] = 0.85f;
            d.InterceptProbability[ThreatClass.ATGM_FireAndForget] = 0.42f;
            d.InterceptProbability[ThreatClass.HE] = 0.68f;
            d.InterceptProbability[ThreatClass.APFSDS] = 0f;
            _definitions[d.Id] = d;
        }

        // --- AMAP-ADS / STRIKESHIELD (German, modular hard-kill) ---
        private static void RegisterAmapAds()
        {
            var d = new APSDefinition
            {
                Id = "amapads",
                Name = "AMAP-ADS (StrikeShield)",
                Description = "German modular hard-kill APS. Independent modules protect separate zones; destroyed modules lose their zone.",
                Country = "Germany",
                YearIntroduced = 2007,
                Type = APSType.HardKill,
                DetectionRange = 70f,
                DetectionFOVHorizontal = 90f,
                DetectionFOVVertical = 45f,
                MinDetectionSpeed = 80f,
                MaxDetectionSpeed = 1000f,
                ReactionTime = 0.03f,
                SupportedThreats = ThreatClass.RPG | ThreatClass.HEAT | ThreatClass.ATGM_SACLOS | ThreatClass.ATGM_MCLOS | ThreatClass.HE,
                EngagementRangeMin = 2f,
                EngagementRangeMax = 14f,
                EngagementFOVHorizontal = 85f,
                EngagementFOVVertical = 40f,
                MinEngagementSpeed = 80f,
                MaxEngagementSpeed = 1000f,
                CountermeasureCapacity = 4,
                ReloadTime = 60f,
                CooldownBetweenShots = 0.25f,
                CanEngageMultiple = false,
                MaxSimultaneousEngagements = 1,
                MassKg = 95f,
                Cost = 62000f,
                PowerConsumptionKw = 1.4f,
                RadarModelPath = "Models/amapads_radar",
                LauncherModelPath = "Models/amapads_launcher",
                SensorModelPath = "Models/amapads_sensor",
                InterceptEffectPrefab = "Effects/amapads_intercept",
                LaunchSound = "Audio/amapads_launch",
                RadarSound = "Audio/amapads_radar",
                IsGameplayApproximation = true,
                Notes = new[]
                {
                    "Each module covers its own zone; multiple modules cover more area.",
                    "Destroyed module removes protection for its zone.",
                    "Close-range, fast reaction design.",
                    "All effectiveness values are gameplay approximations."
                }
            };
            d.InterceptProbability[ThreatClass.RPG] = 0.89f;
            d.InterceptProbability[ThreatClass.HEAT] = 0.85f;
            d.InterceptProbability[ThreatClass.ATGM_SACLOS] = 0.75f;
            d.InterceptProbability[ThreatClass.ATGM_MCLOS] = 0.77f;
            d.InterceptProbability[ThreatClass.HE] = 0.62f;
            d.InterceptProbability[ThreatClass.APFSDS] = 0f;
            _definitions[d.Id] = d;
        }

        // --- ZASLIN / ZASLON (Ukrainian, hard-kill) ---
        private static void RegisterZaslin()
        {
            var d = new APSDefinition
            {
                Id = "zaslin",
                Name = "Zaslin",
                Description = "Ukrainian hard-kill APS with external interceptor modules. One-shot or limited-use per preset.",
                Country = "Ukraine",
                YearIntroduced = 2016,
                Type = APSType.HardKill,
                DetectionRange = 80f,
                DetectionFOVHorizontal = 160f,
                DetectionFOVVertical = 50f,
                MinDetectionSpeed = 70f,
                MaxDetectionSpeed = 1100f,
                ReactionTime = 0.055f,
                SupportedThreats = ThreatClass.RPG | ThreatClass.HEAT | ThreatClass.ATGM_SACLOS | ThreatClass.ATGM_MCLOS | ThreatClass.HE,
                EngagementRangeMin = 3f,
                EngagementRangeMax = 20f,
                EngagementFOVHorizontal = 150f,
                EngagementFOVVertical = 45f,
                MinEngagementSpeed = 70f,
                MaxEngagementSpeed = 1100f,
                CountermeasureCapacity = 2,
                ReloadTime = 0f,
                CooldownBetweenShots = 1.0f,
                CanEngageMultiple = false,
                MaxSimultaneousEngagements = 1,
                MassKg = 130f,
                Cost = 54000f,
                PowerConsumptionKw = 1.3f,
                RadarModelPath = "Models/zaslin_radar",
                LauncherModelPath = "Models/zaslin_launcher",
                SensorModelPath = "Models/zaslin_sensor",
                InterceptEffectPrefab = "Effects/zaslin_intercept",
                LaunchSound = "Audio/zaslin_launch",
                RadarSound = "Audio/zaslin_radar",
                IsGameplayApproximation = true,
                Notes = new[]
                {
                    "External interceptor modules protect only their installed arc.",
                    "Limited-use modules depending on preset configuration.",
                    "Detonates protective charge on detection of approach.",
                    "All effectiveness values are gameplay approximations."
                }
            };
            d.InterceptProbability[ThreatClass.RPG] = 0.87f;
            d.InterceptProbability[ThreatClass.HEAT] = 0.83f;
            d.InterceptProbability[ThreatClass.ATGM_SACLOS] = 0.73f;
            d.InterceptProbability[ThreatClass.ATGM_MCLOS] = 0.75f;
            d.InterceptProbability[ThreatClass.HE] = 0.58f;
            d.InterceptProbability[ThreatClass.APFSDS] = 0f;
            _definitions[d.Id] = d;
        }
    }
}