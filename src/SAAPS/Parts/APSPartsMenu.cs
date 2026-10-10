using DesignerFramework.AssetManagement;
using DesignerFramework.UI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Sprocket.Selection;

namespace SprocketAdvancedAPS.Parts
{
    internal static class APSPartsMenu
    {
        private static bool warned;
        private static int logs;

        [HarmonyPrefix, HarmonyPatch(typeof(SelectionPanel), nameof(SelectionPanel.SetDisplayOptions))]
        private static void Groups(ref Il2CppReferenceArray<ISelectionGroup> __0, ref int __1)
        {
            if (__0 == null) return;
            try
            {
                var groups = new List<ISelectionGroup>();
                var aps = new List<Option>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                int newActive = -1, oldIndex = 0;
                foreach (var item in __0)
                {
                    bool wasActive = oldIndex++ == __1;
                    var group = item?.TryCast<OptionGroup>();
                    if (group?.items == null || group.Name == "Active Protection Systems")
                    {
                        if (wasActive) newActive = groups.Count;
                        groups.Add(item!);
                        continue;
                    }
                    var retained = new List<Option>();
                    bool changed = false;
                    foreach (var option in group.items)
                    {
                        if (option?.Tags?.Any(tag => tag == "saapsPart") == true)
                        {
                            if (seen.Add(option.Name)) aps.Add(option);
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
                }
                if (aps.Count == 0) return;
                groups.Add(new OptionGroup("Active Protection Systems", aps.ToArray()).Cast<ISelectionGroup>());
                __0 = new Il2CppReferenceArray<ISelectionGroup>(groups.ToArray());
                __1 = newActive < 0 ? groups.Count - 1 : newActive;
                if (logs++ < 3) Main.ModLog.LogInfo($"[SAAPS] Menu grouped {aps.Count} APS options under Active Protection Systems.");
            }
            catch (Exception ex)
            {
                if (!warned) { warned = true; Main.ModLog.LogWarning("[SAAPS] APS menu grouping failed; native options retained: " + ex.Message); }
            }
        }
    }
}
