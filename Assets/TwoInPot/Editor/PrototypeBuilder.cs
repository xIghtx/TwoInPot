using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;

namespace TwoInPot.Editor
{
    public static class PrototypeBuilder
    {
        const string Root = "Assets/TwoInPot";
        static Sprite square;
        static Material material;
        [MenuItem("Two In Pot/Create Prototype Scene")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before creating the scene.");
            if (File.Exists(Root + "/Scenes/Prototype.unity")) throw new InvalidOperationException("Prototype already exists; open it instead of regenerating.");
            // Preserve any user work in the currently open scene before creating our separate scene.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the current scene before generating the prototype.");
            foreach (var folder in new[] { "Art", "Data", "Prefabs", "Scenes" }) Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            var texture = new Texture2D(16, 16);
            var pixels = new Color[256]; for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes(Root + "/Art/Square.png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Root + "/Art/Square.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/Art/Square.png");
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 16; importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
            square = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Square.png");
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, Root + "/Art/Unlit.mat");
            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            stage.physicalSpawn = new Vector2(-6, -2.3f); stage.logicSpawn = new Vector2(-11.5f, -2.3f);
            AssetDatabase.CreateAsset(stage, Root + "/Data/Stage01.asset");
            var stage2 = ScriptableObject.CreateInstance<StageDefinition>();
            stage2.displayName = "02 / VECTOR LAUNCH (PLANNED)"; stage2.movementMode = MovementMode.Launch;
            AssetDatabase.CreateAsset(stage2, Root + "/Data/Stage02_Planned.asset");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 8;
            camera.transform.position = new Vector3(0, 1, -10); camera.backgroundColor = new Color(.055f, .09f, .15f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.gameObject.AddComponent<AudioListener>();
            for (int x = -14; x <= 14; x++) Box("Grid", new Vector2(x, 1), new Vector2(.018f, 15), new Color(.09f, .15f, .22f));
            for (int y = -6; y <= 8; y++) Box("Grid", new Vector2(0, y), new Vector2(28, .018f), new Color(.09f, .15f, .22f));
            Box("Ground", new Vector2(0, -4), new Vector2(28, 1.2f), new Color(.22f, .31f, .43f), true);
            Box("LeftBoundary", new Vector2(-14, 0), new Vector2(.5f, 12), new Color(.22f, .31f, .43f), true);
            Box("RightBoundary", new Vector2(14, 0), new Vector2(.5f, 12), new Color(.22f, .31f, .43f), true);
            Box("StepOne", new Vector2(-2, -2.25f), new Vector2(2.5f, .45f), new Color(.28f, .40f, .53f), true);
            Box("StepTwo", new Vector2(1, -1.1f), new Vector2(2, .45f), new Color(.28f, .40f, .53f), true);
            var gate = Box("CooperativeGate", new Vector2(4, -.6f), new Vector2(.55f, 5.6f), new Color(.95f, .52f, .25f), true);
            var button = Box("LogicSwitch", new Vector2(-5.5f, -3.25f), new Vector2(1.2f, .3f), new Color(.3f, .7f, .72f));
            var switchPoint = new GameObject("SwitchDetectionPoint"); switchPoint.transform.position = new Vector3(-5.5f, -2.75f, 0);
            Box("FlagPole", new Vector2(10, -2), new Vector2(.12f, 2.8f), new Color(.7f, .8f, .9f));
            Box("Flag", new Vector2(10.6f, -.85f), new Vector2(1.2f, .7f), new Color(.4f, 1f, .7f));
            var stateGo = new GameObject("StageState"); stateGo.AddComponent<NetworkObject>();
            var state = stateGo.AddComponent<StageState>(); state.gate = gate; state.logicSwitch = switchPoint.transform;
            state.switchVisual = button.GetComponent<SpriteRenderer>();
            var player = new GameObject("CoopPlayer"); player.AddComponent<NetworkObject>();
            var rigidbody = player.AddComponent<Rigidbody2D>(); rigidbody.gravityScale = stage.gravityScale;
            rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation; rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = player.AddComponent<BoxCollider2D>(); collider.size = new Vector2(.8f, 1.2f);
            var frictionless = new PhysicsMaterial2D("PlayerFrictionless") { friction = 0, bounciness = 0 };
            AssetDatabase.CreateAsset(frictionless, Root + "/Data/PlayerFrictionless.physicsMaterial2D"); collider.sharedMaterial = frictionless;
            var visual = Box("Body", Vector2.zero, new Vector2(.8f, 1.2f), Color.white); visual.transform.SetParent(player.transform, false);
            visual.GetComponent<SpriteRenderer>().sortingOrder = 3;
            foreach (float x in new[] { -.17f, .17f })
            {
                var eye = Box("Eye", new Vector2(x, .18f), new Vector2(.10f, .16f), new Color(.07f, .12f, .18f));
                eye.transform.SetParent(player.transform, false); eye.GetComponent<SpriteRenderer>().sortingOrder = 4;
            }
            var nt = player.AddComponent<NetworkTransform>();
            nt.SyncPositionZ = false; nt.SyncRotAngleX = false; nt.SyncRotAngleY = false; nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false; nt.SyncScaleY = false; nt.SyncScaleZ = false; nt.Interpolate = true;
            var controller = player.AddComponent<CoopPlayer>(); controller.stage = stage; controller.bodyVisual = visual.GetComponent<SpriteRenderer>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(player, Root + "/Prefabs/CoopPlayer.prefab"); UnityEngine.Object.DestroyImmediate(player);
            var net = new GameObject("NetworkManager");
            var transport = net.AddComponent<UnityTransport>();
            var manager = net.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = transport; manager.NetworkConfig.PlayerPrefab = prefab;
            manager.NetworkConfig.TickRate = 60; manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            var session = net.AddComponent<SessionController>(); session.manager = manager; session.transport = transport;
            var ui = new GameObject("ConnectionUI");
            var panel = ScriptableObject.CreateInstance<PanelSettings>(); panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            AssetDatabase.CreateAsset(panel, Root + "/UI/PanelSettings.asset");
            var document = ui.AddComponent<UIDocument>(); document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/Connection.uxml");
            var view = ui.AddComponent<ConnectionView>(); view.session = session;
            net.AddComponent<SmokeProbe>();
            PlayerSettings.productName = "Two In Pot"; PlayerSettings.companyName = "SchoolClub";
            PlayerSettings.runInBackground = true; PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/Prototype.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Root + "/Scenes/Prototype.unity", true) };
            // NGO validates in-scene IDs only after the saved scene is in the build list.
            EditorSceneManager.OpenScene(Root + "/Scenes/Prototype.unity");
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[TwoInPot] Prototype scene created and ready.");
        }
        static GameObject Box(string name, Vector2 position, Vector2 size, Color color, bool solid = false)
        {
            var go = new GameObject(name); go.transform.position = position; go.transform.localScale = size;
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = square; renderer.sharedMaterial = material; renderer.color = color;
            if (solid) { go.layer = 6; go.AddComponent<BoxCollider2D>(); }
            return go;
        }
        [MenuItem("Two In Pot/Validate Prototype")]
        public static void Validate()
        {
            if (Vector2.Distance(MotionMath.LaunchVelocity(90, 10), new Vector2(0, 10)) > .001f) throw new Exception("Vertical launch failed");
            if (Vector2.Distance(MotionMath.PositionAt(Vector2.zero, new Vector2(2, 4), new Vector2(0, -10), 1), new Vector2(2, -1)) > .001f) throw new Exception("Ballistics failed");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CoopPlayer.prefab");
            if (prefab == null || prefab.GetComponent<NetworkObject>() == null || prefab.GetComponent<CoopPlayer>().stage == null) throw new Exception("Player prefab invalid");
            if (EditorBuildSettings.scenes.Length != 1 || !EditorBuildSettings.scenes[0].enabled) throw new Exception("Build scene invalid");
            Debug.Log("[TwoInPot] VALIDATION PASSED: ballistics, prefab, stage config, build scene.");
        }
        [MenuItem("Two In Pot/Add Stage 2 Launch Area")]
        public static void AddStageTwoLaunchArea() => RebuildStageTwoLaunchArea();

