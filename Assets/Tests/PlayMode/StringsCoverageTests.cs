using System.Collections.Generic;
using NUnit.Framework;
using Riptide.Core;
using Riptide.Game;
using Riptide.UI;

namespace Riptide.PlayMode.Tests
{
    /// <summary>
    /// UI spec 5-UI-a ✅: every key the UI layer renders exists in strings.json
    /// (no KeyNotFound at runtime), and the registry itself stays duplicate-free.
    /// </summary>
    public sealed class StringsCoverageTests
    {
        [Test]
        public void EveryUiKey_ResolvesInStringsJson()
        {
            StringTable strings = RuntimeContent.LoadStrings();
            var missing = new List<string>();
            foreach (string key in UiStringKeys.All)
            {
                if (!strings.Has(key))
                {
                    missing.Add(key);
                }
            }

            Assert.That(missing, Is.Empty, $"strings.json is missing: {string.Join(", ", missing)}");
        }

        [Test]
        public void TheRegistry_HasNoDuplicates()
        {
            var seen = new HashSet<string>();
            foreach (string key in UiStringKeys.All)
            {
                Assert.That(seen.Add(key), Is.True, $"duplicate registry entry '{key}'");
            }

            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(70), "registry covers the whole UI surface");
        }
    }
}
