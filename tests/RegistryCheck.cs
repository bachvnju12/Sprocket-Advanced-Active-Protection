using System.Text.Json;
using Sprocket.Vehicles;
using SprocketAdvancedAPS.Parts;

internal static class RegistryCheck
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void CheckInterceptionRules()
    {
        foreach (float speed in new[] { 50f, 599f, 600f })
            Check(SprocketAdvancedAPS.Systems.APSInterceptRules.Eligible(speed, 50f, 600f, false, true), "Speed mode accepts slow unsupported classes and inclusive ceiling");
        foreach (float speed in new[] { 49f, 601f, 0f, -1f, float.NaN, float.PositiveInfinity })
            Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Eligible(speed, 50f, 600f, true, true), "Invalid/outside speed rejected");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Eligible(50f, 50f, 600f, false, false), "Class mode still rejects unsupported ammunition");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Eligible(50f, 50f, float.NaN, true, true), "Invalid ceiling rejected");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.Approaching(3600f, 36000f, 360000f, 12f), "Incoming 600m/s at 60m accepted");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Approaching(3600f, -36000f, 360000f, 12f), "Receding shot rejected");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Approaching(3600f, 0f, 360000f, 12f), "Crossing shot rejected");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Approaching(4000f, 36000f, 360000f, 12f), "Trajectory misses protection radius");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.ReactionReady(0.08f, 0f, 0.09f), "Reaction not ready at range entry");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.ReactionReady(0.09f, 0f, 0.09f), "Reaction ready at 6m with radar timestamp preserved");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.ReactionReady(0.09f, 0.08f, 0.09f), "Reset-at-entry regression would miss");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.ReactionSatisfied(2f, 1f, 0.045f, 30f), "ReactionSatisfied keeps the normal delayed path");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.ReactionSatisfied(0.02f, 0.01f, 0.045f, 0.04f), "Short window before impact still engages");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.ReactionSatisfied(0.02f, 0.01f, 0.045f, 0.045f), "Window equal to reaction delay engages");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.ReactionSatisfied(0.02f, 0.01f, 0.045f, 0.05f), "Window longer than reaction delay must still wait");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.ReactionSatisfied(0.02f, 0.01f, 0.045f, -1f), "Invalid time-to-impact falls back to the reaction gate");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.ReactionSatisfied(0.02f, 0.01f, 0.045f, float.PositiveInfinity), "Infinite time-to-impact falls back to the reaction gate");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.Probability(0f, 0.1f, true) == 0.1f, "Speed mode removes hidden zero class veto");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.Probability(0f, 0.1f, false) == 0f, "Class mode preserves zero probability");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.Ready(true, true, true, false, 1f, 4, 0f, false), "Ready hard-kill allowed");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Ready(false, true, true, false, 1f, 20, 0f, false), "Soft-kill cannot release projectiles");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Ready(true, true, true, false, 1f, 0, 0f, false), "Empty ammo blocked");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Ready(true, true, true, false, 1f, 4, 0.8f, false), "Cooldown blocked");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Ready(true, true, true, false, 1f, 4, 0f, true), "Own shot blocked");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Ready(true, false, true, false, 1f, 4, 0f, false), "Disabled blocked");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Ready(true, true, true, true, 1f, 4, 0f, false), "Destroyed blocked");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.Ready(true, true, true, false, float.NaN, 4, 0f, false), "Invalid health blocked");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.JamReady(true, true, true, false, 1f, 20, 0f, false), "Soft-kill system may jam");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.JamReady(false, true, true, false, 1f, 20, 0f, false), "Hard-kill system cannot jam");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.JamReady(true, true, true, false, 1f, 0, 0f, false), "Jam blocked without countermeasures");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.JamReady(true, true, true, false, 1f, 20, 1.2f, false), "Jam blocked during cooldown");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.JamReady(true, true, true, false, 1f, 20, 0f, true), "Jam blocked on own round");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.DeflectionRate(0f) > 0f, "Weakest jam still deflects");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.DeflectionRate(1f) > SprocketAdvancedAPS.Systems.APSInterceptRules.DeflectionRate(0.3f), "Stronger jam turns harder");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.DeflectionWeight(3.7f, 0.02f) > 0f, "Normal tick turns the round");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.DeflectionWeight(3.7f, 10f) == 1f, "Long frame clamps instead of teleporting");
        foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            Check(SprocketAdvancedAPS.Systems.APSInterceptRules.DeflectionWeight(bad, 0.02f) == 0f, "Invalid rate leaves velocity untouched: " + bad);
        foreach (float bad in new[] { 0f, -0.02f, float.NaN })
            Check(SprocketAdvancedAPS.Systems.APSInterceptRules.DeflectionWeight(3.7f, bad) == 0f, "Invalid delta leaves velocity untouched: " + bad);
        foreach (int flags in new[] { 1, 17, 33, 65 })
            Check(SprocketAdvancedAPS.Systems.APSInterceptRules.IsLiveNative(flags), "Active pooled slot must be visible to the snapshot: " + flags);
        foreach (int flags in new[] { 0, 2, 3, 4, 8, 14, 15 })
            Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.IsLiveNative(flags), "Released/held slot must be ignored: " + flags);
        Console.WriteLine("PASS: production speed, trajectory, reaction, readiness, jamming, deflection and native slot-liveness rules (offline, no native projectile release).");
    }

    private static void CheckJamCone()
    {
        const float h = SprocketAdvancedAPS.Systems.APSInterceptRules.JamHorizontalFOV;
        const float v = SprocketAdvancedAPS.Systems.APSInterceptRules.JamVerticalFOV;
        bool Cone(float x, float y, float z) => SprocketAdvancedAPS.Systems.APSInterceptRules.InCone(x, y, z, h, v);
        (float X, float Y, float Z) Direction(double yaw, double pitch = 0)
        {
            yaw *= Math.PI / 180; pitch *= Math.PI / 180;
            return ((float)(Math.Sin(yaw) * Math.Cos(pitch)), (float)Math.Sin(pitch), (float)(Math.Cos(yaw) * Math.Cos(pitch)));
        }
        foreach (var angles in new[] { (0d, 0d), (15d, 0d), (-15d, 0d), (0d, 10d), (0d, -10d), (15d, 10d) })
        {
            var d = Direction(angles.Item1, angles.Item2);
            Check(Cone(d.X, d.Y, d.Z), "Inclusive jam cone: " + angles);
        }
        foreach (var angles in new[] { (15.01d, 0d), (-15.01d, 0d), (0d, 10.01d), (0d, -10.01d), (60d, 0d), (120d, 0d), (180d, 0d) })
        {
            var d = Direction(angles.Item1, angles.Item2);
            Check(!Cone(d.X, d.Y, d.Z), "Outside jam cone: " + angles);
        }
        Check(!Cone(0, 0, 0) && !Cone(float.NaN, 0, 1) && !Cone(0, float.PositiveInfinity, 1), "Invalid direction rejected");
        Check(Cone(float.MaxValue / 8, 0, float.MaxValue), "Large finite direction avoids squared overflow");
        foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, 361f })
            Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.InCone(0, 0, 1, bad, v), "Invalid horizontal FOV rejected");
        foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, 181f })
            Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.InCone(0, 0, 1, h, bad), "Invalid vertical FOV rejected");
        var rotation = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, MathF.PI / 2);
        var world = System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ, rotation);
        var local = System.Numerics.Vector3.Transform(world, System.Numerics.Quaternion.Inverse(rotation));
        Check(Cone(local.X, local.Y, local.Z) && !Cone(world.X, world.Y, world.Z), "Cone follows emitter rotation");
        var side = Direction(120);
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.InCone(side.X, side.Y, side.Z, 350, 65) && !Cone(side.X, side.Y, side.Z), "Hybrid hard-kill cone stays wide");
        Check(SprocketAdvancedAPS.Systems.APSInterceptRules.JamHeld(true, true, false, 1, false), "Held jam needs no additional ammo");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.JamHeld(false, true, false, 1, false), "Disabled emitter stops jam");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.JamHeld(true, false, false, 1, false), "Inoperable emitter stops jam");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.JamHeld(true, true, true, 1, false), "Destroyed emitter stops jam");
        Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.JamHeld(true, true, false, 1, true), "Friendly round cannot remain jammed");
        foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            Check(!SprocketAdvancedAPS.Systems.APSInterceptRules.JamHeld(true, true, false, bad, false), "Invalid health stops jam");
        Console.WriteLine("PASS: production jam cone boundaries, invalid inputs, rotation math, hybrid separation and held-jam readiness (offline).");
    }

    public static void Main(string[] args)
    {
        Check(args.Length is 1 or 3, "Pass generated Parts directory, optionally --write-audio and output folder");
        if (args.Length == 3)
        {
            Check(args[1] == "--write-audio", "Unknown test argument");
            Directory.CreateDirectory(args[2]);
            foreach (string profile in new[] { "shot", "rocket", "launcher" })
                File.WriteAllBytes(Path.Combine(args[2], profile + ".wav"), WaveCheck.Encode(44100, SprocketAdvancedAPS.Systems.APSWave.Synthesize(profile)));
        }
        CheckInterceptionRules();
        CheckJamCone();
        CustomPresetCheck.Run();
        WaveCheck.Run();
        int count = 0;
        foreach (string path in Directory.GetFiles(args[0], "saaps_*Part.json"))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var components = json.RootElement.GetProperty("components");
            Check(components.GetArrayLength() == 1, "Exactly one identity-bearing component per APS");
            var component = components[0];
            if (json.RootElement.GetProperty("name").GetString() == "saaps_custom")
            {
                Check(json.RootElement.GetProperty("guid").GetString() == "a91bb31a-2cf1-4d0a-8e31-000000000011" && component.GetProperty("type").GetString() == "plateStructurePrefab", "Custom APS uses separate editable schema.");
                Check(APSPartRegistry.GetModuleDefinition(new VehicleComponent { ComponentID = "plateStructurePrefab" }) == null, "Unmarked plate prefab must not become APS.");
                continue;
            }
            Check(component.GetProperty("type").GetString() == "model", "Prefab conversion must not discard APS identity");
            string id = component.GetProperty("fileID").GetString()!;
            var definition = APSPartRegistry.GetModuleDefinition(new VehicleComponent { ComponentID = id });
            Check(definition?.Id == id[6..], "Generated component ID must resolve through production registry");
            Check(APSPartRegistry.IsAPSComponent(new VehicleComponent { ComponentID = id }), "Discovery predicate must accept APS model");
            count++;
        }
        Check(count == 10, "Expected ten APS systems");
        foreach (string? id in new string?[] { null, "plateStructure", "plateStructurePrefab", "model", "saaps_unknown", "saaps_trophy_extra", "SAAPS_trophy" })
            Check(APSPartRegistry.GetModuleDefinition(new VehicleComponent { ComponentID = id }) == null, "Reject vanilla/unknown identity: " + id);
        Console.WriteLine("PASS: production registry resolves 10 generated model IDs and rejects vanilla/unknown IDs. Native placement still needs live game verification.");
    }
}

// Offline stand-ins; production registry above is compiled unchanged. No native game code runs.
namespace BepInEx.Logging { public sealed class ManualLogSource { } }
namespace Sprocket.Vehicles { public sealed class VehicleComponent { public string? ComponentID { get; init; } } }
namespace SprocketAdvancedAPS.Systems
{
    internal sealed record APSDefinition(string Id);
    internal static class APSDefinitions
    {
        internal static bool TryGet(string id, out APSDefinition definition)
        {
            definition = new APSDefinition(id);
            return new[] { "drozd1", "drozd2", "arena", "arenam", "afganit", "shtora1", "trophy", "ironfist", "amapads", "zaslin" }.Contains(id);
        }
    }
}
