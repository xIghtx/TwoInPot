using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwoInPot.Editor
{
    public static class FestivalUpgrade
    {
        const string Root = "Assets/TwoInPot";
        const string Models = Root + "/Art/Models";
        static Mesh block, sphere, pot, handle;
        static Material ceramic, cream, dark, stone, cyan;
        static Sprite square;
        static Material spriteMaterial;

        [MenuItem("Two In Pot/Upgrade Festival Edition")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the current scene first.");
            Directory.CreateDirectory("Docs/Backups/BeforeFestivalUpgrade");
            Backup(Root + "/Scenes/Prototype.unity", "Prototype.unity");
            Backup(Root + "/Prefabs/CoopPlayer.prefab", "CoopPlayer.prefab");
            Directory.CreateDirectory(Models); AssetDatabase.Refresh();
            BuildModels();
            UpgradePlayer();
            var scene = EditorSceneManager.OpenScene(Root + "/Scenes/Prototype.unity");
            var state = UnityEngine.Object.FindAnyObjectByType<StageState>();
            if (state == null || state.additionalStages == null || state.additionalStages.Length < 6) throw new InvalidOperationException("Existing stages 1-8 are required.");
            square = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Square.png");
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Unlit.mat");
            Array.Resize(ref state.additionalStages, 10); Array.Resize(ref state.additionalStageRoots, 10);
            Array.Resize(ref state.logicTargets, 10); Array.Resize(ref state.exits, 10);
            Array.Resize(ref state.puzzleGates, 10); Array.Resize(ref state.puzzleMechanisms, 10);
            string[] names = { "SKY LIFT", "TICK TOCK", "BALANCE BUDDIES", "TOGETHER AGAIN" };
            for (int number = 9; number <= 12; number++)
            {
                int index = number - 3;
                if (state.additionalStageRoots[index] != null) UnityEngine.Object.DestroyImmediate(state.additionalStageRoots[index]);
                string path = Root + "/Data/Stage" + number.ToString("00") + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
                if (definition == null) { definition = ScriptableObject.CreateInstance<StageDefinition>(); AssetDatabase.CreateAsset(definition, path); }
                definition.displayName = number.ToString("00") + " / " + names[number - 9];
                definition.physicalSpawn = new Vector2(-11.2f, -2.3f); definition.logicSpawn = new Vector2(-8.2f, -2.3f);
                definition.movementMode = MovementMode.Platform;
                state.additionalStages[index] = definition; EditorUtility.SetDirty(definition);
                var world = new GameObject("Stage" + number.ToString("00") + "World");
                state.additionalStageRoots[index] = world;
                BuildNewWorld(world.transform, number, out state.logicTargets[index], out state.exits[index], out state.puzzleGates[index], out state.puzzleMechanisms[index]);
                world.SetActive(false);
            }
            state.additionalStages[5].displayName = "08 / PARTNER CIRCUIT"; EditorUtility.SetDirty(state.additionalStages[5]);
            PolishExistingLayouts(state);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Grid") { root.SetActive(false); continue; }
                if (root.name.StartsWith("Stage") && root.name.EndsWith("World"))
                {
                    root.SetActive(root == state.stageOneRoot);
                    DecorateWorld(root);
                }
                else if (root.name.EndsWith("Boundary")) DecorateWorld(root);
            }
            BuildBackdrop(scene);
            EditorUtility.SetDirty(state); EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[POT_FESTIVAL] UPGRADE COMPLETE: 12 stages, ceramic models, animated scenery and player presentation.");
        }

        static void PolishExistingLayouts(StageState state)
        {
            for (int number = 3; number <= 7; number++)
            {
                Transform root = state.additionalStageRoots[number - 3].transform;
                if (number == 3 || number == 4 || number == 5 || number == 7)
                {
                    float edge = number == 4 ? 3.5f : 4;
                    var left = root.Find("LeftGround"); var right = root.Find("RightGround");
                    left.localPosition = new Vector3((-14 - edge) * .5f, -4, 0); left.localScale = new Vector3(14 - edge, 1.2f, 1);
                    right.localPosition = new Vector3((14 + edge) * .5f, -4, 0); right.localScale = new Vector3(14 - edge, 1.2f, 1);
                }
                if (number == 4)
                {
                    var floor = root.Find("WindFloor"); if (floor != null) floor.gameObject.SetActive(false);
                }
                if (number == 5) state.puzzleMechanisms[number - 3].localPosition = new Vector3(0, -2.35f, 0);
                if (number == 7)
                {
                    var pivot = state.puzzleMechanisms[number - 3]; pivot.localPosition = new Vector3(0, -2.2f, 0);
                    var bridge = pivot.Find("Bridge"); bridge.localPosition = Vector3.zero; bridge.localScale = new Vector3(8.5f, .35f, 1);
                }
            }
        }

        static void Backup(string source, string name)
        {
            string destination = "Docs/Backups/BeforeFestivalUpgrade/" + name;
            if (!File.Exists(destination)) File.Copy(source, destination);
        }

        static void BuildNewWorld(Transform root, int number, out Transform target, out Transform exit, out GameObject gate, out Transform mechanism)
        {
            target = Point(root, "LogicTarget", new Vector2(-5.5f, -2.7f));
            exit = Point(root, "Exit", new Vector2(10.8f, number == 9 ? 1.7f : -2.7f));
            gate = null; mechanism = null;
            Box(root, "LeftGround", new Vector2(-8, -4), new Vector2(12, 1.2f), new Color(.22f, .31f, .43f), true);
            Box(root, "LogicConsole", target.position, new Vector2(1.2f, .22f), new Color(.32f, .83f, .85f));
            Box(root, "ExitFlag", (Vector2)exit.position + Vector2.up * 1.2f, new Vector2(.9f, 1.7f), new Color(.4f, 1f, .7f));
            if (number == 9)
            {
                Box(root, "UpperTerrace", new Vector2(8.5f, .5f), new Vector2(11, 1.2f), new Color(.22f, .31f, .43f), true);
                mechanism = Point(root, "SkyLift", new Vector2(0, -3.1f));
                Box(mechanism, "LiftPlatform", Vector2.zero, new Vector2(4.8f, .4f), new Color(.95f, .62f, .3f), true).transform.localPosition = Vector3.zero;
                Box(root, "LiftRail", new Vector2(0, -1.1f), new Vector2(.12f, 4.7f), new Color(.2f, .42f, .5f));
            }
            else if (number == 10)
            {
                Box(root, "RightGround", new Vector2(8, -4), new Vector2(12, 1.2f), new Color(.22f, .31f, .43f), true);
                Box(root, "BridgeFloor", new Vector2(0, -4), new Vector2(4, 1.2f), new Color(.22f, .31f, .43f), true);
                gate = Box(root, "TimedGate", new Vector2(5.5f, -1.3f), new Vector2(.6f, 5.4f), new Color(.95f, .52f, .25f), true);
                Box(root, "JumpStep", new Vector2(.5f, -2.8f), new Vector2(2, .45f), new Color(.38f, .47f, .62f), true);
            }
            else if (number == 11)
            {
                root.Find("LeftGround").localPosition = new Vector3(-9, -4, 0);
                root.Find("LeftGround").localScale = new Vector3(10, 1.2f, 1);
                Box(root, "RightGround", new Vector2(9, -4), new Vector2(10, 1.2f), new Color(.22f, .31f, .43f), true);
                mechanism = Point(root, "BalanceBridge", new Vector2(0, -2.2f));
                Box(mechanism, "Bridge", Vector2.zero, new Vector2(8.5f, .35f), new Color(.95f, .62f, .30f), true).transform.localPosition = Vector3.zero;
                Box(root, "BridgePivot", new Vector2(0, -2.85f), new Vector2(.6f, 1.1f), new Color(.29f, .55f, .59f));
                var weight = Box(root, "Counterweight", new Vector2(-3.5f, 1), new Vector2(.7f, .8f), new Color(.64f, .48f, .76f));
                weight.AddComponent<WorldAccent>().motion = WorldAccent.Motion.Counterweight;
            }
            else
            {
                Box(root, "RightGround", new Vector2(8, -4), new Vector2(12, 1.2f), new Color(.22f, .31f, .43f), true);
                Box(root, "BridgeFloor", new Vector2(0, -4), new Vector2(4, 1.2f), new Color(.22f, .31f, .43f), true);
                gate = Box(root, "RelayGateOne", new Vector2(-1, -1.3f), new Vector2(.55f, 5.4f), new Color(.95f, .52f, .25f), true);
                Box(root, "RelayConsole", new Vector2(3, -2.7f), new Vector2(1.2f, .22f), new Color(.32f, .83f, .85f));
                mechanism = Box(root, "RelayGateTwo", new Vector2(7, -1.3f), new Vector2(.55f, 5.4f), new Color(.65f, .48f, .78f), true).transform;
            }
        }

        static Transform Point(Transform parent, string name, Vector2 position)
        {
            var point = new GameObject(name).transform; point.SetParent(parent, false); point.localPosition = position; return point;
        }
        static GameObject Box(Transform parent, string name, Vector2 position, Vector2 size, Color color, bool solid = false)
        {
            var item = new GameObject(name); item.transform.SetParent(parent, false); item.transform.localPosition = position;
            item.transform.localScale = new Vector3(size.x, size.y, 1);
            var sprite = item.AddComponent<SpriteRenderer>(); sprite.sprite = square; sprite.sharedMaterial = spriteMaterial; sprite.color = color;
            if (solid) { item.layer = 6; item.AddComponent<BoxCollider2D>(); }
            return item;
        }

        static void UpgradePlayer()
        {
            string path = Root + "/Prefabs/CoopPlayer.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old = prefab.GetComponent<PotPresentation>();
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
                foreach (string childName in new[] { "PotModel", "LandingDust" })
                { var child = prefab.transform.Find(childName); if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject); }
                foreach (var sprite in prefab.GetComponentsInChildren<SpriteRenderer>(true)) sprite.enabled = false;
                var model = new GameObject("PotModel").transform; model.SetParent(prefab.transform, false);
                model.localRotation = Quaternion.Euler(-8, 0, 0);
                var tinted = new List<Renderer>();
                tinted.Add(Part(model, "CeramicBody", pot, ceramic, Vector3.zero, Vector3.one, 4).GetComponent<Renderer>());
                for (int i = 0; i < 2; i++) tinted.Add(Part(model, "Handle", handle, ceramic, new Vector3(i == 0 ? -.43f : .43f, .08f, 0), new Vector3(.8f, 1, 1), 4).GetComponent<Renderer>());
                var eyeRoots = new Transform[2]; var feet = new Transform[2];
                for (int i = 0; i < 2; i++)
                {
                    float x = i == 0 ? -.15f : .15f;
                    eyeRoots[i] = new GameObject("BlinkEye").transform; eyeRoots[i].SetParent(model, false); eyeRoots[i].localPosition = new Vector3(x, .04f, -.405f);
                    Part(eyeRoots[i], "EyeWhite", sphere, cream, Vector3.zero, new Vector3(.115f, .145f, .045f), 6);
                    Part(eyeRoots[i], "Pupil", sphere, dark, new Vector3(.012f, -.005f, -.041f), new Vector3(.048f, .075f, .025f), 7);
                    feet[i] = Part(model, "Foot", sphere, ceramic, new Vector3(x, -.48f, -.05f), new Vector3(.14f, .10f, .20f), 4).transform;
                    tinted.Add(feet[i].GetComponent<Renderer>());
                }
                Part(model, "Smile", handle, dark, new Vector3(0, -.18f, -.41f), new Vector3(.28f, .18f, .18f), 6);
                Part(model, "RimHighlight", handle, cream, new Vector3(0, .43f, 0), new Vector3(2.1f, .45f, 2.1f), 5).transform.localRotation = Quaternion.Euler(90, 0, 0);
                var dustRoot = new GameObject("LandingDust").transform; dustRoot.SetParent(prefab.transform, false);
                var puffs = new Transform[6];
                for (int i = 0; i < puffs.Length; i++) puffs[i] = Part(dustRoot, "Dust", sphere, cream, new Vector3(0, -.52f, -.6f), Vector3.one * .12f, 3).transform;
                var presentation = prefab.AddComponent<PotPresentation>();
                presentation.model = model; presentation.eyes = eyeRoots; presentation.feet = feet; presentation.ceramic = tinted.ToArray(); presentation.dust = puffs;
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        static void DecorateWorld(GameObject world)
        {
            foreach (var sprite in world.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sprite.sprite == null) continue;
                Transform parent = sprite.transform;
                var previous = parent.Find("FestivalArt");
                if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                Vector3 size = parent.localScale; size.z = 1; parent.localScale = size;
                var art = new GameObject("FestivalArt").transform; art.SetParent(parent, false);
                Color color = sprite.color; color.a = 1;
                if (parent.name == "Wind")
                {
                    var streaks = new Transform[6];
                    for (int i = 0; i < streaks.Length; i++) streaks[i] = Part(art, "WindStreak", block, cyan, new Vector3(0, -.32f + i * .13f, -.25f), new Vector3(.20f, .018f, .08f), 1).transform;
                    var accent = art.gameObject.AddComponent<WorldAccent>(); accent.motion = WorldAccent.Motion.Wind; accent.parts = streaks;
                }
                else
                {
                    Material mat = WorldMaterial(color);
                    Part(art, "BeveledSurface", block, mat, Vector3.zero, new Vector3(1, 1, .6f), sprite.sortingOrder);
                    bool console = parent.name.Contains("Console") || parent.name.Contains("Switch");
                    bool flag = parent.name.Contains("Flag") && !parent.name.Contains("Pole");
                    if (console || flag)
                    {
                        var accent = art.gameObject.AddComponent<WorldAccent>(); accent.motion = console ? WorldAccent.Motion.Console : WorldAccent.Motion.Flag;
                        if (console) Part(art, "ConsoleLight", block, cream, new Vector3(0, .03f, -.18f), new Vector3(.38f, .3f, .1f), sprite.sortingOrder + 1);
                    }
                }
                sprite.enabled = false;
            }
        }

        static Material WorldMaterial(Color color)
        {
            string key = ColorUtility.ToHtmlStringRGB(color);
            string path = Models + "/Surface_" + key + ".mat";
            return SaveMaterial(path, color);
        }
        static GameObject Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, int order)
        {
            var part = new GameObject(name); part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return part;
        }

        static void BuildBackdrop(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var item in scene.GetRootGameObjects()) if (item.name == "FestivalBackdrop") UnityEngine.Object.DestroyImmediate(item);
            var root = new GameObject("FestivalBackdrop").transform;
            for (int i = 0; i < 11; i++)
            {
                float height = 2.4f + Mathf.Sin(i * 2.4f) * 1.3f;
                Part(root, "DistantWorkshop", block, stone, new Vector3(-14 + i * 2.8f, -2 + height * .5f, 2.5f), new Vector3(2.1f, height, .4f), -10);
                Part(root, "Window", block, cyan, new Vector3(-14 + i * 2.8f, -1 + height * .4f, 2.1f), new Vector3(.35f, .5f, .15f), -9);
            }
            Part(root, "Moon", sphere, cream, new Vector3(10, 6, 3), Vector3.one * .8f, -12);
        }

        static void BuildModels()
        {
            block = SaveMesh("BeveledBlock", BeveledBlock());
            sphere = SaveMesh("RoundDetail", Lathe(new[] { new Vector2(0, -1), new Vector2(.5f, -.866f), new Vector2(.866f, -.5f), new Vector2(1, 0), new Vector2(.866f, .5f), new Vector2(.5f, .866f), new Vector2(0, 1) }, 16));
            pot = SaveMesh("PotBody", Lathe(new[] { new Vector2(0, -.48f), new Vector2(.25f, -.48f), new Vector2(.38f, -.34f), new Vector2(.43f, -.08f), new Vector2(.39f, .20f), new Vector2(.30f, .35f), new Vector2(.34f, .42f), new Vector2(.32f, .49f), new Vector2(.25f, .49f), new Vector2(.23f, .30f), new Vector2(0, .29f) }, 24));
            handle = SaveMesh("RoundHandle", Torus());
            ceramic = SaveMaterial(Models + "/Ceramic.mat", new Color(1, .58f, .27f));
            cream = SaveMaterial(Models + "/Cream.mat", new Color(1, .9f, .68f));
            dark = SaveMaterial(Models + "/Ink.mat", new Color(.045f, .065f, .095f));
            stone = SaveMaterial(Models + "/Backdrop.mat", new Color(.075f, .14f, .2f));
            cyan = SaveMaterial(Models + "/Mint.mat", new Color(.25f, .72f, .72f));
        }
        static Material SaveMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("TwoInPot/Ceramic");
            if (shader == null) throw new InvalidOperationException("Ceramic shader not imported.");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.SetColor("_BaseColor", color); EditorUtility.SetDirty(material); return material;
        }
        static Mesh SaveMesh(string name, Mesh mesh)
        {
            mesh.name = name; string path = Models + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
        }
        static Mesh Lathe(Vector2[] profile, int segments)
        {
            var vertices = new Vector3[profile.Length * (segments + 1)]; var triangles = new List<int>();
            for (int y = 0; y < profile.Length; y++)
                for (int x = 0; x <= segments; x++)
                {
                    float angle = x * Mathf.PI * 2 / segments; int index = y * (segments + 1) + x;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * profile[y].x, profile[y].y, Mathf.Sin(angle) * profile[y].x);
                    if (y == profile.Length - 1 || x == segments) continue;
                    int next = index + segments + 1;
                    triangles.AddRange(new[] { index, next, index + 1, index + 1, next, next + 1 });
                }
            var mesh = new Mesh { vertices = vertices, triangles = triangles.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh Torus()
        {
            const int segments = 20, rings = 8;
            var vertices = new Vector3[(segments + 1) * (rings + 1)]; var triangles = new List<int>();
            for (int i = 0; i <= segments; i++) for (int j = 0; j <= rings; j++)
            {
                float a = i * Mathf.PI * 2 / segments, b = j * Mathf.PI * 2 / rings;
                float r = .13f + .045f * Mathf.Cos(b); int index = i * (rings + 1) + j;
                vertices[index] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, Mathf.Sin(b) * .045f);
                if (i == segments || j == rings) continue;
                int next = index + rings + 1; triangles.AddRange(new[] { index, next, index + 1, index + 1, next, next + 1 });
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh BeveledBlock()
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            Vector3[] inner = { new Vector3(-.44f,-.44f,-.23f), new Vector3(.44f,-.44f,-.23f), new Vector3(.44f,.44f,-.23f), new Vector3(-.44f,.44f,-.23f) };
            Vector3[] outer = { new Vector3(-.5f,-.5f,-.12f), new Vector3(.5f,-.5f,-.12f), new Vector3(.5f,.5f,-.12f), new Vector3(-.5f,.5f,-.12f) };
            Quad(inner[3], inner[2], inner[1], inner[0]);
            for (int i = 0; i < 4; i++)
            {
                int n = (i + 1) % 4;
                Quad(inner[i], inner[n], outer[n], outer[i]);
                Quad(outer[i], outer[n], outer[n] + Vector3.forward * .4f, outer[i] + Vector3.forward * .4f);
            }
            var mesh = new Mesh { vertices = vertices.ToArray(), triangles = triangles.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count; vertices.AddRange(new[] { a, b, c, d });
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }

        [MenuItem("Two In Pot/Validate Festival Edition")]
        public static void ValidateFestival()
        {
            var state = UnityEngine.Object.FindAnyObjectByType<StageState>();
            if (state == null || state.TotalStages != 12) throw new InvalidOperationException("Expected 12 stages.");
            for (int i = 0; i < 10; i++)
                if (state.additionalStages[i] == null || state.additionalStageRoots[i] == null || state.logicTargets[i] == null || state.exits[i] == null) throw new InvalidOperationException("Missing stage references: " + (i + 3));
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CoopPlayer.prefab");
            var visual = player.GetComponent<PotPresentation>();
            if (visual == null || visual.model == null || visual.ceramic.Length < 3 || visual.eyes.Length != 2) throw new InvalidOperationException("Player model references incomplete.");
            if (AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Art/Ceramic.shader") == null) throw new InvalidOperationException("Shader missing.");
            Debug.Log("[POT_FESTIVAL] VALIDATION PASSED: 12 stage definitions and roots, controls, goals and player model references.");
        }

        public static void CapturePreview()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CoopPlayer.prefab");
            var preview = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var partner = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            preview.transform.position = new Vector3(-6, -2.78f, 0);
            partner.transform.position = new Vector3(-11.5f, -2.78f, 0);
            foreach (var puff in preview.GetComponent<PotPresentation>().dust) puff.gameObject.SetActive(false);
            foreach (var puff in partner.GetComponent<PotPresentation>().dust) puff.gameObject.SetActive(false);
            var tint = new MaterialPropertyBlock(); tint.SetColor("_BaseColor", new Color(.25f, .82f, .82f));
            foreach (var renderer in partner.GetComponent<PotPresentation>().ceramic) renderer.SetPropertyBlock(tint);
            var target = new RenderTexture(1600, 900, 24); var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            Vector3 cameraPosition = camera.transform.position; float cameraSize = camera.orthographicSize;
            var state = UnityEngine.Object.FindAnyObjectByType<StageState>();
            var backdrop = GameObject.Find("FestivalBackdrop");
            bool firstActive = state.stageOneRoot.activeSelf;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                Directory.CreateDirectory("Docs/Previews"); File.WriteAllBytes("Docs/Previews/FestivalStage01.png", image.EncodeToPNG());
                state.stageOneRoot.SetActive(false); if (backdrop != null) backdrop.SetActive(false);
                preview.transform.position = new Vector3(-.85f, 0, 0); partner.transform.position = new Vector3(.85f, 0, 0);
                camera.transform.position = new Vector3(0, .05f, -10); camera.orthographicSize = 1.4f;
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                File.WriteAllBytes("Docs/Previews/PotCharacters.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                camera.transform.position = cameraPosition; camera.orthographicSize = cameraSize;
                state.stageOneRoot.SetActive(firstActive); if (backdrop != null) backdrop.SetActive(true);
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(preview); UnityEngine.Object.DestroyImmediate(partner);
            }
        }

        public static void FinishAndVerify()
        {
            Upgrade();
            BuildAndVerify();
        }

        public static void BuildAndVerify()
        {
            if (UnityEngine.Object.FindAnyObjectByType<StageState>() == null)
                EditorSceneManager.OpenScene(Root + "/Scenes/Prototype.unity");
            ValidateFestival();
            CapturePreview();
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.productName = "Two In Pot";
            Directory.CreateDirectory("Builds/Windows");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Root + "/Scenes/Prototype.unity" },
                locationPathName = "Builds/Windows/TwoInPot.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Festival build failed: " + result.summary.result);
            Debug.Log("[POT_FESTIVAL] WINDOWS BUILD PASSED: " + result.summary.totalSize + " bytes.");
        }
    }
}
