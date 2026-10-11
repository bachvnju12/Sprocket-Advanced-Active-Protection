using DesignerFramework.AssetManagement;
using DesignerFramework.UI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Sprocket.Selection;
using Sprocket.PartImporting;
using Sprocket.VehicleDesigner.Access;
using Sprocket.VehicleDesigner.UI;
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
        private static IVehicleEditor? Editor;
        private static IntPtr GUI;
        private static bool warned, clickWarned;
        private static int logs;

        internal static void Clear() { Editor = null; GUI = IntPtr.Zero; warned = clickWarned = false; logs = 0; }

        [HarmonyPrefix, HarmonyPatch(typeof(VehicleEditorGUIPlugin), nameof(VehicleEditorGUIPlugin.SetEditor))]
        private static void SetEditor(VehicleEditorGUIPlugin __instance, IVehicleEditor __0)
        {
            GUI = __instance.Pointer;
            Editor = __0;
            warned = clickWarned = false;
            logs = 0;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(VehicleEditorGUIPlugin), nameof(VehicleEditorGUIPlugin.Release))]
        private static void ReleaseEditor(VehicleEditorGUIPlugin __instance)
        {
            if (GUI == __instance.Pointer) Clear();
        }

        private static bool IsAPS(Option? option) => option?.Tags?.Any(tag => tag is "saapsPart" or "saapsCustomPart") == true;
        private static bool IsCustom(Option? option) => option?.Tags?.Any(tag => tag == "saapsCustomPart") == true;

        private static Option? CustomOption()
        {
            try
            {
                var editor = Editor ?? throw new InvalidOperationException("No editor context from native GUI SetEditor.");
                var target = editor.Target ?? throw new InvalidOperationException("Editor has no vehicle target.");
                var modules = new Il2CppSystem.Collections.Generic.List<PlateStructureDesigner>();
                editor.ModuleReader.GetAll(modules);
                PlateStructureDesigner? designer = null;
                foreach (var module in modules)
                {
                    if (module.plateStructureGuid != StructureGuid || module.addonPlateStructureGuid != AddonGuid || module.Target?.Pointer != target.Pointer) continue;
                    if (designer != null) throw new InvalidOperationException("Ambiguous native add-on designer.");
                    designer = module;
                }
                if (designer == null) throw new InvalidOperationException($"Native add-on designer unavailable among {modules.Count} plate designers.");
                Sprocket.AssetManagement.IAssetInfo? asset = null;
                foreach (var candidate in editor.AssetDatabase.GetAllWithTags(new[] { "saapsCustomPart" }))
                {
                    if (!string.Equals(candidate?.TryCast<PartDisplayCard>()?.PartGuid, APSCustomRuntime.PartGuid, StringComparison.OrdinalIgnoreCase)) continue;
                    if (asset != null) throw new InvalidOperationException("Duplicate Custom APS catalog identity.");
                    asset = candidate;
                }
                var card = asset?.TryCast<PartDisplayCard>();
                if (card == null || !string.Equals(card.PartGuid, APSCustomRuntime.PartGuid, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Custom APS catalog part unavailable.");
                var option = Option.ToOption(asset!);
                if (!IsCustom(option)) throw new InvalidOperationException("Custom APS catalog tag missing.");
                var source = designer;
                var owner = target.Pointer;
                option.OnSelect = (Action<Il2CppSystem.Object>)(_ =>
                {
                    try
                    {
                        if (Editor?.Pointer != editor.Pointer || editor.Target?.Pointer != owner || source.Target?.Pointer != owner)
                            throw new InvalidOperationException("Custom APS menu belongs to a closed vehicle.");
                        var current = new Il2CppSystem.Collections.Generic.List<PlateStructureDesigner>();
                        editor.ModuleReader.GetAll(current);
                        bool loaded = false;
                        foreach (var module in current)
                            loaded |= module.Pointer == source.Pointer && module.plateStructureGuid == StructureGuid && module.addonPlateStructureGuid == AddonGuid;
                        if (!loaded) throw new InvalidOperationException("Custom APS designer is no longer loaded.");
                        source.LoadPrefabPlateStructure(APSCustomRuntime.PartGuid);
                    }
                    catch (Exception ex)
                    {
                        if (!clickWarned) { clickWarned = true; Main.ModLog.LogWarning("[SAAPS] Custom APS click failed: " + ex.Message); }
                    }
                });
                return option;
            }
            catch (Exception ex) { Warn(ex); return null; }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(SelectionPanel), nameof(SelectionPanel.SetDisplayOptions))]
        private static void Groups(ref Il2CppReferenceArray<ISelectionGroup> __0, ref int __1)
        {
            if (__0 == null) return;
            try
            {
                bool firepower = __0.Any(item => item?.TryCast<OptionGroup>()?.items?.Any(option =>
                    option?.Tags?.Any(tag => tag == "saapsPart") == true) == true);
                var groups = new List<ISelectionGroup>();
                var aps = new List<Option>();
                var seen = new HashSet<IntPtr>();
                bool customSeen = false, activeAPS = false, removedCustom = false;
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
                    bool isAPSGroup = firepower && group.Name == GroupName;
                    if (isAPSGroup) existing ??= group;
                    var retained = new List<Option>();
                    bool changed = isAPSGroup;
                    foreach (var option in group.items)
                    {
                        if (!firepower && IsCustom(option))
                        {
                            changed = removedCustom = true;
                            continue;
                        }
                        if (firepower && (IsAPS(option) || isAPSGroup))
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
                if (!firepower)
                {
                    if (removedCustom)
                    {
                        __0 = new Il2CppReferenceArray<ISelectionGroup>(groups.ToArray());
                        __1 = newActive >= 0 ? newActive : 0;
                    }
                    return;
                }
                if (aps.Count == 0) return;
                if (!customSeen && CustomOption() is { } custom)
                {
                    aps.Add(custom);
                    customSeen = true;
                }
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
