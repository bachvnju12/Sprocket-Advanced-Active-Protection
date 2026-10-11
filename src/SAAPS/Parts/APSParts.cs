using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Sprocket.Vehicles;
using SprocketAdvancedAPS.Systems;

namespace SprocketAdvancedAPS.Parts
{
    internal sealed class APSPartRegistry
    {
        private static readonly IReadOnlyDictionary<string, string> DefinitionIds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["saaps_drozd1"] = "drozd1",
            ["saaps_drozd2"] = "drozd2",
            ["saaps_arena"] = "arena",
            ["saaps_arenam"] = "arenam",
            ["saaps_afganit"] = "afganit",
            ["saaps_shtora1"] = "shtora1",
            ["saaps_trophy"] = "trophy",
            ["saaps_ironfist"] = "ironfist",
            ["saaps_amapads"] = "amapads",
            ["saaps_zaslin"] = "zaslin"
        };

        internal static APSPartRegistry? Instance { get; private set; }
        internal APSPartRegistry(ManualLogSource log) => Instance = this;

        internal static APSDefinition? GetModuleDefinition(VehicleComponent component) =>
            component == null ? null : GetModuleDefinition(component.ComponentID) ?? Core.APSCustomRuntime.Find(component)?.Definition;

        internal static APSDefinition? GetModuleDefinition(string? componentId) =>
            componentId != null && DefinitionIds.TryGetValue(componentId, out var id) && APSDefinitions.TryGet(id, out var definition) ? definition : null;

        internal static bool IsAPSComponent(VehicleComponent component) => GetModuleDefinition(component) != null;
        internal static IEnumerable<string> GetPartGuids() => DefinitionIds.Keys;
        internal void Shutdown() => Instance = null;
    }
}
