using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SprocketAdvancedAPS.Core;

internal static class CustomPresetCheck
{
    internal static void Run()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        void Reject(Action action)
        {
            try { action(); } catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException) { return; }
            throw new Exception("Invalid custom preset accepted.");
        }
        using var geometry = JsonDocument.Parse("{\"format\":\"freeform\",\"mesh\":{\"vertices\":[0,0,0,1,0,0,0,1,0,0,0,1],\"faces\":[{\"v\":[0,2,1],\"t\":[5,5,5]},{\"v\":[0,1,3],\"t\":[5,5,5]},{\"v\":[0,3,2],\"t\":[5,5,5]},{\"v\":[1,2,3],\"t\":[5,5,5]}],\"edges\":[0,1,0,2]}}");
        var settings = new APSCustomSettings { Name = "APS tự tạo 日本語", Reaction = .125f };
        var preset = new APSCustomPreset(settings, geometry.RootElement.Clone());
        string json = preset.Serialize();
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var copy = APSCustomPreset.Parse(json);
            Check(copy.Settings.Name == settings.Name && copy.Settings.Reaction == .125f && copy.Geometry.GetRawText() == APSCustomPreset.Parse(copy.Serialize()).Geometry.GetRawText(), "Unicode/invariant native geometry round-trip.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        foreach (string field in typeof(APSCustomSettings).GetProperties().Select(p => p.Name))
        {
            var node = JsonNode.Parse(json)!; node["Settings"]!.AsObject().Remove(field);
            Reject(() => APSCustomPreset.Parse(node.ToJsonString()));
        }
        Reject(() => APSCustomSettings.Parse("{}"));
        Reject(() => APSCustomSettings.Parse(JsonSerializer.Serialize(settings).Replace("\"Version\":1", "\"Version\":1,\"Version\":1")));
        foreach (var bad in new[] { settings with { Version = 2 }, settings with { Id = "../other" }, settings with { Type = 3 }, settings with { Sound = "../sample" }, settings with { DetectionRange = float.NaN }, settings with { RangeMin = 200 }, settings with { Horizontal = 361 }, settings with { MinSpeed = 3000 }, settings with { Capacity = 0 }, settings with { Concurrent = 17 }, settings with { Intercept = new[] { .5f } } })
            Reject(bad.Validate);
        Reject(() => APSCustomPreset.PathFor("unused", "../other"));
        Reject(() => APSCustomPreset.Parse(new string(' ', APSCustomPreset.MaxBytes + 1)));
        foreach (string field in new[] { "v", "t" })
        {
            var node = JsonNode.Parse(json)!;
            node["Geometry"]!["mesh"]!["faces"]![0]![field]![0] = field == "v" ? 4 : 0;
            Reject(() => APSCustomPreset.Parse(node.ToJsonString()));
        }
        var invalid = JsonNode.Parse(json)!; invalid["Geometry"]!["mesh"]!["vertices"]![0] = 21;
        Reject(() => APSCustomPreset.Parse(invalid.ToJsonString()));
        foreach (string malformed in new[] { "{}", "{\"format\":\"freeform\",\"mesh\":null}", "{\"format\":\"freeform\",\"format\":\"freeform\",\"mesh\":{}}" })
            Reject(() => APSCustomPreset.ValidateGeometry(JsonDocument.Parse(malformed).RootElement));
        var degenerate = JsonNode.Parse(json)!;
        for (int axis = 0; axis < 3; axis++) degenerate["Geometry"]!["mesh"]!["vertices"]![3 + axis] = 0;
        Reject(() => APSCustomPreset.Parse(degenerate.ToJsonString()));
        string folder = Path.Combine(Path.GetTempPath(), "saaps-preset-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            preset.Save(folder, false);
            string path = APSCustomPreset.PathFor(folder, settings.Id);
            string original = File.ReadAllText(path);
            Reject(() => (preset with { Settings = settings with { Name = "changed" } }).Save(folder, false));
            Reject(() => (preset with { Settings = settings with { Capacity = 0 } }).Save(folder, true));
            Check(File.ReadAllText(path) == original, "Failed write preserves original preset.");
            (preset with { Settings = settings with { Name = "changed" } }).Save(folder, true);
            Check(File.ReadAllText(path + ".bak") == original && APSCustomPreset.Load(path).Settings.Name == "changed", "Atomic overwrite keeps backup.");
            Check(!Directory.EnumerateFiles(folder, "*.tmp").Any(), "No leaked temp files.");
            File.Delete(path);
            Check(APSCustomSettings.Parse(JsonSerializer.Serialize(settings)).Name == settings.Name, "Vehicle settings snapshot needs no preset file.");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        Console.WriteLine("PASS: production Custom APS settings, strict schema, Unicode/locale, mesh bounds, atomic save/backup and standalone snapshot (offline; native placement not exercised).");
    }
}

namespace SprocketAdvancedAPS.Core
{
    // Native marker cache is not exercised by this offline registry check.
    internal static class APSCustomRuntime
    {
        internal sealed record Entry(SprocketAdvancedAPS.Systems.APSDefinition Definition);
        internal static Entry? Find(Sprocket.Vehicles.VehicleComponent component) => null;
    }
}
