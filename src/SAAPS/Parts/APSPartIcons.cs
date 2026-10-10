using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Sprocket;
using Sprocket.AssetManagement;
using Sprocket.PartImporting;
using UnityEngine;

namespace SprocketAdvancedAPS.Parts
{
    [HarmonyPatch]
    internal static class APSPartIcons
    {
        private static readonly Dictionary<string, Sprite> Sprites = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Texture2D> Textures = new(StringComparer.Ordinal);
        private static readonly HashSet<string> Failed = new(StringComparer.Ordinal);
        private static readonly string[] Ids = { "saaps_drozd1", "saaps_drozd2", "saaps_arena", "saaps_arenam", "saaps_afganit", "saaps_shtora1", "saaps_trophy", "saaps_ironfist", "saaps_amapads", "saaps_zaslin" };

        private static Sprite? Load(string? key)
        {
            int index = Array.IndexOf(Ids, key);
            if (index < 0 && Guid.TryParse(key, out var guid))
                for (int i = 0; i < Ids.Length; i++)
                    if (guid.ToString() == $"a91bb31a-2cf1-4d0a-8e31-{i + 1:000000000000}") { index = i; break; }
            if (index < 0) return null;
            string id = Ids[index];
            if (Failed.Contains(id)) return null;
            if (Sprites.TryGetValue(id, out var cached)) return cached;
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(typeof(Main).Assembly.Location)!, "SprocketAdvancedAPS", "icons", id + ".png");
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                Textures[id] = texture;
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false)) throw new InvalidDataException("Invalid APS icon PNG");
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
                sprite.hideFlags = HideFlags.HideAndDontSave;
                Sprites[id] = sprite;
                return sprite;
            }
            catch (Exception ex)
            {
                Failed.Add(id);
                Main.ModLog.LogWarning($"[SAAPS] Icon unavailable for {id}: {ex.Message}");
                return null;
            }
        }

        private static void Apply(PartDisplayCard? card)
        {
            if (card == null) return;
            var icon = Load(card.Identifier) ?? Load(card.PartGuid);
            if (icon != null) card.Icon = icon;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GameIcons), nameof(GameIcons.GetOrDefault))]
        private static void Lookup(string __0, ref Sprite __result)
        {
            var icon = Load(__0);
            if (icon != null) __result = icon;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(PartDefinitionCardFactory), nameof(PartDefinitionCardFactory.CreateCard))]
        private static void Created(PartDisplayCard __result) => Apply(__result);
        [HarmonyPostfix, HarmonyPatch(typeof(PartDefinitionCardFactory), nameof(PartDefinitionCardFactory.GetAllWithTags), new[] { typeof(Il2CppStringArray) })]
        private static void Listed(Il2CppReferenceArray<IAssetInfo> __result)
        {
            if (__result != null) foreach (var item in __result) Apply(item?.TryCast<PartDisplayCard>());
        }
        [HarmonyPostfix, HarmonyPatch(typeof(PartDefinitionCardFactory), nameof(PartDefinitionCardFactory.GetByIdentifier))]
        private static void Retrieved(IAssetInfo __result) => Apply(__result?.TryCast<PartDisplayCard>());

        internal static void Cleanup()
        {
            foreach (var sprite in Sprites.Values) UnityEngine.Object.Destroy(sprite);
            foreach (var texture in Textures.Values) UnityEngine.Object.Destroy(texture);
            Sprites.Clear(); Textures.Clear(); Failed.Clear();
        }
    }
}
