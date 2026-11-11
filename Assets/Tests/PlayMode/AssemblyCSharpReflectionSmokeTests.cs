using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Infrastructure smoke test for a dedicated PlayMode test assembly.
    /// Runtime scripts still compile into the predefined Assembly-CSharp, which
    /// an assembly definition cannot reference directly, so this test proves the
    /// assembly can be located and inspected through reflection instead.
    /// </summary>
    public class AssemblyCSharpReflectionSmokeTests
    {
        private static readonly string[] ExpectedTypeNames =
        {
            "GameSceneManager",
            "RoomTransition",
            "BaseEnemy",
            "SpearWander.Abilities.DeadEnemyTracker"
        };

        [Test]
        public void AssemblyCSharpExposesExpectedRuntimeTypes()
        {
            Assert.That(
                Application.isPlaying,
                Is.True,
                "This test must execute in PlayMode; Application.isPlaying was false.");

            Assembly assembly = FindAssemblyCSharp();
            Assert.That(
                assembly,
                Is.Not.Null,
                "The loaded assembly 'Assembly-CSharp' could not be found in the current AppDomain.");

            foreach (string typeName in ExpectedTypeNames)
            {
                Type type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);

                Assert.That(
                    type,
                    Is.Not.Null,
                    $"Type '{typeName}' was not found in assembly '{assembly.GetName().Name}'.");

                Assert.That(
                    type.Assembly,
                    Is.SameAs(assembly),
                    $"Type '{typeName}' resolved from a different assembly: '{type.Assembly.GetName().Name}'.");

                TestContext.WriteLine(
                    $"[PlayModeReflectionSmoke] {typeName} -> {type.FullName} [{type.Assembly.GetName().Name}]");
            }
        }

        private static Assembly FindAssemblyCSharp()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (string.Equals(
                        assemblies[i].GetName().Name,
                        "Assembly-CSharp",
                        StringComparison.Ordinal))
                {
                    return assemblies[i];
                }
            }

            return null;
        }
    }
}
