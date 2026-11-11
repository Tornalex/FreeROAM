using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ProjectSpear.Phase0
{
    /// <summary>
    /// Baseline integrity checks for the prefabs that will be touched by the
    /// later data-driven phases. These tests only read the prefabs.
    /// </summary>
    [TestFixture]
    public class PrefabIntegrityBaselineTests
    {
        private static readonly string[] KeyPrefabPaths =
        {
            "Assets/Prefabs/Utilities/Player.prefab",
            "Assets/Prefabs/Utilities/SpearV2.prefab",
            "Assets/Prefabs/Enemies/Enemy.prefab",
            "Assets/Prefabs/Enemies/Flying Enemy.prefab",
            "Assets/Prefabs/Enemies/RecallEnemy.prefab",
            "Assets/Prefabs/Enemies/FreeRoam Boss.prefab",
            "Assets/Prefabs/Enemies/Spore Cloud.prefab",
            "Assets/Prefabs/Props/SpearPickup.prefab",
            "Assets/Prefabs/Props/Ability Pickup.prefab"
        };

        [Test]
        public void KeyPrefabsHaveNoMissingMonoBehaviourScripts()
        {
            List<string> missing = new List<string>();

            foreach (string prefabPath in KeyPrefabPaths)
            {
                GameObject prefab = BaselineTestSupport.LoadPrefab(prefabPath);
                Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);

                foreach (Transform child in transforms)
                {
                    MonoBehaviour[] behaviours = child.GetComponents<MonoBehaviour>();
                    for (int i = 0; i < behaviours.Length; i++)
                    {
                        if (behaviours[i] == null)
                        {
                            missing.Add(prefabPath + " :: " + BuildTransformPath(child));
                        }
                    }
                }
            }

            Assert.IsEmpty(
                missing,
                "Key prefabs contain missing MonoBehaviour scripts:\n" +
                string.Join("\n", missing));
        }

        private static string BuildTransformPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
