using UnityEditor;
using UnityEngine;

namespace TwoInPot.Editor
{
    public static class TraversalTuning
    {
        public static void ApplyAndBuild()
        {
            const string root = "Assets/TwoInPot";
            string path = root + "/Prefabs/CoopPlayer.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var collider = prefab.GetComponent<BoxCollider2D>();
                // Preserve the feet at -0.6, so floor contact and spawn heights remain stable.
                collider.size = new Vector2(.6f, 1.35f);
                collider.offset = new Vector2(0, .075f);
                var art = prefab.GetComponent<PotPresentation>();
                art.climberSprite.transform.localScale = Vector3.one * .75f;
                art.engineerSprite.transform.localScale = Vector3.one * .75f;
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            foreach (string guid in AssetDatabase.FindAssets("t:StageDefinition", new[] { root + "/Data" }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<StageDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                definition.jumpSpeed = 13;
                EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
            FestivalUpgrade.BuildAndVerify();
            Debug.Log("[POT_TRAVERSAL] Smaller characters/colliders and stronger jumps applied.");
        }
    }
}
