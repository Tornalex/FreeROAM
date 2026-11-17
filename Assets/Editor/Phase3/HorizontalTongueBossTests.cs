using NUnit.Framework;
using SpearWander.Boss;
using UnityEditor;
using UnityEngine;

namespace ProjectSpear.Phase3
{
    /// <summary>
    /// Focused authoring coverage for the horizontal tongue sweep. Read-only:
    /// these tests load the prefab and assets but never save them.
    /// </summary>
    [TestFixture]
    public class HorizontalTongueBossTests
    {
        private const string BossPrefabPath =
            "Assets/Prefabs/Enemies/FreeRoam Boss.prefab";
        private const string HorizontalDataPath =
            "Assets/Data/Boss Data/Frog/Attacks/Horizontal Tongue Attack Data.asset";
        private const string ProfilePath =
            "Assets/Data/Boss Data/Frog/Frog Boss AI Data.asset";

        [Test]
        public void PrefabOwnsAConfiguredHorizontalTongueAttack()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            Assert.IsNotNull(prefab, "Boss prefab missing: " + BossPrefabPath);

            FreeRoamBoss boss = prefab.GetComponent<FreeRoamBoss>();
            Assert.IsNotNull(boss, "FreeRoamBoss missing from the boss prefab.");

            SerializedObject serializedBoss = new SerializedObject(boss);
            serializedBoss.Update();

            SerializedProperty originProperty =
                serializedBoss.FindProperty("horizontalTongueOrigin");
            SerializedProperty dataProperty =
                serializedBoss.FindProperty("horizontalTongueAttackData");

            Assert.IsNotNull(originProperty, "horizontalTongueOrigin is not serialized.");
            Assert.IsNotNull(dataProperty, "horizontalTongueAttackData is not serialized.");

            Transform origin = originProperty.objectReferenceValue as Transform;
            HorizontalTongueAttackData data =
                dataProperty.objectReferenceValue as HorizontalTongueAttackData;

            Assert.IsNotNull(origin, "horizontalTongueOrigin is not assigned.");
            Assert.IsNotNull(data, "horizontalTongueAttackData is not assigned.");
            Assert.IsTrue(
                origin.IsChildOf(boss.transform),
                "horizontalTongueOrigin must be a child of the boss.");
            Assert.AreEqual("Horizontal Tongue Origin", origin.name);

            Transform existingTongueOrigin =
                serializedBoss.FindProperty("tongueOrigin").objectReferenceValue as Transform;
            Assert.IsNotNull(existingTongueOrigin, "The standard tongue origin was lost.");
            Assert.Less(
                origin.position.y,
                existingTongueOrigin.position.y,
                "The horizontal origin must sit below the standard elevated tongue origin.");
        }

        [Test]
        public void HorizontalTongueDataAndWeightUseDocumentedDefaults()
        {
            HorizontalTongueAttackData data =
                AssetDatabase.LoadAssetAtPath<HorizontalTongueAttackData>(HorizontalDataPath);
            Assert.IsNotNull(data, "Horizontal tongue data missing: " + HorizontalDataPath);

            Assert.That(data.Damage, Is.EqualTo(2));
            Assert.That(data.Cooldown, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(data.WindupTime, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(data.HeightOffset, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(data.Speed, Is.EqualTo(24f).Within(0.0001f));
            Assert.That(data.MaxExtensionTime, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(data.RecoveryTime, Is.EqualTo(0.35f).Within(0.0001f));

            BossAIProfile profile =
                AssetDatabase.LoadAssetAtPath<BossAIProfile>(ProfilePath);
            Assert.IsNotNull(profile, "Boss AI profile missing: " + ProfilePath);
            Assert.That(profile.HorizontalTongueWeight, Is.EqualTo(2f).Within(0.0001f));
        }
    }
}
