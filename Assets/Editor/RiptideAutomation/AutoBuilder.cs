using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Riptide.EditorAutomation
{
    /// <summary>
    /// Contract 8D: file-triggered Android closed-testing build (same pattern as
    /// AutoTestRunner — drop "android" into Temp/riptide_build.txt, refresh).
    /// Configures player settings, generates + assigns the app icon, builds an
    /// AAB to Builds/, and writes the outcome to Temp/riptide_build_result.txt.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoBuilder
    {
        private const string TriggerPath = "Temp/riptide_build.txt";
        private const string ResultPath = "Temp/riptide_build_result.txt";
        private static double nextPollTime;

        static AutoBuilder()
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
            File.WriteAllText(ResultPath, "BUILDING\n");
            try
            {
                BuildAndroid();
            }
            catch (Exception ex)
            {
                File.AppendAllText(ResultPath, $"BUILDFAILED exception {ex.GetType().Name}: {ex.Message}\n");
            }
        }

        public static void BuildAndroid()
        {
            // ---- player identity & versions (DECISIONS.md P8) ----
            PlayerSettings.productName = "Riptide";
            PlayerSettings.companyName = "NickG";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.riptide.game");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            // Portrait only (GDD: portrait mobile).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Play requirements: IL2CPP + ARM64; minSdk 24; targetSdk Auto (= highest
            // installed — Nick confirms the current Play floor at upload).
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            ApplyGeneratedIcon();

            EditorUserBuildSettings.buildAppBundle = true;

            Directory.CreateDirectory("Builds");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = "Builds/riptide-closed-testing.aab",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            File.AppendAllText(ResultPath,
                $"BUILDRESULT {summary.result} size={summary.totalSize} errors={summary.totalErrors} " +
                $"warnings={summary.totalWarnings} time={summary.totalTime.TotalSeconds:F0}s " +
                $"output={summary.outputPath}\n");
        }

        /// <summary>The 8D icon hook: a generated wave glyph until branded art exists.</summary>
        private static void ApplyGeneratedIcon()
        {
            const string iconPath = "Assets/Icon/app_icon.png";
            Directory.CreateDirectory("Assets/Icon");
            if (!File.Exists(iconPath))
            {
                File.WriteAllBytes(iconPath, GenerateIconPng(432));
                AssetDatabase.ImportAsset(iconPath);
            }

            var importer = (TextureImporter?)AssetImporter.GetAtPath(iconPath);
            if (importer != null && importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                importer.SaveAndReimport();
            }

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Android, new[] { icon }, IconKind.Application);
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Application);
            }
        }

        private static byte[] GenerateIconPng(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var deep = new Color(0.04f, 0.055f, 0.08f);
            var cyan = new Color(0.24f, 0.9f, 0.88f);
            var teal = new Color(0.17f, 0.71f, 0.6f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    Color c = Color.Lerp(deep, new Color(0.05f, 0.10f, 0.16f), v);

                    // Two stylized wave bands.
                    float wave1 = 0.42f + 0.06f * Mathf.Sin(u * 9.4f + 0.8f);
                    float wave2 = 0.30f + 0.05f * Mathf.Sin(u * 7.1f + 2.6f);
                    if (Mathf.Abs(v - wave1) < 0.035f)
                    {
                        c = cyan;
                    }
                    else if (v < wave1 && v > wave2)
                    {
                        c = Color.Lerp(teal, deep, (wave1 - v) * 6f);
                    }
                    else if (Mathf.Abs(v - wave2) < 0.02f)
                    {
                        c = Color.Lerp(cyan, teal, 0.5f);
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return png;
        }
    }
}
