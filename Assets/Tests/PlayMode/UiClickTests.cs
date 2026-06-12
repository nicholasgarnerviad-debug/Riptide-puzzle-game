using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Riptide.Game;
using Riptide.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// Regression net for the day-one human bug: every button was dead because
    /// the runtime-created input module had no actions, and no test ever CLICKED.
    /// These tests assert the module is armed and drive real EventSystem raycasts
    /// + click events through the gate and Home — proving nothing blocks the
    /// buttons and the handlers actually fire.
    /// </summary>
    public sealed class UiClickTests
    {
        private static void WipeMeta()
        {
            foreach (string key in new[]
            {
                "riptide.voyage", "riptide.streak", "riptide.endless.best",
                "riptide.daily.attemptDay", "riptide.daily.retryUsed",
            })
            {
                PlayerPrefs.DeleteKey(key);
            }

            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "riptide_save.json");
            if (System.IO.File.Exists(savePath))
            {
                System.IO.File.Delete(savePath);
            }
        }

        [UnityTest]
        public IEnumerator InputModule_IsArmed_GateDivesIntoL1_AndHomeButtonsClick()
        {
            WipeMeta(); // virgin profile: the gate's Continue must dive into L1
            PlayerPrefs.DeleteKey(ConsentAgeGate.BirthYearKey); // force the gate
            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            yield return null;
            yield return null;

            // The dead-buttons regression: a module with no actions routes nothing.
            var module = Object.FindFirstObjectByType<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null, "input module exists");
            Assert.That(module!.actionsAsset, Is.Not.Null, "module has actions (AssignDefaultActions)");
            Assert.That(module.point?.action?.enabled, Is.True, "pointer action enabled");

            // Age gate: Continue must be reachable by raycast, close the gate, and
            // (ROADMAP M1) launch a virgin profile straight into z1-l1.
            Assert.That(screens.AgeGateOpen, Is.True, "first-run gate up");
            Click("confirm");
            Assert.That(screens.AgeGateOpen, Is.False, "Continue closes the gate");
            yield return null;
            Assert.That(flow.Screen, Is.EqualTo(FlowScreen.Playing), "M1: virgin profile dives into L1");
            Assert.That(flow.Mode, Is.EqualTo(GameMode.Voyage));

            // Back out to Home, wait out the entrance transition, then the voyage
            // hero card must navigate on a raycast click.
            flow.GoTo(FlowScreen.Home);
            float wait = ThemeRuntime.Seconds("t.screen") + 0.25f;
            float until = Time.realtimeSinceStartup + wait;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Click("voyage");
            Assert.That(flow.Screen, Is.EqualTo(FlowScreen.ZoneMap), "voyage card navigates to the map");

            PlayerPrefs.DeleteKey(ConsentAgeGate.BirthYearKey);
            WipeMeta();
            Object.Destroy(screens.transform.parent != null ? screens.transform.parent.gameObject : screens.gameObject);
        }

        /// <summary>Raycasts at the named button's center and clicks what the UI actually hits.</summary>
        private static void Click(string buttonName)
        {
            GameObject target = FindActive(buttonName);
            Assert.That(target, Is.Not.Null, $"button '{buttonName}' present and active");

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, target!.transform.position),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, $"raycast at '{buttonName}' hits UI");

            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.Not.Null,
                $"top hit '{hits[0].gameObject.name}' resolves to a click handler — nothing blocks '{buttonName}'");
            Assert.That(handler!.transform == target.transform
                    || handler.transform.IsChildOf(target.transform)
                    || target.transform.IsChildOf(handler.transform),
                Is.True, $"the handler under the pointer IS '{buttonName}', not a covering element");

            pointer.pointerPress = handler;
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static GameObject FindActive(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == name && t.gameObject.activeInHierarchy)
                {
                    return t.gameObject;
                }
            }

            return null!;
        }
    }
}
