using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Riptide.UI;

namespace Riptide.EditorAutomation
{
    /// <summary>
    /// UI spec 4-UI-b: serializes the §3 component builders into prefabs under
    /// Assets/Prefabs/Components (DECISIONS: generated, never hand-authored) and
    /// audits every interactable for the 120 ref-px touch target. File-triggered
    /// (Temp/riptide_genprefabs.txt) like the rest of the automation.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoPrefabGen
    {
        private const string TriggerPath = "Temp/riptide_genprefabs.txt";
        private const string ResultPath = "Temp/riptide_prefabs_result.txt";
        private const string OutputDir = "Assets/Prefabs/Components";
        private static double nextPollTime;

        static AutoPrefabGen()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPollTime || EditorApplication.isPlaying
                || EditorApplication.isCompiling)
            {
                return;
            }

            nextPollTime = EditorApplication.timeSinceStartup + 2.0;
            if (!File.Exists(TriggerPath))
            {
                return;
            }

            File.Delete(TriggerPath);
            Generate();
        }

        [MenuItem("Riptide/Generate Component Prefabs")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputDir);
            var report = new StringBuilder();
            int audited = 0;
            int violations = 0;

            var canvasGo = new GameObject("PrefabGenCanvas", typeof(RectTransform));
            var canvasRoot = (RectTransform)canvasGo.transform;
            try
            {
                Save(report, ref audited, ref violations, "ButtonPrimary",
                    UiComponents.ButtonPrimary(canvasRoot, "ButtonPrimary", "Primary", () => { }).gameObject);
                Save(report, ref audited, ref violations, "ButtonSecondary",
                    UiComponents.ButtonSecondary(canvasRoot, "ButtonSecondary", "Secondary", () => { }).gameObject);
                Save(report, ref audited, ref violations, "ButtonGhost",
                    UiComponents.ButtonGhost(canvasRoot, "ButtonGhost", "Ghost", () => { }).gameObject);
                Save(report, ref audited, ref violations, "ButtonReward",
                    UiComponents.ButtonReward(canvasRoot, "ButtonReward", "Free · Watch ad", () => { }).gameObject);
                Save(report, ref audited, ref violations, "IconButton",
                    UiComponents.IconButton(canvasRoot, "IconButton", "✕", () => { }).gameObject);
                Save(report, ref audited, ref violations, "Card",
                    UiComponents.Card(canvasRoot, "Card", new Vector2(820f, 520f)).gameObject);
                Save(report, ref audited, ref violations, "Sheet",
                    UiComponents.SheetComponent(canvasRoot, "Sheet", 900f).gameObject);
                Save(report, ref audited, ref violations, "Modal",
                    UiComponents.Modal(canvasRoot, "Modal", "Title", "Body").parent.gameObject);
                Save(report, ref audited, ref violations, "CoinCounter",
                    UiComponents.CoinCounterComponent(canvasRoot).gameObject);
                Save(report, ref audited, ref violations, "StarTriplet",
                    UiComponents.StarTripletComponent(canvasRoot).gameObject);
                Save(report, ref audited, ref violations, "ProgressPips",
                    UiComponents.ProgressPipsComponent(canvasRoot, 5).gameObject);
                Save(report, ref audited, ref violations, "StreakFlame",
                    UiComponents.StreakFlameComponent(canvasRoot).gameObject);
                Save(report, ref audited, ref violations, "CreatureChip",
                    UiComponents.CreatureChipComponent(canvasRoot, 0).gameObject);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }

            AssetDatabase.SaveAssets();
            report.AppendLine($"PREFABGEN done components=13 audited={audited} touchTargetViolations={violations}");
            File.WriteAllText(ResultPath, report.ToString());
            Debug.Log(report.ToString());
        }

        private static void Save(StringBuilder report, ref int audited, ref int violations,
            string name, GameObject instance)
        {
            int before = violations;
            AuditTouchTargets(instance, ref audited, ref violations, report, name);
            string path = $"{OutputDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            report.AppendLine($"saved {path}{(violations > before ? "  !! touch-target violation" : "")}");
        }

        /// <summary>Spec §3/§8: every raycast-receiving interactable ≥120 ref-px both axes.</summary>
        private static void AuditTouchTargets(GameObject root, ref int audited, ref int violations,
            StringBuilder report, string name)
        {
            float min = ThemeRuntime.Theme.MinTouchTargetRefPx;
            foreach (var selectable in root.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
            {
                audited++;
                float width = 0f;
                float height = 0f;
                foreach (var graphic in selectable.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    if (!graphic.raycastTarget)
                    {
                        continue;
                    }

                    var rect = ((RectTransform)graphic.transform).sizeDelta;
                    width = Mathf.Max(width, rect.x);
                    height = Mathf.Max(height, rect.y);
                }

                if (width < min || height < min)
                {
                    violations++;
                    report.AppendLine($"  !! {name}/{selectable.name}: hit rect {width}x{height} < {min}");
                }
            }
        }
    }
}
