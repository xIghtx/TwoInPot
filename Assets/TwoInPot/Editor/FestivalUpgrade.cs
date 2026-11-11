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
        static Material skin, hair, clay, wood, metal, blue;
        static Sprite square;
        static Material spriteMaterial;

        [MenuItem("Two In Pot/Upgrade Festival Edition")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the current scene first.");
            Directory.CreateDirectory("Docs/Backups/BeforeFestivalUpgrade");
            Directory.CreateDirectory("Docs/Backups/BeforeClimberUpgrade");
            foreach (string source in new[] { "/Scenes/Prototype.unity", "/Prefabs/CoopPlayer.prefab" })
            {
                string destination = "Docs/Backups/BeforeClimberUpgrade/" + Path.GetFileName(source);
                if (!File.Exists(destination)) File.Copy(Root + source, destination);
            }
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
            AddClimbingTerraces(state);
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
            Camera.main.backgroundColor = new Color(.61f, .77f, .79f);
            EditorUtility.SetDirty(state); EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[POT_FESTIVAL] UPGRADE COMPLETE: 12 stages, human climber and engineer, keyboard hammer, friendly workshop art.");
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

        static void AddClimbingTerraces(StageState state)
        {
            foreach (int number in new[] { 3, 5, 7, 8, 11, 12 })
            {
                int index = number - 3; Transform root = state.additionalStageRoots[index].transform;
                var previous = root.Find("ClimbingTerraces"); if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var terraces = new GameObject("ClimbingTerraces").transform; terraces.SetParent(root, false);
                Box(terraces, "LowRock", new Vector2(6, -2.9f), new Vector2(2.1f, .7f), new Color(.56f, .66f, .60f), true);
                Box(terraces, "MiddleRock", new Vector2(8, -2.0f), new Vector2(2.2f, .7f), new Color(.61f, .70f, .64f), true);
                Box(terraces, "SummitBoard", new Vector2(10.7f, -.95f), new Vector2(3.4f, .55f), new Color(.75f, .57f, .35f), true);
                state.exits[index].localPosition = new Vector3(10.8f, -.1f, 0);
                var flag = root.Find("ExitFlag"); if (flag != null) flag.localPosition = new Vector3(10.8f, 1.1f, 0);
            }
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
                foreach (string childName in new[] { "PotModel", "LandingDust", "HammerRig" })
                { var child = prefab.transform.Find(childName); if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject); }
                foreach (var sprite in prefab.GetComponentsInChildren<SpriteRenderer>(true)) sprite.enabled = false;
                var model = new GameObject("PotModel").transform; model.SetParent(prefab.transform, false);
                var tinted = new List<Renderer>();
                var climber = new GameObject("ClimberAppearance").transform; climber.SetParent(model, false);
                var engineer = new GameObject("EngineerAppearance").transform; engineer.SetParent(model, false);
                Part(climber, "IronPot", pot, clay, new Vector3(0, -.27f, 0), new Vector3(.9f, .58f, .9f), 4);
                var rim = Part(climber, "PotRim", handle, metal, new Vector3(0, .015f, 0), new Vector3(2.0f, .8f, 2.0f), 5);
                rim.transform.localRotation = Quaternion.Euler(90, 0, 0);
                tinted.Add(Part(climber, "ClimberVest", sphere, ceramic, new Vector3(0, .29f, .01f), new Vector3(.25f, .32f, .19f), 4).GetComponent<Renderer>());
                Part(climber, "ShirtCollar", block, cream, new Vector3(0, .51f, -.15f), new Vector3(.24f, .055f, .06f), 5);
                var eyeRoots = new List<Transform>();
                BuildFace(climber, false, eyeRoots, tinted);
                BuildFace(engineer, true, eyeRoots, tinted);
                tinted.Add(Part(engineer, "EngineerJacket", block, blue, new Vector3(0, .22f, 0), new Vector3(.55f, .62f, .30f), 4).GetComponent<Renderer>());
                Part(engineer, "ToolSatchel", block, wood, new Vector3(.34f, .13f, .08f), new Vector3(.22f, .40f, .28f), 3);
                Part(engineer, "Notebook", block, cream, new Vector3(.34f, .22f, -.09f), new Vector3(.13f, .20f, .05f), 5);
                Part(engineer, "JacketZip", block, cream, new Vector3(0, .23f, -.18f), new Vector3(.035f, .51f, .04f), 5);
                var feet = new Transform[2];
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1 : 1;
                    Part(engineer, "TrouserLeg", block, dark, new Vector3(side * .14f, -.28f, 0), new Vector3(.19f, .40f, .23f), 4);
                    feet[i] = Part(engineer, "WorkBoot", sphere, wood, new Vector3(side * .15f, -.51f, -.06f), new Vector3(.15f, .09f, .20f), 4).transform;
                    Part(engineer, "JacketSleeve", sphere, blue, new Vector3(side * .34f, .25f, 0), new Vector3(.12f, .26f, .12f), 4);
                    Part(engineer, "Hand", sphere, skin, new Vector3(side * .36f, .015f, -.06f), Vector3.one * .09f, 5);
                }
                var toolRig = new GameObject("HammerRig").transform; toolRig.SetParent(prefab.transform, false);
                var toolPivot = new GameObject("HammerPivot").transform; toolPivot.SetParent(toolRig, false);
                toolPivot.localPosition = new Vector3(0, .45f, -.42f); toolPivot.localRotation = Quaternion.Euler(0, 0, -135);
                var shaft = Part(toolPivot, "WoodenShaft", block, wood, new Vector3(.9f, 0, 0), new Vector3(1.8f, .085f, .09f), 7).transform;
                var head = Part(toolPivot, "SoftSteelHammer", block, metal, new Vector3(1.8f, 0, 0), new Vector3(.23f, .48f, .24f), 8).transform;
                var arms = new Transform[4]; var hands = new Transform[2];
                for (int i = 0; i < 4; i++) arms[i] = Part(toolRig, "Arm", block, skin, new Vector3(i % 2 == 0 ? -.18f : -.34f, .4f - i * .06f, -.4f), new Vector3(.26f, .12f, .14f), 6).transform;
                for (int i = 0; i < 2; i++) hands[i] = Part(toolPivot, "GripHand", sphere, skin, new Vector3(.38f + i * .25f, .025f, -.06f), Vector3.one * .08f, 9).transform;
                engineer.gameObject.SetActive(false);
                var shape = prefab.GetComponent<BoxCollider2D>(); shape.size = new Vector2(.8f, 1.8f); shape.offset = new Vector2(0, .3f);
                var dustRoot = new GameObject("LandingDust").transform; dustRoot.SetParent(prefab.transform, false);
                var puffs = new Transform[6];
                for (int i = 0; i < puffs.Length; i++) puffs[i] = Part(dustRoot, "Dust", sphere, cream, new Vector3(0, -.52f, -.6f), Vector3.one * .12f, 3).transform;
                var presentation = prefab.AddComponent<PotPresentation>();
                presentation.model = model; presentation.eyes = eyeRoots.ToArray(); presentation.feet = feet; presentation.ceramic = tinted.ToArray(); presentation.dust = puffs;
                presentation.climberAppearance = climber.gameObject; presentation.engineerAppearance = engineer.gameObject; presentation.hammerRig = toolRig.gameObject;
                presentation.toolPivot = toolPivot; presentation.shaft = shaft; presentation.hammerHead = head; presentation.arms = arms; presentation.hands = hands;
                PosePreviewArms(presentation);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        static void BuildFace(Transform parent, bool engineer, List<Transform> eyes, List<Renderer> tinted)
        {
            Part(parent, "Neck", sphere, skin, new Vector3(0, .56f, -.01f), new Vector3(.11f, .13f, .10f), 5);
            Part(parent, "Head", sphere, skin, new Vector3(0, .83f, -.01f), new Vector3(.27f, .30f, .23f), 5);
            if (engineer)
            {
                Part(parent, "Hair", sphere, hair, new Vector3(0, 1.03f, .01f), new Vector3(.285f, .17f, .24f), 5);
                Part(parent, "SideHair", block, hair, new Vector3(-.24f, .87f, 0), new Vector3(.07f, .23f, .22f), 5);
            }
            else
            {
                tinted.Add(Part(parent, "SafetyHelmet", sphere, ceramic, new Vector3(0, 1.055f, .01f), new Vector3(.31f, .15f, .26f), 6).GetComponent<Renderer>());
                Part(parent, "HelmetBrim", block, cream, new Vector3(0, .99f, -.035f), new Vector3(.69f, .055f, .57f), 7);
            }
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -.105f : .105f;
                var eye = new GameObject("BlinkEye").transform; eye.SetParent(parent, false); eye.localPosition = new Vector3(x, .84f, -.235f); eyes.Add(eye);
                if (engineer)
                {
                    Part(eye, "GoggleFrame", sphere, wood, Vector3.zero, new Vector3(.13f, .12f, .045f), 7);
                    Part(eye, "GoggleGlass", sphere, cream, new Vector3(0, 0, -.04f), new Vector3(.105f, .095f, .023f), 8);
                }
                else Part(eye, "EyeWhite", sphere, cream, Vector3.zero, new Vector3(.07f, .085f, .025f), 7);
                Part(eye, "Pupil", sphere, dark, new Vector3(0, -.005f, engineer ? -.065f : -.03f), new Vector3(.028f, .045f, .018f), 9);
            }
            Part(parent, "Nose", sphere, skin, new Vector3(0, .765f, -.255f), new Vector3(.055f, .055f, .06f), 8);
            Part(parent, "Smile", handle, hair, new Vector3(0, .69f, -.205f), new Vector3(.35f, .18f, .15f), 8);
        }

        static void PosePreviewArms(PotPresentation visual)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector3 shoulder = visual.model.TransformPoint(new Vector3(i == 0 ? -.17f : .17f, .45f, -.22f));
                Vector3 wrist = visual.toolPivot.TransformPoint(new Vector3(.38f + i * .25f, .025f, -.06f));
                Vector3 elbow = Vector3.Lerp(shoulder, wrist, .5f) + new Vector3(i == 0 ? -.1f : .1f, .12f, -.04f);
                for (int segment = 0; segment < 2; segment++)
                {
                    Vector3 a = segment == 0 ? shoulder : elbow, b = segment == 0 ? elbow : wrist;
                    Transform arm = visual.arms[i * 2 + segment]; arm.position = (a + b) * .5f;
                    arm.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                    arm.localScale = new Vector3(Vector3.Distance(a, b), .12f, .14f);
                }
            }
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
                if (parent.name.Contains("Ground") || parent.name.Contains("Boundary")) color = new Color(.48f, .59f, .56f);
                else if (parent.name.Contains("Step") || parent.name.Contains("Platform") || parent.name.Contains("Bridge")) color = Color.Lerp(color, new Color(.78f, .60f, .37f), .6f);
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
                    if (Mathf.Abs(parent.lossyScale.x) > 3 && parent.GetComponent<BoxCollider2D>() != null)
                    {
                        Part(art, "WoodenEdge", block, wood, new Vector3(0, .39f, -.19f), new Vector3(.97f, .12f, .12f), sprite.sortingOrder + 1);
                        for (int bolt = 0; bolt < 4; bolt++) Part(art, "RoundBolt", sphere, metal, new Vector3(-.42f + bolt * .28f, .38f, -.28f), new Vector3(.008f, .035f, .025f), sprite.sortingOrder + 2);
                    }
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
            for (int i = 0; i < 5; i++)
            {
                float x = -15 + i * 7;
                Part(root, "SoftHill", sphere, stone, new Vector3(x, -3.9f, 4), new Vector3(5.5f, 3.7f + i % 2, .25f), -30);
                for (int cloud = 0; cloud < 3; cloud++)
                    Part(root, "Cloud", sphere, cream, new Vector3(x + cloud * .7f, 4.9f + (i % 2) * .8f, 4), new Vector3(.9f, .45f + (cloud % 2) * .2f, .2f), -20);
                Part(root, "WorkshopPost", block, wood, new Vector3(x + 1, -1.0f, 2), new Vector3(.18f, 4.8f, .2f), -10);
                Part(root, "WorkshopCrossbeam", block, wood, new Vector3(x + 2, 1.35f, 2), new Vector3(2.4f, .18f, .2f), -10);
                Part(root, "SupplyCrate", block, wood, new Vector3(x + 2, -2.5f, 1.8f), new Vector3(1.3f, 1.1f, .45f), -9);
                Part(root, "CrateBand", block, metal, new Vector3(x + 2, -2.5f, 1.45f), new Vector3(.16f, 1.15f, .08f), -8);
            }
            Part(root, "WarmSun", sphere, cream, new Vector3(11, 6, 4), Vector3.one * .65f, -25);
        }

        static void BuildModels()
        {
            block = SaveMesh("BeveledBlock", BeveledBlock());
            sphere = SaveMesh("RoundDetail", Lathe(new[] { new Vector2(0, -1), new Vector2(.5f, -.866f), new Vector2(.866f, -.5f), new Vector2(1, 0), new Vector2(.866f, .5f), new Vector2(.5f, .866f), new Vector2(0, 1) }, 16));
            pot = SaveMesh("PotBody", Lathe(new[] { new Vector2(0, -.48f), new Vector2(.25f, -.48f), new Vector2(.38f, -.34f), new Vector2(.43f, -.08f), new Vector2(.39f, .20f), new Vector2(.30f, .35f), new Vector2(.34f, .42f), new Vector2(.32f, .49f), new Vector2(.25f, .49f), new Vector2(.23f, .30f), new Vector2(0, .29f) }, 24));
            handle = SaveMesh("RoundHandle", Torus());
            ceramic = SaveMaterial(Models + "/Ceramic.mat", new Color(.91f, .57f, .25f));
            cream = SaveMaterial(Models + "/Cream.mat", new Color(1, .9f, .68f));
            dark = SaveMaterial(Models + "/Ink.mat", new Color(.045f, .065f, .095f));
            stone = SaveMaterial(Models + "/Backdrop.mat", new Color(.46f, .66f, .61f));
            cyan = SaveMaterial(Models + "/Mint.mat", new Color(.25f, .72f, .72f));
            skin = SaveMaterial(Models + "/Skin.mat", new Color(.94f, .72f, .51f));
            hair = SaveMaterial(Models + "/Hair.mat", new Color(.27f, .19f, .15f));
            clay = SaveMaterial(Models + "/IronPot.mat", new Color(.26f, .32f, .35f));
            wood = SaveMaterial(Models + "/Wood.mat", new Color(.60f, .39f, .21f));
            metal = SaveMaterial(Models + "/SoftSteel.mat", new Color(.64f, .72f, .73f));
            blue = SaveMaterial(Models + "/EngineerBlue.mat", new Color(.25f, .61f, .66f));
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
            if (visual == null || visual.model == null || visual.ceramic.Length < 3 || visual.eyes.Length != 4 || visual.climberAppearance == null || visual.engineerAppearance == null || visual.toolPivot == null || visual.arms.Length != 4) throw new InvalidOperationException("Climber/engineer model references incomplete.");
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
            partner.GetComponent<PotPresentation>().climberAppearance.SetActive(false);
            partner.GetComponent<PotPresentation>().engineerAppearance.SetActive(true);
            partner.GetComponent<PotPresentation>().hammerRig.SetActive(false);
            foreach (var puff in preview.GetComponent<PotPresentation>().dust) puff.gameObject.SetActive(false);
            foreach (var puff in partner.GetComponent<PotPresentation>().dust) puff.gameObject.SetActive(false);
            var tint = new MaterialPropertyBlock(); tint.SetColor("_BaseColor", new Color(.25f, .82f, .82f));
            foreach (var renderer in partner.GetComponent<PotPresentation>().ceramic) renderer.SetPropertyBlock(tint);
            bool pixel = prefab.GetComponent<PotPresentation>().pixelArt; int width = pixel ? 640 : 1600, height = pixel ? 360 : 900;
            var target = new RenderTexture(width, height, 24) { filterMode = FilterMode.Point }; var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            Vector3 cameraPosition = camera.transform.position; float cameraSize = camera.orthographicSize;
            var state = UnityEngine.Object.FindAnyObjectByType<StageState>();
            var backdrop = GameObject.Find(pixel ? "PixelBackdrop" : "FestivalBackdrop");
            bool firstActive = state.stageOneRoot.activeSelf;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                Directory.CreateDirectory("Docs/Previews"); File.WriteAllBytes("Docs/Previews/FestivalStage01.png", image.EncodeToPNG());
                state.stageOneRoot.SetActive(false); if (backdrop != null) backdrop.SetActive(false);
                preview.transform.position = new Vector3(-.85f, 0, 0); partner.transform.position = new Vector3(.85f, 0, 0);
                camera.transform.position = new Vector3(0, .05f, -10); camera.orthographicSize = pixel ? 1.875f : 1.4f;
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
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

