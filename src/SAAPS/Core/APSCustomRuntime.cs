using System.Text.Json;
using BepInEx;
using HarmonyLib;
using Sprocket.Vehicles;
using Sprocket.Vehicles.PlateStructures;
using Sprocket.Vehicles.PlateStructures.Design;
using SprocketAdvancedAPS.Systems;
using SerializationInfo = Il2CppSystem.Runtime.Serialization.SerializationInfo;

namespace SprocketAdvancedAPS.Core;

internal static class APSCustomRuntime
{
    internal const string PartGuid = "a91bb31a-2cf1-4d0a-8e31-000000000011";
    private const string Key = "saapsCustomAPS_v1";
    internal sealed record Entry(PlateStructure Plate, IntPtr Owner, APSCustomSettings Settings, APSDefinition Definition);

    private static readonly Dictionary<IntPtr, Entry> Entries = new();
    private sealed class PlacementCall { internal IntPtr Designer; internal bool Marked; }
    [ThreadStatic] private static Stack<PlacementCall>? Placement;
    internal static string Folder => Path.Combine(Paths.ConfigPath, "SprocketAdvancedAPS", "CustomAPS");
    internal static Entry? Find(VehicleComponent component) =>
        Entries.TryGetValue(component.Pointer, out var entry) &&
        component.VehicleObject?.Released == false && component.Vehicle?.Pointer == entry.Owner &&
        entry.Plate.VehicleObject?.Released == false && entry.Plate.Vehicle?.Pointer == entry.Owner ? entry : null;
    internal static void Clear() { Entries.Clear(); Placement?.Clear(); }

    internal static APSCustomSettings FromDefinition(APSDefinition d) => new()
    {
        BaseProfile = d.Id, Name = "Custom " + d.Name, Type = (int)d.Type,
        DetectionRange = d.DetectionRange, DetectionHorizontal = d.DetectionFOVHorizontal, DetectionVertical = d.DetectionFOVVertical,
        RangeMin = d.EngagementRangeMin, RangeMax = d.EngagementRangeMax, Horizontal = d.EngagementFOVHorizontal, Vertical = d.EngagementFOVVertical,
        SpeedBased = APSCore.SpeedBasedInterception(d), Reaction = d.ReactionTime, MinSpeed = APSCore.MinInterceptSpeed(d), MaxSpeed = APSCore.MaxInterceptSpeed(d),
        Capacity = d.CountermeasureCapacity, Reload = d.ReloadTime, Cooldown = d.CooldownBetweenShots,
        Concurrent = Math.Clamp(d.MaxSimultaneousEngagements, 1, 16), Threats = d.SupportedThreats == ThreatClass.All ? 2047 : (int)d.SupportedThreats,
        Intercept = Enumerable.Range(0, 11).Select(i => d.InterceptProbability.GetValueOrDefault((ThreatClass)(1 << i), .5f)).ToArray(),
        Jam = Enumerable.Range(0, 11).Select(i => d.SoftKillProbability.GetValueOrDefault((ThreatClass)(1 << i), 0f)).ToArray(),
        Sound = d.Id is "drozd1" or "drozd2" or "afganit" ? "rocket" : d.Id == "trophy" ? "launcher" : "shot"
    };

    internal static void Set(PlateStructure plate, APSCustomSettings settings)
    {
        settings.Validate();
        if (plate.VehicleObject?.Released != false || plate.Vehicle == null) throw new InvalidOperationException("Plate has no live owner.");
        var d = new APSDefinition
        {
            Id = "custom_" + settings.Id, Name = settings.Name, Type = (APSType)settings.Type,
            CustomSettings = settings, DetectionRange = settings.DetectionRange,
            DetectionFOVHorizontal = settings.DetectionHorizontal, DetectionFOVVertical = settings.DetectionVertical,
            MinDetectionSpeed = settings.MinSpeed, MaxDetectionSpeed = settings.MaxSpeed,
            EngagementRangeMin = settings.RangeMin, EngagementRangeMax = settings.RangeMax,
            EngagementFOVHorizontal = settings.Horizontal, EngagementFOVVertical = settings.Vertical,
            JamFOVHorizontal = settings.JamHorizontal, JamFOVVertical = settings.JamVertical,
            MinEngagementSpeed = settings.MinSpeed, MaxEngagementSpeed = settings.MaxSpeed, ReactionTime = settings.Reaction,
            CountermeasureCapacity = settings.Capacity, ReloadTime = settings.Reload, CooldownBetweenShots = settings.Cooldown,
            MaxSimultaneousEngagements = settings.Concurrent, CanEngageMultiple = settings.Concurrent > 1,
            SupportedThreats = (ThreatClass)settings.Threats,
            InterceptProbability = Enumerable.Range(0, 11).ToDictionary(i => (ThreatClass)(1 << i), i => settings.Intercept[i]),
            SoftKillProbability = Enumerable.Range(0, 11).ToDictionary(i => (ThreatClass)(1 << i), i => settings.Jam[i]),
            LaunchSound = "audio/" + settings.Sound + ".wav"
        };
        Entries[plate.Pointer] = new(plate, plate.Vehicle.Pointer, settings, d);
        if (plate.Vehicle?.TryCast<Vehicle>() is { } vehicle) APSCore.Instance?.ModulesChanged(vehicle);
    }