        [MenuItem("Two In Pot/Rebuild Stage 2 Launch Area")]
        public static void RebuildStageTwoLaunchArea()
        {
            var scene = EditorSceneManager.OpenScene(Root + "/Scenes/Prototype.unity");
            square = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Square.png");
            material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Unlit.mat");
            if (square == null || material == null) throw new InvalidOperationException("Stage art is missing.");
            var state = UnityEngine.Object.FindAnyObjectByType<StageState>();
            if (state == null) throw new InvalidOperationException("StageState is missing.");
            if (state.logicSwitch != null) state.logicSwitch.position = new Vector3(-5.5f, -2.75f, 0);
            if (state.switchVisual != null) { state.switchVisual.transform.position = new Vector3(-5.5f, -3.25f, 0); state.switchVisual.transform.localScale = new Vector3(1.2f, .3f, 1); }
            var stageOneRoot = FindLargestRoot("Stage01World");
            if (stageOneRoot == null)
            {
                stageOneRoot = new GameObject("Stage01World");
                string[] stageOneNames = { "Ground", "StepOne", "StepTwo", "CooperativeGate", "LogicSwitch", "FlagPole", "Flag" };
                foreach (var item in stageOneNames) { var found = FindSceneObject(item); if (found != null) found.transform.SetParent(stageOneRoot.transform, true); }
            }
            // Scene.GetRootGameObjects reliably includes inactive objects.  The old
            // global search skipped inactive Stage02World roots, leaving duplicates.
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Stage02World") UnityEngine.Object.DestroyImmediate(root);
            var stageTwoRoot = new GameObject("Stage02World");
            CreateStageTwoBox(stageTwoRoot.transform, "LaunchGround", new Vector2(0, -4), new Vector2(28, 1.2f), new Color(.19f, .27f, .38f), true);
            CreateStageTwoBox(stageTwoRoot.transform, "LaunchPad", new Vector2(-8, -3.25f), new Vector2(2.2f, .25f), new Color(1f, .62f, .30f), true);
            CreateStageTwoBox(stageTwoRoot.transform, "AimConsole", new Vector2(-11.5f, -3.25f), new Vector2(1.6f, .25f), new Color(.32f, .83f, .85f), true);
            CreateStageTwoBox(stageTwoRoot.transform, "VectorWall", new Vector2(1, -1.55f), new Vector2(.7f, 4.9f), new Color(.58f, .35f, .58f), true);
            CreateStageTwoBox(stageTwoRoot.transform, "LandingPlatform", new Vector2(10, -1.3f), new Vector2(4, .35f), new Color(.4f, 1f, .7f), true);
            CreateStageTwoBox(stageTwoRoot.transform, "TargetFlag", new Vector2(11.5f, -.45f), new Vector2(.8f, 1.4f), new Color(.4f, 1f, .7f));
            stageTwoRoot.SetActive(false);
            var stage2 = AssetDatabase.LoadAssetAtPath<StageDefinition>(Root + "/Data/Stage02_Planned.asset");
            stage2.displayName = "02 / VECTOR LAUNCH"; stage2.movementMode = MovementMode.Launch;
            stage2.physicalSpawn = new Vector2(-8, -2.3f); stage2.logicSpawn = new Vector2(-11.5f, -2.3f);
            stage2.minimumLaunchAngle = 5; stage2.maximumLaunchAngle = 175; stage2.startingLaunchAngle = 45;
            stage2.minimumLaunchPower = 7; stage2.maximumLaunchPower = 18; stage2.chargeSpeed = 9; stage2.aimSpeed = 42;
            state.stageOne = AssetDatabase.LoadAssetAtPath<StageDefinition>(Root + "/Data/Stage01.asset");
            state.stageTwo = stage2; state.stageOneRoot = stageOneRoot; state.stageTwoRoot = stageTwoRoot;
            EditorUtility.SetDirty(stage2); EditorUtility.SetDirty(state);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[TwoInPot] Stage 2 launch area rebuilt.");
        }
        [MenuItem("Two In Pot/Repair Stage 2 Visuals")]
        public static void RepairStageTwoVisuals()
        {
            var scene = EditorSceneManager.OpenScene(Root + "/Scenes/Prototype.unity");
            square = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Square.png");
            material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Unlit.mat");
            var root = FindLargestRoot("Stage02World");
            if (root == null) throw new InvalidOperationException("Stage 2 is missing.");
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.sprite = square;
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[TwoInPot] Stage 2 visuals repaired.");
        }
        [MenuItem("Two In Pot/Build Stages 3 To 8")]
        public static void BuildStagesThreeToEight()
        {
            RebuildStageTwoLaunchArea();
            var scene = EditorSceneManager.OpenScene(Root + "/Scenes/Prototype.unity");
            square = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Square.png");
            material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Unlit.mat");
            var state = UnityEngine.Object.FindAnyObjectByType<StageState>();
            if (state == null) throw new InvalidOperationException("StageState is missing.");
            var roots = new GameObject[6]; var definitions = new StageDefinition[6];
            var targets = new Transform[6]; var exits = new Transform[6]; var gates = new GameObject[6]; var mechanisms = new Transform[6];
            string[] names = { "MOVING PLATFORM", "WIND TUNNEL", "MAGNETIC CRATE", "SPEED GATE", "ROTATING BRIDGE", "FINAL CIRCUIT" };
            for (int i = 0; i < 6; i++)
            {
                int number = i + 3;
                foreach (var root in scene.GetRootGameObjects()) if (root.name == "Stage" + number.ToString("00") + "World") UnityEngine.Object.DestroyImmediate(root);
                string path = Root + "/Data/Stage" + number.ToString("00") + ".asset";
                definitions[i] = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
                if (definitions[i] == null) { definitions[i] = ScriptableObject.CreateInstance<StageDefinition>(); AssetDatabase.CreateAsset(definitions[i], path); }
                definitions[i].displayName = number.ToString("00") + " / " + names[i]; definitions[i].movementMode = MovementMode.Platform;
                definitions[i].physicalSpawn = new Vector2(-11.2f, -2.3f); definitions[i].logicSpawn = new Vector2(-8.2f, -2.3f);
                EditorUtility.SetDirty(definitions[i]);
                roots[i] = new GameObject("Stage" + number.ToString("00") + "World"); roots[i].SetActive(false);
                BuildPuzzleWorld(roots[i].transform, number, out targets[i], out exits[i], out gates[i], out mechanisms[i]);
            }
            state.additionalStages = definitions; state.additionalStageRoots = roots; state.logicTargets = targets; state.exits = exits; state.puzzleGates = gates; state.puzzleMechanisms = mechanisms;
            EditorUtility.SetDirty(state); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[TwoInPot] Stages 3 to 8 built.");
        }
        static void BuildPuzzleWorld(Transform root, int stage, out Transform target, out Transform exit, out GameObject gate, out Transform mechanism)
        {
            target = new GameObject("LogicTarget").transform; target.SetParent(root); target.position = new Vector3(-5.5f, -2.7f, 0);
            exit = new GameObject("Exit").transform; exit.SetParent(root); exit.position = new Vector3(10.8f, -2.7f, 0);
            CreateStageTwoBox(root, "LeftGround", new Vector2(-8, -4), new Vector2(12, 1.2f), new Color(.22f, .31f, .43f), true);
            CreateStageTwoBox(root, "RightGround", new Vector2(8, -4), new Vector2(12, 1.2f), new Color(.22f, .31f, .43f), true);
            CreateStageTwoBox(root, "LogicConsole", target.position, new Vector2(1.2f, .22f), new Color(.32f, .83f, .85f), false);
            CreateStageTwoBox(root, "ExitFlag", exit.position + Vector3.up * 1.2f, new Vector2(.9f, 1.7f), new Color(.4f, 1f, .7f), false);
            gate = null; mechanism = null;
            if (stage == 3)
            {
                var pivot = new GameObject("MovingPlatform"); pivot.transform.SetParent(root); pivot.transform.localPosition = Vector3.zero; mechanism = pivot.transform;
                CreateStageTwoBox(pivot.transform, "Platform", Vector2.zero, new Vector2(3.2f, .35f), new Color(.95f, .62f, .30f), true).transform.localPosition = new Vector3(0, -2.25f, 0);
            }
            else if (stage == 4)
            {
                CreateStageTwoBox(root, "WindFloor", new Vector2(0, -4), new Vector2(4, 1.2f), new Color(.22f, .31f, .43f), true);
                var wind = CreateStageTwoBox(root, "Wind", new Vector2(0, -1.8f), new Vector2(6, 2.4f), new Color(.32f, .83f, .85f), false); wind.GetComponent<SpriteRenderer>().color = new Color(.32f, .83f, .85f, .28f); mechanism = wind.transform;
            }
            else if (stage == 5)
            {
                var crate = CreateStageTwoBox(root, "MagneticCrate", Vector2.zero, new Vector2(2.8f, .45f), new Color(.58f, .35f, .58f), true); mechanism = crate.transform;
            }
            else if (stage == 6 || stage == 8)
            {
                CreateStageTwoBox(root, "BridgeFloor", new Vector2(0, -4), new Vector2(4, 1.2f), new Color(.22f, .31f, .43f), true);
                gate = CreateStageTwoBox(root, "SpeedGate", new Vector2(5.5f, -1.3f), new Vector2(.55f, 5.4f), new Color(.95f, .52f, .25f), true);
                if (stage == 8) CreateStageTwoBox(root, "FinalPlatform", new Vector2(2.5f, -1.2f), new Vector2(3, .35f), new Color(.4f, 1f, .7f), true);
            }
            else if (stage == 7)
            {
                var bridge = new GameObject("RotatingBridge"); bridge.transform.SetParent(root); bridge.transform.localPosition = Vector3.zero; mechanism = bridge.transform;
                CreateStageTwoBox(bridge.transform, "Bridge", Vector2.zero, new Vector2(6.2f, .35f), new Color(.95f, .62f, .30f), true).transform.localPosition = new Vector3(0, -2.2f, 0);
            }
        }
        static GameObject CreateStageTwoBox(Transform parent, string name, Vector2 position, Vector2 size, Color color, bool solid = false)
        {
            var go = Box(name, position, size, color, solid);
            go.transform.SetParent(parent, true);
            // Keep the serialized references explicit so regenerated scenes cannot
            // lose their visual assets during an editor domain reload.
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.sharedMaterial = material;
            if (renderer.sprite == null || renderer.sharedMaterial == null)
                throw new InvalidOperationException("Stage 2 visual asset assignment failed.");
            EditorUtility.SetDirty(renderer);
            return go;
        }
        static GameObject FindSceneObject(string name)
        {
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                if (candidate.name == name && candidate.scene.IsValid()) return candidate;
            return null;
        }
        static GameObject FindLargestRoot(string name)
        {
            GameObject best = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                if (candidate.name == name && candidate.scene.IsValid() && (best == null || candidate.transform.childCount > best.transform.childCount)) best = candidate;
            return best;
        }
    }
}
