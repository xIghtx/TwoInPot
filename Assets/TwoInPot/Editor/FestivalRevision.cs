using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TwoInPot.Editor
{
    public static class FestivalRevision
    {
        const string Art = "Assets/TwoInPot/Art/Pixel";
        static Sprite[] tiles;
        static readonly Dictionary<string, Sprite> baked = new Dictionary<string, Sprite>();
        static Sprite Tile(int index, int width, int height)
        {
            string key = index + "_" + width + "_" + height;
            if (!baked.TryGetValue(key, out var sprite)) { sprite = PixelDensityBaker.Resize(tiles[index], width, height, index == 1 || index == 4); baked[key] = sprite; }
            return sprite;
        }
        static GameObject Piece(Transform parent, string name, Vector2 pos, Vector2 size, int tile, bool solid, Color color)
        {
            var item = new GameObject(name); item.transform.SetParent(parent, false); item.transform.localPosition = pos;
            var renderer = item.AddComponent<SpriteRenderer>(); renderer.sprite = Tile(tile, Mathf.RoundToInt(size.x * 24), Mathf.RoundToInt(size.y * 24)); renderer.color = color;
            renderer.sortingOrder = solid ? 1 : 3;
            var texture = renderer.sprite.texture; string path = Art + "/Pixel_" + texture.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("TwoInPot/PixelUnlit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetTexture("_PixelTex", texture); EditorUtility.SetDirty(mat); renderer.sharedMaterial = mat;
            if (solid) { item.layer = 6; item.AddComponent<BoxCollider2D>().size = size; }
            return item;
        }
        public static void ApplyAndBuild()
        {
            baked.Clear();
            tiles = new Sprite[16];
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(Art + "/WorkshopTiles.png"))
                if (asset is Sprite sprite && sprite.name.StartsWith("WorkshopTiles") && int.TryParse(sprite.name.Substring(13), out int i) && i < 16) tiles[i] = sprite;
            if (tiles[1] == null) throw new InvalidOperationException("Missing native workshop tile atlas");
            const string prefabPath = "Assets/TwoInPot/Prefabs/CoopPlayer.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var art = prefab.GetComponent<PotPresentation>();
                var oldDots = prefab.transform.Find("PushPreview"); if (oldDots != null) UnityEngine.Object.DestroyImmediate(oldDots.gameObject);
                var dots = new GameObject("PushPreview").transform; dots.SetParent(prefab.transform, false); art.pushDots = new SpriteRenderer[8];
                var square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TwoInPot/Art/Square.png");
                var squareMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Pixel_Square.mat");
                for (int i = 0; i < 8; i++)
                {
                    var dot = new GameObject("Dot" + i); dot.transform.SetParent(dots, false); dot.transform.localScale = Vector3.one * (2f / 24);
                    var renderer = dot.AddComponent<SpriteRenderer>(); renderer.sprite = square; renderer.sharedMaterial = squareMaterial; renderer.sortingOrder = 15; renderer.color = new Color(1,.8f,.3f); renderer.enabled = false; art.pushDots[i] = renderer;
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene = EditorSceneManager.OpenScene("Assets/TwoInPot/Scenes/Prototype.unity");
            var stage = UnityEngine.Object.FindAnyObjectByType<StageState>();
            var circuit = stage.GetComponent<StageCircuit>(); if (circuit == null) circuit = stage.gameObject.AddComponent<StageCircuit>(); stage.circuit = circuit;
            circuit.gates = new Transform[36]; circuit.markers = new SpriteRenderer[36];
            string[] names = { "신호 통로", "발사 시험실", "운반 회로", "바람 덕트", "자석 보관고", "가속 검문소", "균형 교량", "기억 문", "승강 회랑", "시간 잠금실", "추 균형실", "최종 반환로" };
            for (int room = 1; room <= 12; room++)
            {
                Transform root = (room == 1 ? stage.stageOneRoot : room == 2 ? stage.stageTwoRoot : stage.additionalStageRoots[room - 3]).transform;
                var old = root.Find("ExtendedRoute"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var route = new GameObject("ExtendedRoute").transform; route.SetParent(root, false);
                Color color = room <= 3 ? new Color(.8f,.93f,1) : room <= 6 ? new Color(.72f,.72f,1) : room <= 9 ? new Color(1,.76f,.52f) : new Color(.48f,.62f,.85f);
                Piece(route, "SafetyFloor", new Vector2(25,-5), new Vector2(28,1), 1, true, color);
                for (int station = 0; station < 3; station++)
                {
                    Vector2 point = StageCircuit.Point(room, station); int index = (room - 1) * 3 + station;
                    Piece(route, "CheckpointPlatform" + station, point + Vector2.down * .8f, new Vector2(3,.4f), room % 2 == 0 ? 3 : 4, true, color);
                    circuit.markers[index] = Piece(route, "StationMarker" + station, point + Vector2.down * .5f, new Vector2(.75f,.2f), 14, false, Color.white).GetComponent<SpriteRenderer>();
                    circuit.gates[index] = Piece(route, "CircuitGate" + station, new Vector2(point.x - 2,-.5f), new Vector2(.5f,9), 6, true, color).transform;
                    // Twelve distinct profiles: stair, narrow shaft, staggered shelves, low ceiling.
                    int profile = (room + station) % 4;
                    if (profile == 0)
                    {
                        Piece(route,"Step"+station,new Vector2(point.x+2,-3.6f),new Vector2(1.5f,.5f),4,true,color);
                        Piece(route,"StepHigh"+station,new Vector2(point.x+3.7f,-2.5f),new Vector2(1.4f,.4f),4,true,color);
                    }
                    else if (profile == 1) Piece(route,"BraceColumn"+station,new Vector2(point.x+3,-2.4f),new Vector2(.6f,3.2f),2,true,color);
                    else if (profile == 2)
                    {
                        Piece(route,"LowerShelf"+station,new Vector2(point.x+2.5f,-3),new Vector2(2,.4f),4,true,color);
                        Piece(route,"UpperShelf"+station,new Vector2(point.x+4.2f,-1.5f),new Vector2(1.5f,.4f),4,true,color);
                    }
                    else Piece(route,"LowCeiling"+station,new Vector2(point.x+3.1f,1.3f),new Vector2(3.6f,.5f),4,true,color);
                    // Rails, warning panels and lamp counts distinguish rooms without visual noise.
                    for (int lamp = 0; lamp <= room % 3; lamp++) Piece(route,"SignalLamp"+station+"_"+lamp,new Vector2(point.x-.6f+lamp*.55f,2.8f),new Vector2(.35f,.5f),10,false,color);
                }
                Piece(route,"FinalPlatform",new Vector2(36,-3.4f),new Vector2(4,.4f),4,true,color);
                Piece(route,"FinalFlag",new Vector2(36,-1.7f),new Vector2(.7f,1.4f),7,false,Color.white);
                var definition = room == 1 ? stage.stageOne : room == 2 ? stage.stageTwo : stage.additionalStages[room - 3];
                definition.displayName = room.ToString("00") + " / " + names[room - 1]; EditorUtility.SetDirty(definition);
            }
            var boundary = GameObject.Find("RightBoundary"); if (boundary != null) { Vector3 p = boundary.transform.position; p.x = 40; boundary.transform.position = p; }
            var backdrop = GameObject.Find("PixelBackdrop");
            for (int i = 1; i <= 2; i++)
            {
                string name = "ExtensionBackdrop" + i; var old = GameObject.Find(name); if (old != null) UnityEngine.Object.DestroyImmediate(old);
                var copy = UnityEngine.Object.Instantiate(backdrop); copy.name = name; copy.transform.position += Vector3.right * (640f / 24 * i);
                var tint = copy.AddComponent<ExtensionBackdrop>(); tint.source = backdrop.GetComponent<SpriteRenderer>();
            }
            if (Camera.main.GetComponent<ExtensionCamera>() == null) Camera.main.gameObject.AddComponent<ExtensionCamera>();
            EditorUtility.SetDirty(stage); EditorUtility.SetDirty(circuit); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            FestivalUpgrade.BuildAndVerify();
        }
    }
}