    internal static APSCustomPreset Capture(PlateStructure plate)
    {
        var entry = Find(plate) ?? throw new InvalidOperationException("Not a Custom APS.");
        using var geometry = JsonDocument.Parse(plate.Mesh.ToJson(false));
        return new(entry.Settings, geometry.RootElement.Clone());
    }

    internal static void Apply(PlateStructure plate, APSCustomPreset preset)
    {
        preset.Validate();
        var old = Capture(plate);
        // ponytail: only unique live meshes; native vertex edit separates palette copies. Add copy-on-write only with verified mesh registration API.
        var objects = plate.Vehicle.ObjectReader.Items;
        int count = objects.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<VehicleObject>>().Count;
        for (int i = 0; i < count; i++)
        {
            var obj = objects[i];
            if (obj == null || obj.Released) continue;
            var components = obj.Components;
            if (components == null) continue;
            int n = components.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<VehicleComponent>>().Count;
            for (int j = 0; j < n; j++)
                if (components[j]?.TryCast<PlateStructure>() is { } other && other.Pointer != plate.Pointer && other.Mesh?.Pointer == plate.Mesh.Pointer)
                    throw new InvalidOperationException("Shared plate mesh: make a native vertex edit on this part before loading a preset; other parts will not be changed.");
        }
        // Deserialize in an unattached mesh first, never mutate the target with unvalidated native data.
        var trial = new PlateStructureMesh();
        trial.FromJson(preset.Geometry.GetRawText());
        var blueprint = trial.ToMeshBlueprint(false);
        if (blueprint.Faulted) throw new InvalidDataException(blueprint.FaultReason);
        try
        {
            plate.Mesh.LoadBlueprint(blueprint);
            plate.RequestMeshRefresh();
            Set(plate, preset.Settings);
            plate.blueprintRef.MarkModified();
        }
        catch (Exception applyError)
        {
            try
            {
                plate.Mesh.FromJson(old.Geometry.GetRawText());
                plate.RequestMeshRefresh();
                Set(plate, old.Settings);
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException("Preset apply and rollback failed; do not save this vehicle over its original.", applyError, rollbackError);
            }
            throw;
        }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(PlateStructureDesigner), nameof(PlateStructureDesigner.LoadPrefabPlateStructure), new[] { typeof(string) })]
    private static void BeginPrefab(PlateStructureDesigner __instance, string __0, out bool __state)
    {
        __state = string.Equals(__0, PartGuid, StringComparison.OrdinalIgnoreCase);
        if (__state) (Placement ??= new()).Push(new PlacementCall { Designer = __instance.Pointer });
    }
    [HarmonyFinalizer, HarmonyPatch(typeof(PlateStructureDesigner), nameof(PlateStructureDesigner.LoadPrefabPlateStructure), new[] { typeof(string) })]
    private static Exception? EndPrefab(Exception? __exception, bool __state)
    {
        if (__state && Placement?.Count > 0 && !Placement.Pop().Marked)
            Main.ModLog.LogWarning("[SAAPS] Custom APS placement did not create a verified new plate; no APS marker applied. Placement path requires live verification.");
        return __exception;
    }
    [HarmonyPostfix, HarmonyPatch(typeof(PlateStructureDesigner), nameof(PlateStructureDesigner.NewStructure))]
    private static void Created(PlateStructureDesigner __instance, PlateStructure __result) => MarkCreated(__instance, __result);
    [HarmonyPostfix, HarmonyPatch(typeof(PlateStructureDesigner), nameof(PlateStructureDesigner.NewStructureWithFoundation))]
    private static void CreatedWithFoundation(PlateStructureDesigner __instance, PlateStructure __result) => MarkCreated(__instance, __result);
    private static void MarkCreated(PlateStructureDesigner designer, PlateStructure plate)
    {
        if (Placement == null || Placement.Count == 0 || Placement.Peek().Designer != designer.Pointer || plate == null || Find(plate) != null) return;
        try
        {
            Set(plate, FromDefinition(APSDefinitions.All["trophy"]));
            Placement.Peek().Marked = true;
            Main.ModLog.LogInfo("[SAAPS] Custom APS marker applied to newly created plate.");
        }
        catch (Exception ex) { Main.ModLog.LogWarning("[SAAPS] Custom APS marker rejected: " + ex.Message); }
    }
    internal static void CheckNativeJsonRoundTrip()
    {
        // Synthetic state only; never load a user vehicle or alter scene objects.
        const string fixture = "{\"v\":\"2.0\",\"header\":{\"v\":\"0.2\",\"name\":\"SAAPS serialization check\",\"gameVersion\":\"0.2.55.5\",\"mass\":0,\"creationDate\":\"1945.01.01\",\"class\":\"\",\"desc\":\"\",\"cost\":0},\"blueprints\":[],\"meshes\":[],\"objects\":[{\"guid\":\"00000000-0000-0000-0000-000000000000\",\"vuid\":1,\"pvuid\":-1,\"flags\":0,\"vanillaSentinel\":2671}]}";
        var serializer = new VehicleBlueprintSerializer();
        var blueprint = serializer.DeserializeJSON(fixture);
        var captured = new SerializationInfo(Il2CppInterop.Runtime.Il2CppType.Of<Il2CppSystem.Object>(),
            new Il2CppSystem.Runtime.Serialization.FormatterConverter().Cast<Il2CppSystem.Runtime.Serialization.IFormatterConverter>());
        var entries = blueprint.VehicleObjects[0].State.GetEnumerator();
        while (entries.MoveNext())
            if (entries.Name is not ("guid" or "vuid" or "pvuid" or "flags")) captured.AddValue(entries.Name, entries.Value, entries.ObjectType);
        var settings = new APSCustomSettings { Name = "APS tự tạo", Enabled = false };
        captured.AddValue(Key, JsonSerializer.Serialize(settings));
        blueprint.VehicleObjects[0].State = captured;
        var restored = serializer.DeserializeJSON(serializer.SerializeToJSON(blueprint, true)).VehicleObjects[0].State;
        var copy = APSCustomSettings.Parse(restored.GetString(Key));
        if (copy.Id != settings.Id || copy.Name != settings.Name || copy.Enabled || !copy.Intercept.SequenceEqual(settings.Intercept) || restored.GetInt32("vanillaSentinel") != 2671)
            throw new InvalidOperationException("Native Custom APS settings round-trip failed.");
    }

    [HarmonyPostfix, HarmonyPatch(typeof(VehicleComponent), nameof(VehicleComponent.SaveData))]
    private static void Save(VehicleComponent __instance, SerializationInfo __0)
    {
        if (Find(__instance) is { } entry) __0.AddValue(Key, JsonSerializer.Serialize(entry.Settings));
    }
    [HarmonyPostfix, HarmonyPatch(typeof(VehicleComponent), nameof(VehicleComponent.LoadData))]
    private static void Load(VehicleComponent __instance, SerializationInfo __0)
    {
        if (__instance.TryCast<PlateStructure>() is not { } plate) return;
        Entries.Remove(plate.Pointer);
        try
        {
            var values = __0.GetEnumerator();
            while (values.MoveNext())
                if (values.Name == Key)
                {
                    string json = __0.GetString(Key);
                    Set(plate, APSCustomSettings.Parse(json));
                    break;
                }
        }
        catch (Exception ex) { Main.ModLog.LogWarning("[SAAPS] Custom APS settings rejected: " + ex.Message); }
    }
    [HarmonyPrefix, HarmonyPatch(typeof(VehicleComponent), nameof(VehicleComponent.ReleaseInternal))]
    private static void Release(VehicleComponent __instance)
    {
        if (!Entries.Remove(__instance.Pointer)) return;
        if (__instance.Vehicle?.TryCast<Vehicle>() is { } vehicle) APSCore.Instance?.ModulesChanged(vehicle);
    }
}
