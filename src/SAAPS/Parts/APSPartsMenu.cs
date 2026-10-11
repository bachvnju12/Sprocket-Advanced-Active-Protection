using DesignerFramework.AssetManagement;
using DesignerFramework.UI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Sprocket.Selection;
using Sprocket.PartImporting;
using Sprocket.VehicleDesigner;
using Sprocket.Vehicles.PlateStructures.Design;
using SprocketAdvancedAPS.Core;

namespace SprocketAdvancedAPS.Parts
{
    internal static class APSPartsMenu
    {
        private const string GroupName = "Active Protection Systems";
        // Native addonPlateStructurePart.json; do not route custom geometry through a hull designer.
        private const string AddonGuid = "8f8a9d20-eb45-482e-b149-014c964c4e2c";
        private const string StructureGuid = "7f8a9d20-eb45-482e-b149-014c964c4e2c";
        private static bool warned;
        private static int logs;

        private static bool IsAPS(Option? option) => option?.Tags?.Any(tag => tag is "saapsPart" or "saapsCustomPart") == true;
        private static bool IsCustom(Option? option) => option?.Tags?.Any(tag => tag == "saapsCustomPart") == true;

        [HarmonyPostfix, HarmonyPatch(typeof(VehicleEditor), nameof(VehicleEditor.GetModuleAssetSelection))]
        private static void ModuleSelection(VehicleEditor __instance, ISelectionBuffer __0) => AddCustom(__instance, __0);

        [HarmonyPostfix, HarmonyPatch(typeof(VehicleEditor), nameof(VehicleEditor.GetEditorAssetSelection))]
        private static void EditorSelection(VehicleEditor __instance, ISelectionBuffer __0) => AddCustom(__instance, __0);

        private static void AddCustom(VehicleEditor __instance, ISelectionBuffer __0)
        {
            try
            {
                var buffer = __0?.TryCast<VehicleAssetSelection>();
                if (buffer?.items == null || __instance.target == null) return;
                bool hasAPS = false;
                foreach (var item in buffer.items)
                {
                    var group = item?.TryCast<OptionGroup>();
                    if (group?.items == null) continue;
                    if (group.items.Any(IsCustom)) return;
                    hasAPS |= group.items.Any(IsAPS);
                }
                if (!hasAPS) return;
                var modules = new Il2CppSystem.Collections.Generic.List<PlateStructureDesigner>();
                __instance.modules.GetAll(modules);
                PlateStructureDesigner? designer = null;
                foreach (var module in modules)
                {
                    if (module.plateStructureGuid != StructureGuid || module.addonPlateStructureGuid != AddonGuid || module.Target?.Pointer != __instance.target.Pointer) continue;
                    if (designer != null) throw new InvalidOperationException("Ambiguous native add-on designer.");
                    designer = module;
                }
                if (designer == null) throw new InvalidOperationException("Native add-on designer unavailable.");
                var asset = __instance.AssetDatabase.GetByIdentifier(APSCustomRuntime.PartGuid);
                var card = asset?.TryCast<PartDisplayCard>();
                if (card == null || !string.Equals(card.PartGuid, APSCustomRuntime.PartGuid, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Custom APS catalog part unavailable.");
                var option = Option.ToOption(asset!);
                if (!IsCustom(option)) throw new InvalidOperationException("Custom APS catalog tag missing.");
                var source = designer;
                var owner = __instance.target.Pointer;
                option.OnSelect = (Action<Il2CppSystem.Object>)(_ =>
                {
                    try
                    {
                        if (__instance.target?.Pointer != owner || source.Target?.Pointer != owner)
                            throw new InvalidOperationException("Custom APS menu belongs to a closed vehicle.");
                        var current = new Il2CppSystem.Collections.Generic.List<PlateStructureDesigner>();
                        __instance.modules.GetAll(current);
                        bool loaded = false;
                        foreach (var module in current)
                            loaded |= module.Pointer == source.Pointer && module.plateStructureGuid == StructureGuid && module.addonPlateStructureGuid == AddonGuid;
                        if (!loaded) throw new InvalidOperationException("Custom APS designer is no longer loaded.");
                        source.LoadPrefabPlateStructure(APSCustomRuntime.PartGuid);
                    }
                    catch (Exception ex) { Warn(ex); }
                });
                buffer.Add(new OptionGroup(GroupName, new[] { option }).Cast<ISelectionGroup>());
            }
            catch (Exception ex) { Warn(ex); }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(SelectionPanel), nameof(SelectionPanel.SetDisplayOptions))]
        private static void Groups(ref Il2CppReferenceArray<ISelectionGroup> __0, ref int __1)
        {
            if (__0 == null) return;
            try
            {
                var groups = new List<ISelectionGroup>();
                var aps = new List<Option>();
                var seen = new HashSet<IntPtr>();
                bool customSeen = false, activeAPS = false;
                OptionGroup? existing = null;
                int newActive = -1, oldIndex = 0;
                foreach (var item in __0)
                {
                    bool wasActive = oldIndex++ == __1;
                    var group = item?.TryCast<OptionGroup>();
                    if (group?.items == null)
                    {
                        if (wasActive) newActive = groups.Count;
                        groups.Add(item!);
                        continue;
                    }
                    bool isAPSGroup = group.Name == GroupName;
                    if (isAPSGroup) existing ??= group;
                    var retained = new List<Option>();
                    bool changed = isAPSGroup;
                    foreach (var option in group.items)
                    {
                        if (IsAPS(option) || isAPSGroup)
                        {
                            if (option != null && seen.Add(option.Pointer) && (!IsCustom(option) || !customSeen))
                            {
                                aps.Add(option);
                                customSeen |= IsCustom(option);
                            }
                            changed = true;
                        }
                        else retained.Add(option!);
                    }
                    if (!changed) { if (wasActive) newActive = groups.Count; groups.Add(item!); continue; }
                    if (retained.Count > 0)
                    {
                        var copy = new OptionGroup(group.Name, retained.ToArray()) { Flags = group.Flags, InteractableTest = group.InteractableTest };
                        if (wasActive) newActive = groups.Count;
                        groups.Add(copy.Cast<ISelectionGroup>());
                    }
                    else if (wasActive) activeAPS = true;
                }
                if (aps.Count == 0) return;
                var merged = new OptionGroup(GroupName, aps.ToArray());
                if (existing != null) { merged.Flags = existing.Flags; merged.InteractableTest = existing.InteractableTest; }
                groups.Add(merged.Cast<ISelectionGroup>());
                __0 = new Il2CppReferenceArray<ISelectionGroup>(groups.ToArray());
                __1 = activeAPS || newActive < 0 ? groups.Count - 1 : newActive;
                if (logs++ < 3) Main.ModLog.LogInfo($"[SAAPS] Menu grouped {aps.Count} APS options under {GroupName} (Custom APS: {customSeen}).");
            }
            catch (Exception ex) { Warn(ex); }
        }

        private static void Warn(Exception ex)
        {
            if (!warned) { warned = true; Main.ModLog.LogWarning("[SAAPS] APS menu/placement unavailable; native options retained: " + ex.Message); }
        }
    }
}
