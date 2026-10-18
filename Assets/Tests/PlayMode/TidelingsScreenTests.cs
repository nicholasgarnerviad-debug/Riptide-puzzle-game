using System.Collections;
using System.Linq;
using NUnit.Framework;
using Riptide.Core;
using Riptide.Game;
using Riptide.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// GDD §16.6 Tidelings screens through the real app: the tab's Journal by tier,
    /// the Conch's Call ×1 / Again / ×10 into the reveal (eggs spent, Tidelings owned,
    /// the ten-Call's Rare-or-better), back closing the top moment, and growing up from
    /// a card into the celebration with the Journal's line reward.
    /// </summary>
    public sealed class TidelingsScreenTests
    {
        private static readonly long Today = CivilDate.ToEpochDays(2026, 10, 14);

        private static IEnumerator Boot(System.Action<GameFlow, ScreenManager> ready)
        {
            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "riptide_save.json");
            if (System.IO.File.Exists(savePath))
            {
                System.IO.File.Delete(savePath);
            }

            (GameFlow flow, ScreenManager screens) = GameBootstrap.CreateApp(instantAnimations: true);
            flow.Meta.TodayEpochDay = () => Today;
            yield return null;
            ready(flow, screens);
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                yield return null;
            }
        }

        private static void Teardown(ScreenManager screens)
        {
            while (Overlay.DismissTop())
            {
            }

            Object.Destroy(screens.transform.parent != null ? screens.transform.parent.gameObject : screens.gameObject);
        }

        private static Button Child(Component host, string path)
        {
            Transform? found = host.transform.Find(path);
            Assert.That(found, Is.Not.Null, $"{host.name}/{path} exists");
            return found!.GetComponent<Button>();
        }

        [UnityTest]
        public IEnumerator Journal_ShowsATierAtATime_AndUnmetAsSilhouettes()
        {
            GameFlow flow = null!;
            ScreenManager screens = null!;
            yield return Boot((f, s) => { flow = f; screens = s; });

            flow.GoTo(FlowScreen.Tidepool);
            yield return null;
            var tab = Object.FindFirstObjectByType<TidepoolScreen>();
            Assert.That(tab!.JournalTier, Is.EqualTo(PalRarity.Common));
            Assert.That(GameObject.Find("card_crab"), Is.Not.Null, "Commons show first");

            Child(GameObject.Find("journal")!.transform, "tierEpic").onClick.Invoke();
            yield return null;
            Assert.That(tab.JournalTier, Is.EqualTo(PalRarity.Epic));
            Assert.That(GameObject.Find("card_crab"), Is.Null, "other tiers step aside");
            GameObject axolotl = GameObject.Find("card_axolotl")!;
            Assert.That(axolotl, Is.Not.Null);
            Assert.That(axolotl.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text == "???"), Is.True,
                "an unmet Tideling is a silhouette with ???");
            Teardown(screens);
        }

        [UnityTest]
        public IEnumerator Conch_CallsOneAgainAndTen_IntoTheReveal()
        {
            GameFlow flow = null!;
            ScreenManager screens = null!;
            yield return Boot((f, s) => { flow = f; screens = s; });

            flow.Meta.Save.Pals.Eggs = 60;
            flow.GoTo(FlowScreen.Tidepool);
            yield return null;
            Child(GameObject.Find("dioramaFrame")!.transform, "call").onClick.Invoke();
            yield return null;
            var conch = Object.FindFirstObjectByType<ConchScreen>();
            Assert.That(conch != null && conch.IsOpen, Is.True, "CALL opens the Conch");

            Child(conch!, "content/callOne").onClick.Invoke();
            yield return null;
            Assert.That(flow.Eggs, Is.EqualTo(60 - flow.Pals.Call.Cost));
            var reveal = Object.FindFirstObjectByType<CallReveal>();
            Assert.That(reveal != null && reveal.IsOpen && !reveal.ShowingTen, Is.True, "one Call, one reveal");
            Assert.That(reveal!.Results.Count, Is.EqualTo(1));
            Assert.That(flow.Tideling(reveal.Results[0].Line).Owned, Is.True, "the answer is yours");

            Child(reveal, "content/single/again").onClick.Invoke();
            Assert.That(flow.Eggs, Is.EqualTo(60 - 2 * flow.Pals.Call.Cost), "Again calls once more");
            Assert.That(Overlay.DismissTop(), Is.True, "back closes the reveal first");
            yield return Frames(30);
            Assert.That(conch.IsOpen && !reveal.IsOpen, Is.True, "…leaving the Conch");

            Child(conch, "content/callTen").onClick.Invoke();
            yield return null;
            Assert.That(flow.Eggs, Is.EqualTo(60 - 2 * flow.Pals.Call.Cost - flow.Pals.Call.TenCost));
            Assert.That(reveal.ShowingTen && reveal.Results.Count == 10, Is.True, "ten arrive at once");
            Assert.That(reveal.Results.Any(r => r.Tier >= PalRarity.Rare), Is.True, "a ten-Call holds a Rare or better");
            Assert.That(Child(conch, "content/callTen").interactable, Is.False, "short of eggs, ×10 rests");
            Teardown(screens);
        }

        [UnityTest]
        public IEnumerator GrowUp_FromTheCard_Celebrates_AndPaysTheJournalLine()
        {
            GameFlow flow = null!;
            ScreenManager screens = null!;
            yield return Boot((f, s) => { flow = f; screens = s; });

            int crab = flow.Pals.LineForSpecies(0);
            PalSave pals = flow.Meta.Save.Pals;
            pals.EnsureLines(flow.Pals.Lines.Count);
            pals.Stage[crab] = 2;
            pals.Bond[crab] = 200;
            pals.Pod[0] = crab;
            long coins = flow.Meta.Coins;
            flow.GoTo(FlowScreen.Tidepool);
            yield return null;

            GameObject.Find("card_crab")!.GetComponent<Button>().onClick.Invoke();
            yield return null;
            var sheet = Object.FindFirstObjectByType<TidelingSheet>();
            Button grow = Child(sheet!, "content/info/action");
            Assert.That(grow.gameObject.activeSelf, Is.True, "Grow up! lights up when bond is there");
            grow.onClick.Invoke();
            yield return null;

            Assert.That(flow.Tideling(crab).Stage, Is.EqualTo(3), "Grand");
            Assert.That(flow.Meta.Coins, Is.EqualTo(coins + flow.Pals.LineCompleteCoins), "all three forms: the line pays");
            var party = Object.FindFirstObjectByType<GrowUpCelebration>();
            Assert.That(party != null && party.IsOpen && party.Line == crab, Is.True, "the celebration plays");
            Assert.That(party!.transform.Find("content/lineDone").gameObject.activeSelf, Is.True);
            Assert.That(grow.gameObject.activeSelf, Is.False, "nothing left to grow");
            Teardown(screens);
        }
    }
}
