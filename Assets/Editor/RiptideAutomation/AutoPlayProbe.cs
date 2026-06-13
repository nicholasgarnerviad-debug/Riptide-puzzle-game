using System.Collections.Generic;
using System.IO;
using System.Text;
using Riptide.UI;
using UnityEditor;
using UnityEngine;

namespace Riptide.EditorAutomation
{
    /// <summary>
    /// Boot probe: drop Temp/riptide_play.txt and the editor enters Play mode on
    /// the open scene, waits ~3s, then writes Temp/riptide_play_result.txt with
    /// what actually spawned (ScreenManager, screens, camera color) plus any
    /// errors/exceptions logged during boot — and STAYS playing so a human can
    /// look. The pending flag is a file so it survives the play-mode domain reload.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoPlayProbe
    {
        private const string TriggerPath = "Temp/riptide_play.txt";
        private const string PendingPath = "Temp/riptide_play_pending.txt";
        private const string ResultPath = "Temp/riptide_play_result.txt";
        private const string CreaturesTrigger = "Temp/riptide_creatures.txt";
        private const string CreaturesOut = "Temp/riptide_creatures.png";

        private static readonly List<string> Errors = new List<string>();
        private static double probeAtTime = -1;
        private static double nextPollTime;

        static AutoPlayProbe()
        {
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Poll;
            if (File.Exists(PendingPath) && EditorApplication.isPlaying)
            {
                probeAtTime = EditorApplication.timeSinceStartup + 3.0;
            }
        }

        /// <summary>Renders the 8 procedural creature sprites into a labelled grid
        /// PNG (Temp/riptide_creatures.png) so they can be eyeballed without
        /// navigating to the Tidepool. Each tinted by its Palette.CreatureColor.</summary>
        private static void ExportCreatureContactSheet()
        {
            const int cols = 4;
            const int rows = 2;
            const int tile = 160;
            const int pad = 16;
            int w = cols * tile + (cols + 1) * pad;
            int h = rows * tile + (rows + 1) * pad;
            var sheet = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color bg = ThemeRuntime.Color("bg.abyss");
            var fill = new Color[w * h];
            for (int i = 0; i < fill.Length; i++)
            {
                fill[i] = bg;
            }

            sheet.SetPixels(fill);

            for (int id = 0; id < 8; id++)
            {
                int cx0 = pad + (id % cols) * (tile + pad);
                int cy0 = pad + (1 - id / cols) * (tile + pad); // row 0 at top
                Sprite sprite = CreatureSprites.For(id);
                Texture2D src = sprite.texture;
                Color tint = Palette.CreatureColor((byte)id);
                for (int y = 0; y < tile; y++)
                {
                    for (int x = 0; x < tile; x++)
                    {
                        float u = (float)x / tile;
                        float v = (float)y / tile;
                        Color s = src.GetPixelBilinear(u, v);
                        if (s.a > 0.01f)
                        {
                            Color c = new Color(s.r * tint.r, s.g * tint.g, s.b * tint.b, 1f);
                            sheet.SetPixel(cx0 + x, cy0 + y, Color.Lerp(bg, c, s.a));
                        }
                    }
                }
            }

            sheet.Apply();
            File.WriteAllBytes(CreaturesOut, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            File.WriteAllText(ResultPath, $"CREATURES exported {CreaturesOut}\n");
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception) && Errors.Count < 12
                && File.Exists(PendingPath))
            {
                Errors.Add($"{type}: {condition}");
            }
        }

        private static void Poll()
        {
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup < nextPollTime || EditorApplication.isCompiling)
                {
                    return;
                }

                nextPollTime = EditorApplication.timeSinceStartup + 2.0;

                if (File.Exists(CreaturesTrigger))
                {
                    File.Delete(CreaturesTrigger);
                    ExportCreatureContactSheet();
                    return;
                }

                if (!File.Exists(TriggerPath))
                {
                    return;
                }

                File.Delete(TriggerPath);
                File.WriteAllText(PendingPath, "probe");
                File.WriteAllText(ResultPath, "ENTERING PLAY\n");
                Errors.Clear();

