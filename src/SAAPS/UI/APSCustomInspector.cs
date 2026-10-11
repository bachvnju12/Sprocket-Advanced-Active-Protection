using System.Globalization;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Sprocket.UI;
using Sprocket.Vehicles.PlateStructures;
using Sprocket.Vehicles.PlateStructures.Design;
using SprocketAdvancedAPS.Core;
using SprocketAdvancedAPS.Systems;
using UnityEngine.Events;

namespace SprocketAdvancedAPS.UI;

internal static class APSCustomInspector
{
    private static bool _open = true;
    private static string _message = "";
    private static IntPtr _confirmPlate;
    private static string? _confirmId;
    private static (string Id, string Name)[] _presets = Array.Empty<(string, string)>();
    private static int _selected;
    private static bool _loaded;
    internal static void Clear() { _presets = Array.Empty<(string, string)>(); _loaded = false; _message = ""; _confirmId = null; _confirmPlate = IntPtr.Zero; _selected = 0; }

    [HarmonyPostfix, HarmonyPatch(typeof(PlateStructureEditor), nameof(PlateStructureEditor.OnGUI))]
    private static void Draw(PlateStructureEditor __instance, IGUILayout __0)
    {
        var plate = __instance.Component;
        if (plate == null || APSCustomRuntime.Find(plate) is not { } entry) return;
        var ui = __0.TryCast<IGUIElementDrawer>();
        if (ui == null) return;
        __0.EndAllDropdowns();
        __0.BeginDropdown("Custom APS", _open, DelegateSupport.ConvertDelegate<Il2CppSystem.Action<bool>>((Action<bool>)(v => _open = v))!);
        try
        {
            if (!_loaded) { Reload(); _loaded = true; }
            void Run(Action action)
            {
                try
                {
                    if (!__instance.Active || !__instance.IsValid() || __instance.Gateway?.Editor == null ||
                        __instance.Gateway.Target?.Pointer != plate.Vehicle?.Pointer ||
                        __instance.Component?.Pointer != plate.Pointer || plate.VehicleObject?.Released != false)
                        throw new InvalidOperationException("Part is no longer editable in Designer.");
                    action(); _message = "Done.";
                }
                catch (Exception ex) { _message = ex.Message; Main.ModLog.LogWarning("[SAAPS] Custom APS: " + ex.Message); }
                __instance.RequestRedraw();
            }
            void Change(Func<APSCustomSettings, APSCustomSettings> edit)
            {
                var current = APSCustomRuntime.Find(plate) ?? throw new InvalidOperationException("Custom APS unavailable.");
                APSCustomRuntime.Set(plate, edit(current.Settings));
                plate.blueprintRef.MarkModified();
                _confirmId = null;
            }
            void Input(string label, string value, Action<string> action) => ui.InputField(label, value,
                DelegateSupport.ConvertDelegate<UnityAction<string>>((Action<string>)(v => Run(() => action(v))))!);
            void Number(string label, float value, Func<APSCustomSettings, float, APSCustomSettings> edit) =>
                Input(label, value.ToString("G9", CultureInfo.InvariantCulture), text =>
                {
                    if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) throw new InvalidDataException("Use a finite number with decimal dot.");
                    Change(s => edit(s, v));
                });
            void Button(string label, Action action)
            {
                var tooltip = new UITooltip(label, "");
                ui.Button(label, DelegateSupport.ConvertDelegate<UnityAction>((Action)(() => Run(action)))!, ref tooltip);
            }
            void Select(string label, string[] labels, int index, Action<int> action)
            {
                var list = new Il2CppSystem.Collections.Generic.List<string>();
                foreach (string text in labels) list.Add(text);
                ui.Dropdown(label, list.Cast<Il2CppSystem.Collections.Generic.IReadOnlyList<string>>(), index,
                    DelegateSupport.ConvertDelegate<UnityAction<int>>((Action<int>)(i => Run(() => { if (i >= 0 && i < labels.Length) action(i); })))!, "");
            }
            void Toggle(string label, bool value, Func<APSCustomSettings, bool, APSCustomSettings> edit) =>
                ui.ToggleField(label, value, DelegateSupport.ConvertDelegate<Il2CppSystem.Action<bool>>((Action<bool>)(v => Run(() => Change(s => edit(s, v)))))!, "");

            var s = entry.Settings;
            Input("Name", s.Name, name => Change(x => x with { Name = name }));
            Toggle("Enabled", s.Enabled, (x, v) => x with { Enabled = v });
            Select("Start from profile", APSCustomSettings.Profiles, Array.IndexOf(APSCustomSettings.Profiles, s.BaseProfile), i =>
                Change(x => APSCustomRuntime.FromDefinition(APSDefinitions.All[APSCustomSettings.Profiles[i]]) with { Id = x.Id, Name = x.Name }));
            Select("Type", new[] { "Hard kill", "Soft kill", "Hybrid" }, s.Type, i => Change(x => x with { Type = i }));
            Toggle("Speed-based hard kill", s.SpeedBased, (x, v) => x with { SpeedBased = v });
            Number("Detection range (m)", s.DetectionRange, (x, v) => x with { DetectionRange = v });
            Number("Detection horizontal (full degrees)", s.DetectionHorizontal, (x, v) => x with { DetectionHorizontal = v });
            Number("Detection vertical (full degrees)", s.DetectionVertical, (x, v) => x with { DetectionVertical = v });
            Number("Minimum range (m)", s.RangeMin, (x, v) => x with { RangeMin = v });
            Number("Maximum range (m)", s.RangeMax, (x, v) => x with { RangeMax = v });
            Number("Hard-kill horizontal (full degrees)", s.Horizontal, (x, v) => x with { Horizontal = v });
            Number("Hard-kill vertical (full degrees)", s.Vertical, (x, v) => x with { Vertical = v });
            Number("Jam horizontal (full degrees)", s.JamHorizontal, (x, v) => x with { JamHorizontal = v });
            Number("Jam vertical (full degrees)", s.JamVertical, (x, v) => x with { JamVertical = v });
            Number("Reaction (s)", s.Reaction, (x, v) => x with { Reaction = v });
            Number("Minimum speed (m/s)", s.MinSpeed, (x, v) => x with { MinSpeed = v });
            Number("Maximum speed (m/s)", s.MaxSpeed, (x, v) => x with { MaxSpeed = v });
            Number("Countermeasures (integer)", s.Capacity, (x, v) => x with { Capacity = Integer(v) });
            Number("Reload (s; 0 disables)", s.Reload, (x, v) => x with { Reload = v });
            Number("Cooldown (s)", s.Cooldown, (x, v) => x with { Cooldown = v });
            Number("Concurrent targets (integer)", s.Concurrent, (x, v) => x with { Concurrent = Integer(v) });
            Select("Launch sound", APSCustomSettings.Sounds, Array.IndexOf(APSCustomSettings.Sounds, s.Sound), i => Change(x => x with { Sound = APSCustomSettings.Sounds[i] }));
            for (int i = 0; i < 11; i++)
            {
                int index = i, mask = 1 << i;
                string threat = ((ThreatClass)mask).ToString();
                Toggle(threat + " eligible (class mode)", (s.Threats & mask) != 0, (x, v) => x with { Threats = v ? x.Threats | mask : x.Threats & ~mask });
                Number(threat + " intercept [0..1]", s.Intercept[i], (x, v) => x with { Intercept = Replace(x.Intercept, index, v) });
                Number(threat + " jam [0..1]", s.Jam[i], (x, v) => x with { Jam = Replace(x.Jam, index, v) });
            }
            ui.InfoField("Shape: use native plate vertex/face tools. Forward is local +Z. Presets include shape and APS settings.", 2);
            Button("Save preset", () =>
            {
                var preset = APSCustomRuntime.Capture(plate);
                string path = APSCustomPreset.PathFor(APSCustomRuntime.Folder, preset.Settings.Id);
                if (File.Exists(path))
                {
                    _confirmPlate = plate.Pointer; _confirmId = preset.Settings.Id;
                    throw new IOException("Existing preset: click Confirm overwrite or Save as new.");
                }
                preset.Save(APSCustomRuntime.Folder, false); Reload();
            });
            if (_confirmPlate == plate.Pointer && _confirmId == s.Id)
                Button("Confirm overwrite (backup kept)", () => { APSCustomRuntime.Capture(plate).Save(APSCustomRuntime.Folder, true); _confirmId = null; Reload(); });
            Button("Save as new", () =>
            {
                var preset = APSCustomRuntime.Capture(plate);
                preset = preset with { Settings = preset.Settings with { Id = Guid.NewGuid().ToString("N") } };
                preset.Save(APSCustomRuntime.Folder, false);
                APSCustomRuntime.Set(plate, preset.Settings); plate.blueprintRef.MarkModified(); Reload();
            });
            Button("Refresh preset list", Reload);
            if (_presets.Length > 0)
            {
                Select("Saved preset", _presets.Select(p => p.Name + " [" + p.Id[..8] + "]").ToArray(), _selected, i => _selected = i);
                Button("Load selected preset", () => APSCustomRuntime.Apply(plate,
                    APSCustomPreset.Load(APSCustomPreset.PathFor(APSCustomRuntime.Folder, _presets[_selected].Id))));
            }
            if (_message.Length > 0) ui.InfoField(_message, 2);
        }
        catch (Exception ex) { Main.ModLog.LogWarning("[SAAPS] Custom APS inspector: " + ex); }
        finally { __0.EndAllDropdowns(); }
    }
    private static int Integer(float v) => float.IsFinite(v) && v == MathF.Truncate(v) && v >= 0 && v <= 256 ? (int)v : throw new InvalidDataException("Expected bounded integer.");
    private static float[] Replace(float[] values, int i, float v) { var copy = (float[])values.Clone(); copy[i] = v; return copy; }
    private static void Reload()
    {
        // Index settings only; geometry validation and cloning happen on apply.
        var presets = new List<(string Id, string Name)>();
        if (Directory.Exists(APSCustomRuntime.Folder))
            foreach (string path in Directory.EnumerateFiles(APSCustomRuntime.Folder, "*.json").Take(512))
                try
                {
                    var settings = APSCustomPreset.ReadSettings(path);
                    if (!string.Equals(Path.GetFileName(path), settings.Id + ".json", StringComparison.OrdinalIgnoreCase) || presets.Any(x => x.Id == settings.Id)) continue;
                    presets.Add((settings.Id, settings.Name));
                }
                catch (Exception ex) { Main.ModLog.LogWarning("[SAAPS] Preset skipped: " + Path.GetFileName(path) + ": " + ex.Message); }
        _presets = presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        _selected = Math.Clamp(_selected, 0, Math.Max(0, _presets.Length - 1));
    }
}
