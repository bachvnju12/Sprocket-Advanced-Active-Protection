using System.Reflection;
using DesignerFramework.AssetManagement;
using DesignerFramework.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Sprocket.Selection;
using Sprocket.VehicleDesigner.Access;
using Sprocket.VehicleDesigner.UI;
using Sprocket.Vehicles.PlateStructures.Design;
using SprocketAdvancedAPS.Parts;

static class Check
{
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static Option Part(string tag) => new() { Tags = new[] { tag } };
    static OptionGroup Group(string name, params Option[] options) => new(name, options);
    static object?[] Merge(int active, params ISelectionGroup[] groups)
    {
        object?[] args = { new Il2CppReferenceArray<ISelectionGroup>(groups), active };
        typeof(APSPartsMenu).GetMethod("Groups", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
        return args;
    }
    static OptionGroup APS(object?[] result) => ((Il2CppReferenceArray<ISelectionGroup>)result[0]!).OfType<OptionGroup>().Single(g => g.Name == "Active Protection Systems");
    public static void Main()
    {
        var fixedParts = Enumerable.Range(0, 10).Select(_ => Part("saapsPart")).ToArray();
        var custom = Part("saapsCustomPart");
        var vanilla = Part("plateStructurePrefab");
        var original = Group("Vanilla", vanilla);
        var merged = Merge(0, original, Group("Firepower", fixedParts), Group("Add-ons", custom));
        Assert(APS(merged).items.Count == 11 && (int)merged[1]! == 0, "All eleven parts; preserve active vanilla tab");
        Assert(ReferenceEquals(((Il2CppReferenceArray<ISelectionGroup>)merged[0]!)[0], original), "Unchanged group reused");
        var refreshed = Merge((int)merged[1]!, ((Il2CppReferenceArray<ISelectionGroup>)merged[0]!).ToArray());
        Assert(APS(refreshed).items.Count == 11, "Refresh must not duplicate group/options");
        var existingAPS = Group("Active Protection Systems", custom, fixedParts[0]);
        existingAPS.Flags = 7;
        existingAPS.InteractableTest = new object();
        var duplicate = Merge(1, original, existingAPS, Group("Other", custom, Part("saapsCustomPart"), fixedParts[0]));
        Assert(APS(duplicate).items.Count == 2 && (int)duplicate[1]! == 1, "Duplicate Custom/native pointers; preserve active APS");
        Assert(APS(duplicate).Flags == 7 && ReferenceEquals(APS(duplicate).InteractableTest, existingAPS.InteractableTest), "Preserve existing APS metadata");
        var retained = Merge(0, Group("Firepower", vanilla, fixedParts[0]));
        Assert((int)retained[1]! == 0 && ((OptionGroup)((Il2CppReferenceArray<ISelectionGroup>)retained[0]!)[0]).items[0] == vanilla, "Retain native item and tab");
        var noAPS = Merge(0, original);
        Assert(((Il2CppReferenceArray<ISelectionGroup>)noAPS[0]!)[0] == original, "No APS: unchanged");
        var addonOnly = Merge(0, Group("Add-ons", vanilla, custom));
        var addonGroups = (Il2CppReferenceArray<ISelectionGroup>)addonOnly[0]!;
        Assert(addonGroups.Count == 1 && ((OptionGroup)addonGroups[0]).Name == "Add-ons" &&
            ((OptionGroup)addonGroups[0]).items.Single() == vanilla, "Custom must not create a second APS group outside Firepower");
        var customOnly = Merge(0, Group("Add-ons", custom));
        Assert(((Il2CppReferenceArray<ISelectionGroup>)customOnly[0]!).Count == 0, "Remove empty Custom-only group outside Firepower");
        object?[] nullArgs = { null, 4 };
        typeof(APSPartsMenu).GetMethod("Groups", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, nullArgs);
        Assert(nullArgs[0] == null, "Null input");

        Assert(APS(Merge(0, Group("Firepower", fixedParts))).items.Count == 10, "Missing GUI context fails closed");
        var editor = new IVehicleEditor();
        var gui = new VehicleEditorGUIPlugin();
        void Set(IVehicleEditor? value) => typeof(APSPartsMenu).GetMethod("SetEditor", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object?[] { gui, value });
        var designer = new PlateStructureDesigner { Target = editor.Target, plateStructureGuid = "7f8a9d20-eb45-482e-b149-014c964c4e2c", addonPlateStructureGuid = "8f8a9d20-eb45-482e-b149-014c964c4e2c" };
        var hullDesigner = new PlateStructureDesigner { Target = editor.Target, plateStructureGuid = "hull", addonPlateStructureGuid = designer.addonPlateStructureGuid };
        editor.ModuleReader.Add(hullDesigner);
        editor.ModuleReader.Add(designer);
        editor.AssetDatabase.Card = new() { Tags = new[] { "plateStructurePrefab", "saapsCustomPart" } };
        Set(editor);
        var panel = Merge(0, Group("Firepower", fixedParts));
        Assert(APS(panel).items.Count == 11, "Actual panel injects Custom without VehicleAssetSelection buffer");
        Assert(APS(Merge((int)panel[1]!, ((Il2CppReferenceArray<ISelectionGroup>)panel[0]!).ToArray())).items.Count == 11, "Injected panel refresh does not duplicate");
        var injected = APS(panel).items.Single(o => o.Tags!.Contains("saapsCustomPart"));
        injected.OnSelect!(new());
        Assert(designer.LastGuid == SprocketAdvancedAPS.Core.APSCustomRuntime.PartGuid && hullDesigner.LastGuid == null, "Exact prefab callback; reject hull designer");
        designer.LastGuid = null;
        editor.ModuleReader.Clear();
        injected.OnSelect(new());
        Assert(designer.LastGuid == null, "Unloaded designer rejected");
        editor.ModuleReader.Add(designer);
        editor.Target = new();
        injected.OnSelect(new());
        Assert(designer.LastGuid == null, "Changed vehicle rejected");
        editor.Target = designer.Target;
        Set(null);
        injected.OnSelect(new());
        Assert(designer.LastGuid == null, "Closed GUI context rejected");
        Set(editor);
        Set(new IVehicleEditor());
        injected.OnSelect(new());
        Assert(designer.LastGuid == null, "Changed GUI editor rejected");
        Set(editor);
        Assert(((Il2CppReferenceArray<ISelectionGroup>)Merge(0, original)[0]!).Count == 1, "Unrelated picker untouched");
        Assert(APS(Merge(0, Group("Native", fixedParts[0], custom))).items.Count == 2, "Reuse native supplied option");
        editor.AssetDatabase.Card = null;
        Assert(APS(Merge(0, Group("Firepower", fixedParts))).items.Count == 10, "Missing part fails closed");
        editor.AssetDatabase.Card = new() { Tags = new[] { "saapsCustomPart" }, PartGuid = "wrong" };
        Assert(APS(Merge(0, Group("Firepower", fixedParts))).items.Count == 10, "Wrong catalog GUID rejected");
        editor.AssetDatabase.Card.PartGuid = SprocketAdvancedAPS.Core.APSCustomRuntime.PartGuid;
        editor.ModuleReader.Add(new() { Target = editor.Target, plateStructureGuid = designer.plateStructureGuid, addonPlateStructureGuid = designer.addonPlateStructureGuid });
        Assert(APS(Merge(0, Group("Firepower", fixedParts))).items.Count == 10, "Ambiguous designer fails closed");
        typeof(APSPartsMenu).GetMethod("ReleaseEditor", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { gui });
        Assert(APS(Merge(0, Group("Firepower", fixedParts))).items.Count == 10, "Release clears context");
        Console.WriteLine("PASS: production menu grouping, Custom injection, exact prefab callback and stale-context rejection (stand-ins; not native/live proof).");
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)] sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
    sealed class HarmonyPrefix : Attribute { }
    sealed class HarmonyPostfix : Attribute { }
}
namespace Il2CppSystem
{
    public class Object
    {
        static long next;
        public IntPtr Pointer { get; } = new(++next);
        public T? TryCast<T>() where T : class => this as T;
        public T Cast<T>() where T : class => (this as T)!;
    }
}
namespace Il2CppSystem.Collections.Generic
{
    public class List<T> : System.Collections.Generic.List<T> { }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public class Il2CppReferenceArray<T> : List<T> { public Il2CppReferenceArray(T[] items) : base(items) { } }
}
namespace Sprocket.Selection
{
    public interface ISelectionGroup { }
    public static class SelectionCast
    {
        public static T? TryCast<T>(this ISelectionGroup group) where T : class => group as T;
    }
    public class ISelectionBuffer : Il2CppSystem.Object { }
}
namespace DesignerFramework.AssetManagement
{
    public class Option : Il2CppSystem.Object
    {
        public string[]? Tags;
        public Action<Il2CppSystem.Object>? OnSelect;
        public static Option ToOption(Option card) => new() { Tags = card.Tags };
    }
    public class OptionGroup : Il2CppSystem.Object, ISelectionGroup
    {
        public string Name;
        public Il2CppReferenceArray<Option> items;
        public int Flags;
        public object? InteractableTest;
        public OptionGroup(string name, Option[] options) { Name = name; items = new(options); }
    }
}
namespace Sprocket.PartImporting
{
    public class PartDisplayCard : Sprocket.AssetManagement.IAssetInfo { public string PartGuid = SprocketAdvancedAPS.Core.APSCustomRuntime.PartGuid; }
}
namespace DesignerFramework.UI { public class SelectionPanel { public void SetDisplayOptions() { } } }
namespace Sprocket.AssetManagement
{
    public class IAssetInfo : Option { }
}
namespace Sprocket.VehicleDesigner.UI
{
    public class VehicleEditorGUIPlugin : Il2CppSystem.Object { public void SetEditor() { } public void Release() { } }
}
namespace Sprocket.VehicleDesigner.Access
{
    public class Gateway : Il2CppSystem.Object { }
    public class Modules : List<PlateStructureDesigner>
    {
        public void GetAll(Il2CppSystem.Collections.Generic.List<PlateStructureDesigner> output) => output.AddRange(this);
    }
    public class Database
    {
        public Sprocket.PartImporting.PartDisplayCard? Card;
        public Sprocket.AssetManagement.IAssetInfo[] GetAllWithTags(string[] tags) => Card == null ? Array.Empty<Sprocket.AssetManagement.IAssetInfo>() : new Sprocket.AssetManagement.IAssetInfo[] { Card };
    }
    public class IVehicleEditor : Il2CppSystem.Object
    {
        public Gateway? Target = new();
        public Modules ModuleReader = new();
        public Database AssetDatabase = new();
    }
}
namespace Sprocket.Vehicles.PlateStructures.Design
{
    public class PlateStructureDesigner : Il2CppSystem.Object
    {
        public string? plateStructureGuid;
        public string? addonPlateStructureGuid;
        public Gateway? Target;
        public string? LastGuid;
        public void LoadPrefabPlateStructure(string guid) => LastGuid = guid;
    }
}
namespace SprocketAdvancedAPS.Core { static class APSCustomRuntime { public const string PartGuid = "a91bb31a-2cf1-4d0a-8e31-000000000011"; } }
namespace SprocketAdvancedAPS
{
    static class Main { public static Log ModLog = new(); }
    class Log { public void LogInfo(string text) { } public void LogWarning(string text) { } }
}
