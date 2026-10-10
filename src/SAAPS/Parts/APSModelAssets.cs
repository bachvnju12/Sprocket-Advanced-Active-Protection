using System.Text.Json;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Sprocket.Vehicles;
using Sprocket.Vehicles.AssetManagement;
using Sprocket.Vehicles.AttachedBehaviours;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SprocketAdvancedAPS.Parts
{
    internal static class APSModelAssets
    {
        private static readonly Dictionary<string, Mesh> Meshes = new(StringComparer.Ordinal);
        private static readonly HashSet<string> Failed = new(StringComparer.Ordinal);
        private static readonly List<Material> OwnedMaterials = new();
        private static VehicleMaterial[]? Surfaces;
        private static Texture2D? Atlas;
        private static bool SurfaceFailed;
        private static string AssetRoot => Path.Combine(Path.GetDirectoryName(typeof(Main).Assembly.Location)!, "SprocketAdvancedAPS");

        [HarmonyPrefix, HarmonyPatch(typeof(VehicleObjectModel), nameof(VehicleObjectModel.Build))]
        private static void Building(VehicleObjectModel __instance)
        {
            if (APSPartRegistry.IsAPSComponent(__instance)) __instance.staticRenderer = false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(VehicleObjectModel), nameof(VehicleObjectModel.OnModelLoaded))]
        private static void Loaded(VehicleObjectModel __instance) => Apply(__instance);

        private static VehicleMaterial[]? Materials()
        {
            if (Surfaces != null || SurfaceFailed) return Surfaces;
            try
            {
                var shader = Shader.Find("HDRP/Lit") ?? throw new InvalidOperationException("HDRP/Lit unavailable");
                Atlas = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "SAAPS surface atlas", hideFlags = HideFlags.HideAndDontSave };
                if (!ImageConversion.LoadImage(Atlas, File.ReadAllBytes(Path.Combine(AssetRoot, "textures", "saaps_surfaces.png")), false)
                    || Atlas.width != 512 || Atlas.height != 512) throw new InvalidDataException("Invalid SAAPS surface atlas");
                Atlas.wrapMode = TextureWrapMode.Clamp;
                Atlas.filterMode = FilterMode.Trilinear;
                var surfaces = new VehicleMaterial[3];
                for (int i = 0; i < surfaces.Length; i++)
                {
                    var material = new Material(shader) { name = "SAAPS " + new[] { "paint", "dark metal", "sensor face" }[i], hideFlags = HideFlags.HideAndDontSave };
                    OwnedMaterials.Add(material);
                    material.SetTexture("_BaseColorMap", Atlas);
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_Smoothness", i == 0 ? .24f : i == 1 ? .38f : .65f);
                    material.SetFloat("_Metallic", i == 1 ? .65f : 0f);
                    HDMaterial.ValidateMaterial(material);
                    surfaces[i] = new VehicleMaterial(material) { Flags = VehicleMaterialFlags.Unpainted | VehicleMaterialFlags.DisableGrime | VehicleMaterialFlags.CustomShader };
                }
                Surfaces = surfaces;
            }
            catch (Exception ex)
            {
                SurfaceFailed = true;
                ReleaseSurfaces();
                Main.ModLog.LogWarning($"[SAAPS] Textured surfaces unavailable; keeping native material: {ex.Message}");
            }
            return Surfaces;
        }

        private static Mesh Load(string id)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AssetRoot, "meshes", id + ".json")));
            var root = document.RootElement;
            var positions = root.GetProperty("vertices").EnumerateArray().Select(v => v.GetSingle()).ToArray();
            if (positions.Length < 24 || positions.Length % 3 != 0 || positions.Length > 150000
                || positions.Any(v => !float.IsFinite(v) || Math.Abs(v) > 2)) throw new InvalidDataException("Invalid APS mesh vertices");
            var vertices = new Vector3[positions.Length / 3];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = new Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
            bool textured = root.TryGetProperty("uv", out var uvData);
            Vector2[]? uv = null;
            Vector3[]? normals = null;
            if (textured)
            {
                var coordinates = uvData.EnumerateArray().Select(v => v.GetSingle()).ToArray();
                var directions = root.GetProperty("normals").EnumerateArray().Select(v => v.GetSingle()).ToArray();
                if (coordinates.Length != vertices.Length * 2 || coordinates.Any(v => !float.IsFinite(v) || v < 0 || v > 1)
                    || directions.Length != positions.Length || directions.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Invalid APS UV/normal data");
                uv = new Vector2[vertices.Length]; normals = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    uv[i] = new Vector2(coordinates[2 * i], coordinates[2 * i + 1]);
                    normals[i] = new Vector3(directions[3 * i], directions[3 * i + 1], directions[3 * i + 2]);
                    if (Math.Abs(normals[i].sqrMagnitude - 1) > .01f) throw new InvalidDataException("Invalid APS normal length");
                }
            }
            var triangles = Enumerable.Range(0, textured ? 3 : 1).Select(_ => new List<int>()).ToArray();
            int triangleCount = 0;
            foreach (var face in root.GetProperty("faces").EnumerateArray())
            {
                var indices = face.GetProperty("v").EnumerateArray().Select(v => v.GetInt32()).ToArray();
                int slot = textured ? face.GetProperty("surface").GetInt32() : 0;
                if (slot < 0 || slot >= triangles.Length || indices.Length < 3 || indices.Any(v => v < 0 || v >= vertices.Length)) throw new InvalidDataException("Invalid APS mesh face");
                // Stored faces use inward winding; the renderer needs the reversed fan.
                for (int i = 2; i < indices.Length; i++)
                {
                    if (++triangleCount > 8000 || Vector3.Cross(vertices[indices[i]] - vertices[indices[0]], vertices[indices[i - 1]] - vertices[indices[0]]).sqrMagnitude < 1e-20f)
                        throw new InvalidDataException("Invalid APS triangle geometry/budget");
                    triangles[slot].AddRange(new[] { indices[0], indices[i], indices[i - 1] });
                }
            }
            if (triangleCount < 12) throw new InvalidDataException("Empty APS mesh");
            var mesh = new Mesh { name = id, hideFlags = HideFlags.HideAndDontSave };
            try
            {
                mesh.vertices = vertices;
                mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i].ToArray(), i);
                if (textured) { mesh.uv = uv!; mesh.normals = normals!; mesh.RecalculateTangents(); }
                else mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
            catch { UnityEngine.Object.Destroy(mesh); throw; }
        }

        internal static void Apply(VehicleComponent component)
        {
            if (!APSPartRegistry.IsAPSComponent(component)) return;
            var model = component.TryCast<VehicleObjectModel>();
            var native = model?.Model;
            if (model == null || native == null) return;
            string id = component.ComponentID;
            if (Failed.Contains(id)) return;
            try
            {
                if (!Meshes.TryGetValue(id, out var mesh)) { mesh = Load(id); Meshes.Add(id, mesh); }
                bool changed = native.InteractionMesh != mesh;
                if (changed)
                {
                    model.MarkDynamic();
                    native.SetMesh(mesh); native.SetShadowProxy(mesh);
                    native.SetInteractionMesh(mesh); native.SetCollisionMesh(mesh);
                }
                if (mesh.subMeshCount == 3)
                {
                    var surfaces = Materials();
                    var current = native.Materials;
                    if (surfaces != null)
                    {
                        if (current == null || current.Length != 3 || Enumerable.Range(0, 3).Any(i => current[i]?.Pointer != surfaces[i].Pointer))
                            native.SetMaterials(new Il2CppReferenceArray<VehicleMaterial>(surfaces));
                    }
                    else if (current == null || current.Length != 3)
                    {
                        var fallback = current != null && current.Length > 0 ? current[0] : native.Material;
                        if (fallback != null) native.SetMaterials(new Il2CppReferenceArray<VehicleMaterial>(new[] { fallback, fallback, fallback }));
                    }
                }
                if (changed) Main.ModLog.LogInfo($"[SAAPS] Model ready: component={id}; object={model.VehicleObject?.EntityID}; vertices={mesh.vertexCount}; surfaces={mesh.subMeshCount}.");
            }
            catch (Exception ex)
            {
                Failed.Add(id);
                Main.ModLog.LogError($"[SAAPS] Model replacement failed for {id}: {ex.Message}");
            }
        }

        private static void ReleaseSurfaces()
        {
            foreach (var material in OwnedMaterials) UnityEngine.Object.Destroy(material);
            OwnedMaterials.Clear(); Surfaces = null;
            if (Atlas != null) UnityEngine.Object.Destroy(Atlas);
            Atlas = null;
        }

        internal static void Cleanup()
        {
            foreach (var mesh in Meshes.Values) UnityEngine.Object.Destroy(mesh);
            Meshes.Clear(); Failed.Clear();
            ReleaseSurfaces(); SurfaceFailed = false;
        }
    }
}