                // Audit follow-up: an AAB build can leave an empty Untitled scene
                // open; AutoBoot only fires in SampleScene, so Play showed Unity's
                // default blue void ("the game is blue screened"). Force the scene.
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SampleScene")
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        "Assets/Scenes/SampleScene.unity");
                }

                ForcePortraitGameView();
                EditorApplication.isPlaying = true;
                return;
            }

            // While playing: re-dump state on demand (input debugging).
            if (File.Exists(StateTriggerPath))
            {
                File.Delete(StateTriggerPath);
                WriteReport();
                return;
            }

            if (!File.Exists(PendingPath))
            {
                return;
            }

            if (probeAtTime < 0)
            {
                probeAtTime = EditorApplication.timeSinceStartup + 3.0;
                return;
            }

            if (EditorApplication.timeSinceStartup < probeAtTime)
            {
                return;
            }

            File.Delete(PendingPath);
            WriteReport();
        }

        private const string StateTriggerPath = "Temp/riptide_uistate.txt";

        /// <summary>
        /// The game is portrait 1080×2400; a Free Aspect landscape Game view shows
        /// it at ~40% scale adrift in space (the "this is ugly" screenshot). Adds
        /// and selects a fixed portrait size via the internal GameView API —
        /// reflection, so failures just log and leave the aspect alone.
        /// </summary>
        private static void ForcePortraitGameView()
        {
            try
            {
                // The Device Simulator tab renders frames but routes no input
                // unless activated — with it selected, the game LOOKS dead
                // (Nick's session). Close simulators; the Game view is the
                // one honest play target.
                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    if (window != null && window.GetType().FullName!.Contains("SimulatorWindow"))
                    {
                        window.Close();
                    }
                }

                var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
                var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object instance = singletonType.GetProperty("instance")!.GetValue(null);
                object group = sizesType.GetMethod("GetGroup")!.Invoke(instance,
                    new object[] { (int)instance.GetType().GetProperty("currentGroupType")!.GetValue(instance) });

                var getTotal = group.GetType().GetMethod("GetTotalCount")!;
                var getSize = group.GetType().GetMethod("GetGameViewSize")!;
                int index = -1;
                int total = (int)getTotal.Invoke(group, null);
                for (int i = 0; i < total; i++)
                {
                    object size = getSize.Invoke(group, new object[] { i });
                    string text = (string)size.GetType().GetProperty("baseText")!.GetValue(size);
                    if (text == "Riptide Portrait")
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    var sizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
                    var enumType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");
                    object newSize = System.Activator.CreateInstance(sizeType,
                        System.Enum.ToObject(enumType, 1 /* FixedResolution */), 1080, 2340, "Riptide Portrait");
                    group.GetType().GetMethod("AddCustomSize")!.Invoke(group, new[] { newSize });
                    index = (int)getTotal.Invoke(group, null) - 1;
                }

                var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
                gameViewType.GetProperty("selectedSizeIndex",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic)!
                    .SetValue(gameView, index);

                // "Play Unfocused" starves the Input System of pointer events in
                // some Unity 6 configurations — force focused play.
                var behaviorProp = gameViewType.GetProperty("enterPlayModeBehavior",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);
                if (behaviorProp != null)
                {
                    object focused = System.Enum.Parse(behaviorProp.PropertyType, "PlayFocused");
                    behaviorProp.SetValue(gameView, focused);
                }

                gameView.Focus();
                Debug.Log($"AutoPlayProbe: game view portrait (index {index}), PlayFocused");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"AutoPlayProbe: could not set portrait game view ({ex.Message}) — pick 1080x2340 manually.");
            }
        }

        private static void WriteReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"PROBE scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} playing={Application.isPlaying}");

            var manager = Object.FindFirstObjectByType<ScreenManager>(FindObjectsInactive.Include);
            sb.AppendLine($"screenManager={(manager != null ? "OK" : "MISSING")}");
            if (manager != null)
            {
                sb.AppendLine($"flowScreen={manager.Flow.Screen} stackTop={manager.Stack.TopId ?? "(none)"} ageGate={manager.AgeGateOpen}");
            }

            // Input pipeline state — the dead-buttons investigation.
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            sb.AppendLine($"eventSystem={(eventSystem != null ? eventSystem.name : "MISSING")} module={(eventSystem != null && eventSystem.currentInputModule != null ? eventSystem.currentInputModule.GetType().Name : "none-active")}");
            var module = Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (module != null)
            {
                sb.AppendLine($"moduleEnabled={module.isActiveAndEnabled} actions={(module.actionsAsset != null ? module.actionsAsset.name : "NULL")} "
                    + $"pointEnabled={module.point?.action?.enabled} clickEnabled={module.leftClick?.action?.enabled}");
            }
            else
            {
                sb.AppendLine("inputModule=MISSING");
            }

            var mouse = UnityEngine.InputSystem.Mouse.current;
            sb.AppendLine(mouse != null
                ? $"mouseDevice=OK pos={mouse.position.ReadValue()} press={mouse.leftButton.isPressed}"
                : "mouseDevice=MISSING");
            sb.AppendLine($"appFocused={Application.isFocused}");

            Camera cam = Camera.main;
            sb.AppendLine(cam != null
                ? $"camera bg={ColorUtility.ToHtmlStringRGB(cam.backgroundColor)} ortho={cam.orthographic} clear={cam.clearFlags}"
                : "camera=MISSING");

            int canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length;
            int texts = Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            sb.AppendLine($"canvases={canvases} tmpTexts={texts}");

            sb.AppendLine(Errors.Count == 0 ? "errors=0" : $"errors={Errors.Count}");
            foreach (string error in Errors)
            {
                sb.AppendLine($"  {error}");
            }

            sb.AppendLine("PROBE DONE (left in play mode)");
            File.WriteAllText(ResultPath, sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}
