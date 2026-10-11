using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Sprocket.UI;
using Sprocket.VehicleDesigner.Modules;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AttachedBehaviours;
using SprocketAdvancedAPS.Core;
using SprocketAdvancedAPS.Parts;
using SprocketAdvancedAPS.Systems;
using SprocketAdvancedAPS.UI;
using UnityEngine;
using UnityEngine.Events;
using SerializationInfo = Il2CppSystem.Runtime.Serialization.SerializationInfo;

[assembly: AssemblyMetadata("Sprocket.Mod.Id", "sprocket.advancedaps")]
[assembly: AssemblyMetadata("Sprocket.Mod.DisplayName", "Sprocket Advanced APS")]
[assembly: AssemblyMetadata("Sprocket.Mod.Description", "APS gameplay simulation for Sprocket")]
[assembly: AssemblyMetadata("Sprocket.Mod.Authors", "bachvnju12")]
[assembly: AssemblyMetadata("Sprocket.Mod.Repository", "bachvnju12/Sprocket-Advanced-Active-Protection")]
[assembly: AssemblyMetadata("Sprocket.Mod.Category", "gameplay")]
[assembly: AssemblyMetadata("Sprocket.Mod.License", "MIT")]

namespace SprocketAdvancedAPS
{
    [BepInPlugin(PluginGuid, "Sprocket Advanced APS", ModVersion)]
    [BepInDependency("furryaxw.sprocket-mod-api", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Main : BasePlugin
    {
        internal const string PluginGuid = "sprocket.advancedaps";
        internal const string ModVersion = "1.0.2";
        internal static ManualLogSource ModLog = null!;
        internal static Main Instance = null!;
        private Harmony? _harmony;

        public override void Load()
        {
            Instance = this;
            ModLog = Log;
            try
            {
                APSDefinitions.LoadAll();
                APSCore.InitializeConfig(Config);
                new APSCore(Log);
                new APSSystems(Log);
                new APSPartRegistry(Log);
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(typeof(APSPartHooks));
                APSCustomRuntime.CheckNativeJsonRoundTrip();
                _harmony.PatchAll(typeof(APSCustomRuntime));
                _harmony.PatchAll(typeof(ProjectileTracker));
                foreach (var feature in new[] { typeof(APSCustomInspector), typeof(APSModelAssets), typeof(APSPartsMenu), typeof(APSPartIcons) })
                    try { new Harmony(PluginGuid + "." + feature.Name).PatchAll(feature); }
                    catch (Exception ex)
                    {
                        try { new Harmony(PluginGuid + "." + feature.Name).UnpatchSelf(); }
                        catch (Exception rollback) { throw new AggregateException(ex, rollback); }
                        Log.LogWarning($"[SAAPS] {feature.Name} patches disabled: {ex}");
                    }
                AddComponent<APSDriver>();
                Log.LogInfo("[SAAPS] APS runtime loaded. Optional feature failures, if any, are listed above.");
            }
            catch (Exception ex)
            {
                Cleanup();
                Log.LogError("[SAAPS] APS runtime disabled after initialization failure: " + ex);
            }
        }

        public override bool Unload() => Cleanup();

        private bool Cleanup()
        {
            bool complete = true;
            void Run(Action action)
            {
                try { action(); }
                catch (Exception ex) { complete = false; Log.LogError("[SAAPS] Cleanup failed: " + ex); }
            }
            Run(() => APSDriver.Instance?.Dispose());
            Run(() => _harmony?.UnpatchSelf());
            foreach (var feature in new[] { typeof(APSCustomInspector), typeof(APSModelAssets), typeof(APSPartsMenu), typeof(APSPartIcons) })
                Run(() => new Harmony(PluginGuid + "." + feature.Name).UnpatchSelf());
            Run(() => APSSystems.Instance?.Shutdown());
            Run(() => APSCore.Instance?.Shutdown());
            Run(() => APSPartRegistry.Instance?.Shutdown());
            Run(APSPartIcons.Cleanup);
            Run(APSModelAssets.Cleanup);
            Run(APSCustomRuntime.Clear);
            Run(APSCustomInspector.Clear);
            Run(APSPartsMenu.Clear);
            Run(ReleaseModMetadata);
            if (complete) { ModLog = null!; Instance = null!; }
            return complete;
        }

        private void ReleaseModMetadata()
        {
            try
            {
                var api = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("SprocketModAPI.Keybindings.Keybinds", false))
                    .FirstOrDefault(t => t != null);
                api?.GetMethod("UnregisterModMetadata", new[] { typeof(string) })?.Invoke(null, new object[] { PluginGuid });
            }
            catch
            {
                // Optional API cleanup.
            }
        }
    }

    internal static class APSPartHooks
    {
        internal static System.Collections.Generic.IEnumerable<VehicleComponent> SelectedComponents(Vehicle vehicle)
        {
            if (vehicle == null) yield break;
            var items = vehicle.ObjectReader?.Items;
            if (items == null) yield break;
            int count = items.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<VehicleObject>>().Count;
            for (int i = 0; i < count; i++)
            {
                var obj = items[i];
                if (obj == null || obj.Released || obj.Components == null) continue;
                int components = obj.Components.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<VehicleComponent>>().Count;
                for (int j = 0; j < components; j++)
                {
                    var component = obj.Components[j];
                    if (component != null && component.Vehicle?.Pointer == vehicle.Pointer && APSPartRegistry.IsAPSComponent(component))
                    {
                        if (APSPartRegistry.GetModuleDefinition(component.ComponentID) != null) APSModelAssets.Apply(component);
                        yield return component;
                    }
                }
            }
        }

        internal static string DescribeComponents(Vehicle vehicle)
        {
            if (vehicle == null) return "vehicle unavailable";
            try
            {
                var reader = vehicle.ObjectReader;
                var items = reader?.Items;
                if (items == null) return "no object reader/items";

                int itemCount = items.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<VehicleObject>>().Count;
                int componentCount = 0;
                int shown = 0;
                var identities = new System.Collections.Generic.SortedSet<string>(StringComparer.Ordinal);
                var candidates = new System.Collections.Generic.List<string>();
                var details = new System.Collections.Generic.List<string>();
                for (int i = 0; i < itemCount; i++)
                {
                    var obj = items[i];
                    if (obj == null) continue;
                    bool released = obj.Released;
                    string objectPointer;
                    try { objectPointer = $"0x{obj.Pointer:X}"; }
                    catch { objectPointer = "error"; }
                    var components = obj.Components;
                    if (components == null) continue;
                    int count = components.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<VehicleComponent>>().Count;
                    componentCount += count;
                    for (int j = 0; j < count; j++)
                    {
                        try
                        {
                            var component = components[j];
                            if (component != null)
                            {
                                string identity = component.ComponentID ?? "<null>";
                                identities.Add(identity);
                                string guid = obj.GUID ?? "<null>";
                                if (identity.StartsWith("saaps_", StringComparison.Ordinal) || guid.StartsWith("a91bb31a-", StringComparison.OrdinalIgnoreCase))
                                    candidates.Add($"{i}:{j}={identity}[typeId={component.ComponentTypeID};guid={guid};released={released};ownerMatch={component.Vehicle?.Pointer == vehicle.Pointer};registered={APSPartRegistry.IsAPSComponent(component)}]");
                            }
                            if (shown >= 32) continue;
                            if (component == null)
                            {
                                details.Add($"{i}:{j}=<null>");
                                shown++;
                                continue;
                            }

                            string id = component.ComponentID ?? "<null>";
                            string componentTypeId = component.ComponentTypeID ?? "<null>";
                            string entityId = component.EntityID.ToString();
                            string vehicleObjectGuid = "<none>";
                            try { vehicleObjectGuid = component.VehicleObject?.GUID ?? "<no-guid>"; }
                            catch { vehicleObjectGuid = "<guid-error>"; }
                            string blueprintName = "<not-plate>";
                            try
                            {
                                var plate = component.TryCast<Sprocket.Vehicles.PlateStructures.PlateStructure>();
                                if (plate != null) blueprintName = plate.Blueprint?.Name ?? "<no-blueprint>";
                            }
                            catch { blueprintName = "<blueprint-error>"; }
                            string type = component.GetType().Name;
                            string componentVehicle = "?";
                            bool ownerMatches = false;
                            try
                            {
                                var owner = component.Vehicle;
                                if (owner != null)
                                {
                                    componentVehicle = $"0x{owner.Pointer:X}";
                                    ownerMatches = owner.Pointer == vehicle.Pointer;
                                }
                                else componentVehicle = "null";
                            }
                            catch { componentVehicle = "error"; }

                            string status = APSPartRegistry.GetModuleDefinition(component) != null ? "registered" : "unknown-id";
                            if (released) status += ",object-released";
                            if (!ownerMatches) status += ",owner-mismatch";
                            details.Add($"{i}:{j}={id}:{type}[typeId={componentTypeId};entity={entityId};blueprint={blueprintName};vehicleObjectGuid={vehicleObjectGuid};component=0x{component.Pointer:X};object={objectPointer};vehicle={componentVehicle};{status}]");
                            shown++;
                        }
                        catch (Exception ex)
                        {
                            if (shown < 32)
                            {
                                details.Add($"{i}:{j}=read-error:{ex.GetType().Name}");
                                shown++;
                            }
                        }
                    }
                }

                string summary = $"items={itemCount}, components={componentCount}; allComponentIds={string.Join("|", identities)}; APS candidates={string.Join(", ", candidates.Take(32))}; candidateCount={candidates.Count}";
                if (componentCount > shown) summary += $", truncated={componentCount - shown}";
                return details.Count == 0 ? summary + "; no readable components" : summary + "; " + string.Join(", ", details);
            }
            catch (Exception ex)
            {
                return "component diagnostic failed: " + ex.GetType().Name;
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(VehicleComponent), nameof(VehicleComponent.BuildInternal))]
        private static void Built(VehicleComponent __instance)
        {
            if (!APSPartRegistry.IsAPSComponent(__instance) || APSCore.Instance == null) return;
            try
            {
                var vehicle = __instance.Vehicle?.TryCast<Vehicle>();
                if (vehicle == null) return;
                APSCore.Instance.ModulesChanged(vehicle);
            }
            catch (Exception ex) { Main.ModLog.LogWarning("[SAAPS] APS part state build failed: " + ex.Message); }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(VehicleComponent), nameof(VehicleComponent.ReleaseInternal))]
        private static void Released(VehicleComponent __instance)
        {
            bool isAPS = APSPartRegistry.IsAPSComponent(__instance);
            if (!isAPS || APSCore.Instance == null) return;
            var vehicle = __instance.Vehicle?.TryCast<Vehicle>();
            if (vehicle == null) return;
            APSCore.Instance.ModulesChanged(vehicle);
        }
    }
}
