using System.IO;
using UnityEditor;
using UnityEngine;

namespace TwoInPot.Editor
{
    // Bake the final displayed size, instead of stretching differently sized source pixels.
    internal static class PixelDensityBaker
    {
        const string Folder = "Assets/TwoInPot/Art/Pixel/Density";
        public static Sprite HammerPart(bool head)
        {
            var input = new Texture2D(2, 2); input.LoadImage(File.ReadAllBytes("Assets/TwoInPot/Art/Pixel/Source/Hammer.png"));
            int minX = input.width, minY = input.height, maxX = 0, maxY = 0;
            for (int y = 0; y < input.height; y++) for (int x = 0; x < input.width; x++)
                if (input.GetPixel(x, y).a > .5f) { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
            int width = head ? 10 : 38;
            var output = new Texture2D(width, 14, TextureFormat.RGBA32, false);
            Color32[] palette = { new Color32(12, 19, 50, 255), new Color32(99, 111, 135, 255), new Color32(157, 174, 191, 255), new Color32(211, 225, 231, 255), new Color32(145, 65, 15, 255), new Color32(211, 114, 13, 255), new Color32(247, 172, 39, 255) };
            for (int y = 0; y < 14; y++) for (int x = 0; x < width; x++)
            {
                int column = x + (head ? 38 : 0);
                Color c = input.GetPixel(minX + (int)((column + .5f) * (maxX - minX + 1) / 48), minY + (int)((y + .5f) * (maxY - minY + 1) / 14));
                if (c.a < .5f) { output.SetPixel(x, y, Color.clear); continue; }
                float best = float.MaxValue; Color selected = palette[0];
                foreach (Color candidate in palette) { float distance = (new Vector3(c.r, c.g, c.b) - new Vector3(candidate.r, candidate.g, candidate.b)).sqrMagnitude; if (distance < best) { best = distance; selected = candidate; } }
                output.SetPixel(x, y, selected);
            }
            Directory.CreateDirectory(Folder); string path = Folder + (head ? "/HammerHead.png" : "/HammerHandle.png");
            output.Apply(); File.WriteAllBytes(path, output.EncodeToPNG()); Object.DestroyImmediate(input); Object.DestroyImmediate(output);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 24;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        public static Sprite Resize(Sprite source, int width, int height, bool tiled = false)
        {
            Directory.CreateDirectory(Folder);
            string path = Folder + "/" + source.name + "_" + width + "x" + height + (tiled ? "_tile" : "") + ".png";
            var input = new Texture2D(2, 2); input.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source.texture)));
            var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Rect rect = source.rect;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int sx = tiled ? x % (int)rect.width : Mathf.Min((int)rect.width - 1, (int)((x + .5f) * rect.width / width));
                int sy = tiled ? y % (int)rect.height : Mathf.Min((int)rect.height - 1, (int)((y + .5f) * rect.height / height));
                Color c = input.GetPixel((int)rect.x + sx, (int)rect.y + sy); c.a = c.a < .5f ? 0 : 1;
                output.SetPixel(x, y, c);
            }
            output.Apply(); File.WriteAllBytes(path, output.EncodeToPNG());
            Object.DestroyImmediate(input); Object.DestroyImmediate(output);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static void BakeScenery(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null || !(renderer.name == "PixelSkin" || renderer.name == "TrainingEdge" || renderer.name.StartsWith("UnclaimedLocker") || renderer.name.EndsWith("RecordMarker"))) continue;
                Vector3 scale = renderer.transform.lossyScale;
                Vector2 size = renderer.drawMode == SpriteDrawMode.Simple ? (Vector2)renderer.sprite.bounds.size : renderer.size;
                int width = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(size.x * scale.x) * 24));
                int height = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(size.y * scale.y) * 24));
                if (width > 2048 || height > 2048) throw new System.InvalidOperationException("Unexpected scenery size: " + renderer.name);
                renderer.sprite = Resize(renderer.sprite, width, height, renderer.drawMode == SpriteDrawMode.Tiled);
                renderer.drawMode = SpriteDrawMode.Simple;
                Vector3 local = renderer.transform.localScale;
                renderer.transform.localScale = new Vector3(local.x / Mathf.Abs(scale.x), local.y / Mathf.Abs(scale.y), local.z);
            }
        }
    }
}
