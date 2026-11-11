using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TwoInPot.Editor
{
    public static class PixelArtUpgrade
    {
        const string Root = "Assets/TwoInPot";
        const string Art = Root + "/Art/Pixel";
        static Material material;
        static Sprite[] tiles;
        static Sprite grassEdge;
        static Sprite hammerTool;

        [MenuItem("Two In Pot/Apply Pixel Art and Build")]
        public static void ApplyAndBuild()
        {
            Apply();
            FestivalUpgrade.BuildAndVerify();
            File.Copy("Docs/Previews/PotCharacters.png", "Docs/Previews/PixelCharacters.png", true);
            File.Copy("Docs/Previews/FestivalStage01.png", "Docs/Previews/PixelStage01.png", true);
        }

        public static void Apply()
        {
            AssetDatabase.Refresh();
            material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/PixelUnlit.mat");
            if (material == null) { material = new Material(Shader.Find("TwoInPot/PixelUnlit")); AssetDatabase.CreateAsset(material, Art + "/PixelUnlit.mat"); }
            var characters = ImportAtlas("ClayCharacters");
            tiles = ImportAtlas("WorkshopTiles");
            string path = Root + "/Prefabs/CoopPlayer.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = prefab.GetComponent<PotPresentation>();
                foreach (var mesh in prefab.GetComponentsInChildren<MeshRenderer>(true)) mesh.enabled = false;
                foreach (var sprite in prefab.GetComponentsInChildren<SpriteRenderer>(true)) sprite.enabled = false;
                visual.pixelArt = true;
                var playerCollider = prefab.GetComponent<BoxCollider2D>();
                playerCollider.size = new Vector2(.6f, 1.35f); playerCollider.offset = new Vector2(0, .075f);
                visual.climberFrames = new Sprite[8]; visual.engineerFrames = new Sprite[8];
                Array.Copy(characters, 0, visual.climberFrames, 0, 8); Array.Copy(characters, 8, visual.engineerFrames, 0, 8);
                visual.climberSprite = Character(visual.climberAppearance.transform, characters[0]);
                visual.engineerSprite = Character(visual.engineerAppearance.transform, characters[8]);
                // Keep serialized tool transforms and the exact collision/contact geometry.
                var square = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Square.png");
                Tool(visual.shaft, square, new Color(.57f, .31f, .13f), 7);
                Tool(visual.hammerHead, square, new Color(.75f, .85f, .9f), 8);
                foreach (var arm in visual.arms) Tool(arm, square, new Color(1, .46f, .06f), 6);
                foreach (var hand in visual.hands) Tool(hand, square, new Color(1, .63f, .1f), 9);
                if (tiles != null)
                {
                    visual.shaft.GetComponentInChildren<SpriteRenderer>(true).enabled = false;
                    visual.hammerHead.GetComponentInChildren<SpriteRenderer>(true).enabled = false;
                    var oldHammer = visual.toolPivot.Find("PixelHammer"); if (oldHammer != null) UnityEngine.Object.DestroyImmediate(oldHammer.gameObject);
                    var hammer = new GameObject("PixelHammer"); hammer.transform.SetParent(visual.toolPivot, false);
                    visual.pixelHammer = hammer.AddComponent<SpriteRenderer>(); visual.pixelHammer.sharedMaterial = material; visual.pixelHammer.sortingOrder = 8;
                    visual.pixelHammer.sprite = PixelDensityBaker.HammerPart(false);
                    visual.pixelHammer.drawMode = SpriteDrawMode.Sliced;
                    visual.pixelHammer.size = new Vector2(CoopPlayer.HammerReach, 14f / 24);
                    visual.pixelHammer.transform.localPosition = new Vector3(CoopPlayer.HammerReach / 2, 0, 0);
                    var oldHead = visual.toolPivot.Find("PixelHammerHead"); if (oldHead != null) UnityEngine.Object.DestroyImmediate(oldHead.gameObject);
                    var head = new GameObject("PixelHammerHead"); head.transform.SetParent(visual.toolPivot, false);
                    head.transform.localPosition = new Vector3(CoopPlayer.HammerReach, 0, 0);
                    visual.pixelHammerHead = head.AddComponent<SpriteRenderer>(); visual.pixelHammerHead.sprite = PixelDensityBaker.HammerPart(true); visual.pixelHammerHead.sortingOrder = 8;
                    var dustFrame = PixelDensityBaker.Resize(tiles[12], 14, 14);
                    visual.pixelDust = new SpriteRenderer[visual.dust.Length];
                    for (int i = 0; i < visual.dust.Length; i++)
                    {
                        Tool(visual.dust[i], dustFrame, Color.white, 3);
                        visual.pixelDust[i] = visual.dust[i].GetComponentInChildren<SpriteRenderer>(true); visual.dust[i].gameObject.SetActive(false);
                    }
                    var oldImpact = prefab.transform.Find("PixelImpact"); if (oldImpact != null) UnityEngine.Object.DestroyImmediate(oldImpact.gameObject);
                    var impact = new GameObject("PixelImpact"); impact.transform.SetParent(prefab.transform, false);
                    impact.transform.localScale = Vector3.one;
                    visual.pixelImpact = impact.AddComponent<SpriteRenderer>(); visual.pixelImpact.sprite = PixelDensityBaker.Resize(tiles[13], 22, 22); visual.pixelImpact.sharedMaterial = material; visual.pixelImpact.sortingOrder = 12; visual.pixelImpact.enabled = false;
                }
                visual.climberAppearance.SetActive(true); visual.engineerAppearance.SetActive(false);
                BindPixelTextures(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene = EditorSceneManager.OpenScene(Root + "/Scenes/Prototype.unity");
            if (Camera.main.GetComponent<PixelPerformance>() == null) Camera.main.gameObject.AddComponent<PixelPerformance>();
            if (Camera.main.GetComponent<PixelViewport>() == null) Camera.main.gameObject.AddComponent<PixelViewport>();
            Camera.main.allowHDR = false; Camera.main.allowMSAA = false;
            var cameraData = Camera.main.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null) cameraData.renderPostProcessing = false;
            Camera.main.orthographicSize = 7.5f;
            if (tiles != null)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name.StartsWith("Stage") && root.name.EndsWith("World") || root.name.EndsWith("Boundary")) SkinWorld(root);
                    if (root.name == "FestivalBackdrop") root.SetActive(false);
                }
                Camera.main.backgroundColor = new Color(.18f, .24f, .39f);
            }
            BuildBackground();
            BuildStory();
            FestivalAudioBuilder.Apply();
            foreach (var root in scene.GetRootGameObjects()) { PixelDensityBaker.BakeScenery(root); BindPixelTextures(root); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            foreach (var frame in characters) if (frame == null || frame.texture.filterMode != FilterMode.Point) throw new InvalidOperationException("Invalid pixel frame.");
            Debug.Log("[POT_PIXEL] VALIDATED 16 point-filtered character frames; 3D player renderers disabled.");
        }

        static void BuildBackground()
        {
            const string path = Art + "/Source/WorkshopBackground.png";
            var raw = new Texture2D(2, 2); raw.LoadImage(File.ReadAllBytes(path));
            var image = new Texture2D(640, 360, TextureFormat.RGB24, false);
            for (int y = 0; y < 360; y++) for (int x = 0; x < 640; x++) image.SetPixel(x, y, raw.GetPixel((int)((x + .5f) * raw.width / 640), (int)((y + .5f) * raw.height / 360)));
            image.Apply(); string output = Art + "/WorkshopBackground.png"; File.WriteAllBytes(output, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(raw); UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(output); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 12; importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = 24;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None; importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(output);
            var old = GameObject.Find("PixelBackdrop"); if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var background = new GameObject("PixelBackdrop"); background.transform.position = new Vector3(0, .3f, 5);
            var renderer = background.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.sortingOrder = -100;
        }

        static void BindPixelTextures(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null) continue;
                var texture = renderer.sprite.texture;
                string path = Art + "/Pixel_" + texture.name + ".mat";
                var bound = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (bound == null) { bound = new Material(Shader.Find("TwoInPot/PixelUnlit")); AssetDatabase.CreateAsset(bound, path); }
                bound.SetTexture("_PixelTex", texture); EditorUtility.SetDirty(bound); renderer.sharedMaterial = bound;
            }
        }

        static void BuildStory()
        {
            var decals = ImportAtlas("StoryDecals");
            var state = UnityEngine.Object.FindAnyObjectByType<StageState>();
            var story = state.GetComponent<FacilityStory>(); if (story == null) story = state.gameObject.AddComponent<FacilityStory>();
            story.backdrop = GameObject.Find("PixelBackdrop").GetComponent<SpriteRenderer>();
            var old = GameObject.Find("FacilityRecords"); if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject("FacilityRecords").transform;
            story.memoryPads = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var pad = new GameObject(i == 0 ? "OrangeRecordMarker" : "BlueRecordMarker"); pad.transform.SetParent(root, false);
                pad.transform.localScale = new Vector3(.42f, .1f, 1);
                var sprite = pad.AddComponent<SpriteRenderer>(); sprite.sharedMaterial = material; sprite.sprite = tiles[14]; sprite.sortingOrder = 2; story.memoryPads[i] = sprite;
            }
            story.traces = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var item = new GameObject("UnclaimedLocker" + i); item.transform.SetParent(root, false);
                item.transform.position = new Vector3(-9 + i * 6, 1.0f + i % 2 * .4f, 3); item.transform.localScale = Vector3.one * .8f;
                var sprite = item.AddComponent<SpriteRenderer>(); sprite.sharedMaterial = material; sprite.sprite = decals[i]; sprite.color = new Color(.7f, .72f, .8f, .7f); sprite.sortingOrder = -50;
                story.traces[i] = sprite; sprite.enabled = false;
            }
            EditorUtility.SetDirty(story);
        }


        static SpriteRenderer Character(Transform parent, Sprite frame)
        {
            var old = parent.Find("PixelCharacter"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var item = new GameObject("PixelCharacter"); item.transform.SetParent(parent, false);
            item.transform.localPosition = new Vector3(0, -.6f, -.3f);
            item.transform.localScale = Vector3.one * .75f;
            var sprite = item.AddComponent<SpriteRenderer>(); sprite.sprite = frame; sprite.sharedMaterial = material; sprite.sortingOrder = 5; return sprite;
        }
        static void Tool(Transform item, Sprite frame, Color color, int order)
        {
            var child = item.Find("PixelSurface");
            if (child == null) { child = new GameObject("PixelSurface").transform; child.SetParent(item, false); }
            var sprite = child.GetComponent<SpriteRenderer>(); if (sprite == null) sprite = child.gameObject.AddComponent<SpriteRenderer>();
            sprite.enabled = true; sprite.sprite = frame; sprite.sharedMaterial = material; sprite.color = color; sprite.sortingOrder = order;
        }

        static Sprite[] ImportAtlas(string name)
        {
            string source = Art + "/Source/" + name + ".png";
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false); raw.LoadImage(File.ReadAllBytes(source));
            int grid = name == "StoryDecals" ? 2 : 4;
            int size = grid * 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Color32 c = raw.GetPixel((int)((x + .5f) * raw.width / size), (int)((y + .5f) * raw.height / size));
                c.a = c.a < 150 ? (byte)0 : (byte)255; pixels[y * size + x] = c;
            }
            texture.SetPixels32(pixels); texture.Apply();
            string output = Art + "/" + name + ".png"; File.WriteAllBytes(output, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(raw); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(output);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple; importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None; importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var previousRects = provider.GetSpriteRects();
            var rects = new List<SpriteRect>();
            var result = new Sprite[grid * grid];
            // Generated character bands are slightly taller than a strict quarter-grid.
            int[] cuts = { 0, 64, 128, 192, 256 };
            if (name == "ClayCharacters")
                for (int line = 1; line < 4; line++)
                {
                    int best = int.MaxValue;
                    for (int y = line * 64 - 10; y <= line * 64 + 10; y++)
                    {
                        int occupied = 0; for (int x = 0; x < size; x++) if (pixels[y * size + x].a != 0) occupied++;
                        int score = occupied * 100 + Mathf.Abs(y - line * 64);
                        if (score < best) { best = score; cuts[line] = y; }
                    }
                }
            for (int i = 0; i < result.Length; i++)
            {
                int col = i % grid, row = i / grid;
                Rect rect = new Rect(col * 64, (grid - 1 - row) * 64, 64, 64);
                Vector2 pivot = new Vector2(.5f, .5f);
                if (name == "ClayCharacters" || name == "WorkshopTiles" && (i <= 4 || i == 8 || i == 14))
                {
                    int low = name == "ClayCharacters" ? cuts[3 - row] : (3 - row) * 64;
                    int high = name == "ClayCharacters" ? cuts[4 - row] : (4 - row) * 64;
                    int minX = col * 64 + 63, maxX = col * 64, minY = high - 1, maxY = low;
                    for (int y = low; y < high; y++) for (int x = col * 64; x < (col + 1) * 64; x++)
                        if (pixels[y * size + x].a > 0) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                    if (maxX < minX || maxY < minY) throw new InvalidOperationException("Empty animation frame " + i);
                    rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
                    if (name == "ClayCharacters") pivot = new Vector2((col * 64 + 32f - minX) / rect.width, 0);
                }
                rects.Add(new SpriteRect { name = name + i, rect = rect, pivot = pivot, alignment = SpriteAlignment.Custom, spriteID = ExistingId(previousRects, name + i) });
            }
            if (name == "WorkshopTiles")
            {
                Rect turf = rects[0].rect;
                rects.Add(new SpriteRect { name = "TrainingEdge", rect = new Rect(turf.x, turf.yMax - 18, turf.width, 18), pivot = new Vector2(.5f, .5f), alignment = SpriteAlignment.Custom, spriteID = ExistingId(previousRects, "TrainingEdge") });
                rects.Add(new SpriteRect { name = "HammerTool", rect = rects[11].rect, pivot = new Vector2(.06f, .5f), alignment = SpriteAlignment.Custom, spriteID = ExistingId(previousRects, "HammerTool") });
            }
            provider.SetSpriteRects(rects.ToArray());
            var pairs = new List<SpriteNameFileIdPair>(); foreach (var rect in rects) pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
            provider.Apply(); importer.SaveAndReimport();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(output))
            {
                if (!(asset is Sprite sprite)) continue;
                for (int i = 0; i < result.Length; i++) if (sprite.name == name + i) result[i] = sprite;
                if (sprite.name == "TrainingEdge") grassEdge = sprite;
                if (sprite.name == "HammerTool") hammerTool = sprite;
            }
            return result;
        }
        static GUID ExistingId(SpriteRect[] previous, string name)
        {
            foreach (var rect in previous) if (rect.name == name) return rect.spriteID;
            return GUID.Generate();
        }
        static void SkinWorld(GameObject root)
        {
            var sources = root.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var source in sources)
            {
                if (source == null || source.name == "PixelSkin" || source.name == "GrassEdge" || source.name == "TrainingEdge" || source.sprite == null) continue;
                var old = source.transform.Find("FestivalArt"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                old = source.transform.Find("PixelSkin"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                string n = source.name.ToLowerInvariant();
                bool ground = n.Contains("ground") || n.Contains("floor");
                bool wind = n.Contains("wind");
                int tile = ground ? 1 : n.Contains("console") || n.Contains("switch") ? 5 : n.Contains("flag") ? 7 : n.Contains("gate") ? 6 : n.Contains("crate") ? 8 : n.Contains("rock") ? 3 : n.Contains("rail") ? 9 : wind ? 13 : 4;
                Vector3 scale = source.transform.lossyScale;
                var skin = new GameObject("PixelSkin"); skin.transform.SetParent(source.transform, false);
                skin.transform.localScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(scale.x)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1);
                var renderer = skin.AddComponent<SpriteRenderer>(); renderer.sharedMaterial = material; renderer.sprite = tiles[tile]; renderer.sortingOrder = source.sortingOrder;
                renderer.drawMode = SpriteDrawMode.Tiled; renderer.size = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                if (tile == 5 || tile == 6 || tile == 7 || tile == 8)
                {
                    renderer.drawMode = SpriteDrawMode.Sliced;
                    renderer.size = new Vector2(Mathf.Max(tile == 6 ? .7f : .65f, Mathf.Abs(scale.x)), Mathf.Max(tile == 5 ? .65f : .7f, Mathf.Abs(scale.y)));
                }
                if (ground)
                {
                    var cap = new GameObject("GrassEdge"); cap.transform.SetParent(skin.transform, false); cap.transform.localPosition = new Vector3(0, Mathf.Abs(scale.y) * .5f - .18f, -.01f);
                    cap.name = "TrainingEdge";
                    var edge = cap.AddComponent<SpriteRenderer>(); edge.sprite = grassEdge; edge.sharedMaterial = material; edge.sortingOrder = source.sortingOrder + 1;
                    edge.drawMode = SpriteDrawMode.Tiled; edge.size = new Vector2(Mathf.Abs(scale.x), .36f);
                }
                if (wind) renderer.color = new Color(.6f, .9f, 1, .25f);
                if (tile == 5 || tile == 7)
                { var accent = skin.AddComponent<WorldAccent>(); accent.motion = tile == 5 ? WorldAccent.Motion.Console : WorldAccent.Motion.Flag; }
                int stageNumber = 1;
                if (root.name.StartsWith("Stage"))
                {
                    string digits = ""; foreach (char c in root.name) if (char.IsDigit(c)) digits += c;
                    if (int.TryParse(digits, out int parsed)) stageNumber = parsed;
                }
                Color shade = stageNumber <= 3 ? Color.white : stageNumber <= 6 ? new Color(.8f, .85f, 1) : stageNumber <= 9 ? new Color(1, .8f, .65f) : new Color(.67f, .7f, .85f);
                if (!wind && tile != 5 && tile != 7) renderer.color = shade;
                source.enabled = false;
            }
        }
    }
}

