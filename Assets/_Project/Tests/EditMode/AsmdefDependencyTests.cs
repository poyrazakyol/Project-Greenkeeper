using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Guards TDD §2.2: the assembly dependency arrow is one-way
    /// (Core ← Pitch ← Tools ← Gameplay ← UI). Adding a reverse reference
    /// to any asmdef turns these tests red.
    /// Greenkeeper.Debug and editor/test assemblies are exempt by design.
    /// </summary>
    public class AsmdefDependencyTests
    {
        [Serializable]
        private class AsmdefJson
        {
            public string name;
            public string[] references;
        }

        private static readonly Dictionary<string, int> Rank = new Dictionary<string, int>
        {
            { "Greenkeeper.Core", 0 },
            { "Greenkeeper.Pitch", 1 },
            { "Greenkeeper.Tools", 2 },
            { "Greenkeeper.Gameplay", 3 },
            { "Greenkeeper.UI", 4 },
        };

        [Test]
        public void AllRankedAssembliesExist()
        {
            List<string> names = LoadProjectAsmdefs().Select(a => a.name).ToList();
            foreach (string expected in Rank.Keys)
            {
                Assert.That(names, Does.Contain(expected),
                    $"Assembly '{expected}' is missing from Assets/_Project (TDD §2.2).");
            }
        }

        [Test]
        public void RankedAssemblies_OnlyReferenceLowerRankedGreenkeeperAssemblies()
        {
            foreach (AsmdefJson asmdef in LoadProjectAsmdefs().Where(a => Rank.ContainsKey(a.name)))
            {
                foreach (string reference in asmdef.references ?? Array.Empty<string>())
                {
                    if (!reference.StartsWith("Greenkeeper.", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Assert.That(Rank.ContainsKey(reference), Is.True,
                        $"{asmdef.name} references '{reference}', which is not part of the " +
                        "ranked chain. Ranked assemblies may only depend on Core/Pitch/Tools/Gameplay/UI.");
                    Assert.That(Rank[reference], Is.LessThan(Rank[asmdef.name]),
                        $"{asmdef.name} must not reference {reference}: the dependency arrow " +
                        "is one-way, Core ← Pitch ← Tools ← Gameplay ← UI (TDD §2.2).");
                }
            }
        }

        private static List<AsmdefJson> LoadProjectAsmdefs()
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            return Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories)
                .Select(File.ReadAllText)
                .Select(JsonUtility.FromJson<AsmdefJson>)
                .ToList();
        }
    }
}
