using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.EditorAutomation
{
    /// <summary>
    /// UI spec 4-UI-a ✅: flags hardcoded colors in prefabs — every Graphic must
    /// carry a ThemedElement (or be pure white for sprite tinting via code paths
    /// that themselves resolve theme keys). Runs as an import postprocessor over
    /// Assets/Prefabs and on demand via the menu item.
    /// </summary>
    public sealed class ThemeLiteralScan : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (path.StartsWith("Assets/Prefabs/") && path.EndsWith(".prefab"))
                {
                    ScanPrefab(path);
                }
            }
        }

        [MenuItem("Riptide/Scan Prefabs For Color Literals")]
        public static void ScanAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
            int flagged = 0;
            foreach (string guid in guids)
            {
                flagged += ScanPrefab(AssetDatabase.GUIDToAssetPath(guid));
            }

            Debug.Log(flagged == 0
                ? $"Theme literal scan: clean ({guids.Length} prefabs)."
                : $"Theme literal scan: {flagged} unthemed Graphics flagged.");
        }

        private static int ScanPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return 0;
            }

            int flagged = 0;
            var graphics = new List<Graphic>(prefab.GetComponentsInChildren<Graphic>(true));
            foreach (Graphic graphic in graphics)
            {
                bool themed = graphic.GetComponent<Riptide.UI.ThemedElement>() != null;
                bool neutralWhite = graphic.color == Color.white;
                if (!themed && !neutralWhite)
                {
                    Debug.LogWarning(
                        $"[ThemeLiteralScan] {path}: '{graphic.gameObject.name}' has a hardcoded color and no ThemedElement (spec §1).",
                        graphic);
                    flagged++;
                }
            }

            return flagged;
        }
    }
}
