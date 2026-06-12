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
                if (!File.Exists(TriggerPath))
                {
                    return;
                }

                File.Delete(TriggerPath);
                File.WriteAllText(PendingPath, "probe");
                File.WriteAllText(ResultPath, "ENTERING PLAY\n");
                Errors.Clear();
                ForcePortraitGameView();
                EditorApplication.isPlaying = true;
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
                Debug.Log($"AutoPlayProbe: game view set to Riptide Portrait (index {index})");
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
