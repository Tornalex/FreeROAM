using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectSpear.Phase0
{
    /// <summary>
    /// Shared helpers for the Phase 0 golden-value tests.
    /// These tests are read-only: they load existing assets and prefabs but never
    /// write to them or to any serialized project configuration.
    /// </summary>
    internal static class BaselineTestSupport
    {
        public const float Tolerance = 0.0001f;

        public static T LoadAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, "Baseline asset is missing: " + path);
            return asset;
        }

        public static GameObject LoadPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, "Baseline prefab is missing: " + path);
            return prefab;
        }

        public static T PrefabComponent<T>(string prefabPath) where T : Component
        {
            GameObject prefab = LoadPrefab(prefabPath);
            T component = prefab.GetComponent<T>();
            Assert.IsNotNull(
                component,
                "Component " + typeof(T).Name + " is missing on " + prefabPath);
            return component;
        }

        public static T PrefabChildComponent<T>(string prefabPath) where T : Component
        {
            GameObject prefab = LoadPrefab(prefabPath);
            T component = prefab.GetComponentInChildren<T>(true);
            Assert.IsNotNull(
                component,
                "Child component " + typeof(T).Name + " is missing on " + prefabPath);
            return component;
        }

        public static float Float(Component component, string fieldName)
        {
            return FindProperty(component, fieldName).floatValue;
        }

        public static int Int(Component component, string fieldName)
        {
            return FindProperty(component, fieldName).intValue;
        }

        public static bool Bool(Component component, string fieldName)
        {
            return FindProperty(component, fieldName).boolValue;
        }

        public static string Text(Component component, string fieldName)
        {
            return FindProperty(component, fieldName).stringValue;
        }

        public static Vector2 Vector2Value(Component component, string fieldName)
        {
            return FindProperty(component, fieldName).vector2Value;
        }

        public static Color ColorValue(Component component, string fieldName)
        {
            return FindProperty(component, fieldName).colorValue;
        }

        public static AnimationCurve AnimationCurveValue(Component component, string fieldName)
        {
            return FindProperty(component, fieldName).animationCurveValue;
        }

        public static Object ObjectReference(Component component, string fieldName)
        {
            return FindProperty(component, fieldName).objectReferenceValue;
        }

        private static SerializedProperty FindProperty(Component component, string fieldName)
        {
            SerializedObject serializedObject = new SerializedObject(component);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            Assert.IsNotNull(
                property,
                "Serialized field '" + fieldName + "' was not found on " +
                component.GetType().Name + ".");
            return property;
        }

        public static void AssertClose(float expected, float actual, string label)
        {
            Assert.That(
                actual,
                Is.EqualTo(expected).Within(Tolerance),
                label + " expected " + expected + " but was " + actual + ".");
        }

        public static void AssertVector2(Vector2 expected, Vector2 actual, string label)
        {
            AssertClose(expected.x, actual.x, label + ".x");
            AssertClose(expected.y, actual.y, label + ".y");
        }

        public static void AssertColor(Color expected, Color actual, string label)
        {
            AssertClose(expected.r, actual.r, label + ".r");
            AssertClose(expected.g, actual.g, label + ".g");
            AssertClose(expected.b, actual.b, label + ".b");
            AssertClose(expected.a, actual.a, label + ".a");
        }

        public static void AssertReference(Component component, string fieldName, Object expected)
        {
            Object actual = ObjectReference(component, fieldName);
            Assert.AreSame(
                expected,
                actual,
                "Serialized reference '" + fieldName + "' on " +
                component.GetType().Name + " did not match the baseline asset.");
        }
    }
}
