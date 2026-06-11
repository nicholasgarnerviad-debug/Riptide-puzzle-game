using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Riptide.EditorAutomation
{
    /// <summary>
    /// Headless-ish test driver for an open editor: drop a trigger file
    /// (Temp/riptide_run_tests.txt containing "EditMode" or "PlayMode"), refresh
    /// the editor (Ctrl+R), and results stream to Temp/riptide_test_results.txt.
    /// Exists because the MCP bridge is unreliable on this machine and batchmode
    /// cannot run while the editor holds the project (DECISIONS.md Phase 4).
    /// </summary>
    [InitializeOnLoad]
    public static class AutoTestRunner
    {
        private const string TriggerPath = "Temp/riptide_run_tests.txt";
        private const string ResultsPath = "Temp/riptide_test_results.txt";

        private static TestRunnerApi api = null!;
        private static double nextPollTime;

        static AutoTestRunner()
        {
            // Re-register callbacks after every domain reload, then poll the trigger
            // file from the editor loop — no reload needed to start a run.
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultWriter());
            EditorApplication.update += PollTrigger;
        }

        private const string RefreshTriggerPath = "Temp/riptide_refresh.txt";

        private static void PollTrigger()
        {
            if (EditorApplication.timeSinceStartup < nextPollTime || EditorApplication.isPlaying)
            {
                return;
            }

            nextPollTime = EditorApplication.timeSinceStartup + 2.0;

            // Focus-free asset refresh: external tooling drops this file instead of
            // needing window focus + Ctrl+R (Auto Refresh is disabled on this machine).
            if (File.Exists(RefreshTriggerPath))
            {
                File.Delete(RefreshTriggerPath);
                AssetDatabase.Refresh();
                return;
            }

            if (!File.Exists(TriggerPath))
            {
                return;
            }

            string modeText = File.ReadAllText(TriggerPath).Trim();
            File.Delete(TriggerPath);
            TestMode mode = modeText == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode;
            File.WriteAllText(ResultsPath, $"RUNNING {mode}\n");

            var filter = new Filter { testMode = mode };
            api.Execute(new ExecutionSettings(filter));
        }

        private sealed class ResultWriter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"RUNFINISHED status={result.TestStatus} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} duration={result.Duration:F1}s");
                AppendFailures(result, sb);
                File.AppendAllText(ResultsPath, sb.ToString());
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.IsSuite && result.TestStatus != TestStatus.Passed)
                {
                    File.AppendAllText(ResultsPath,
                        $"FAIL {result.Test.FullName}: {Truncate(result.Message)}\n");
                }
            }

            private static void AppendFailures(ITestResultAdaptor result, StringBuilder sb)
            {
                if (!result.Test.IsSuite && result.TestStatus == TestStatus.Failed)
                {
                    sb.AppendLine($"  failed: {result.Test.FullName}");
                }

                if (result.Children == null)
                {
                    return;
                }

                foreach (ITestResultAdaptor child in result.Children)
                {
                    AppendFailures(child, sb);
                }
            }

            private static string Truncate(string? text) =>
                string.IsNullOrEmpty(text) ? "" : (text!.Length > 300 ? text.Substring(0, 300) : text);
        }
    }
}
