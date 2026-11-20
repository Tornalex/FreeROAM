using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SpearWander.PlayModeTests
{
    public class CameraManagerPlayModeTests
    {
        [Test]
        public void SelectionKeepsGameplayPrioritiesUniqueAndAppliesBoundsAndFixedAnchor()
        {
            GameObject explorationObject = null;
            GameObject corridorObject = null;
            GameObject fixedObject = null;
            GameObject managerObject = null;
            GameObject roomObject = null;
            GameObject anchorObject = null;
            GameObject dialogueObject = null;

            try
            {
                Component exploration = CreateGameplayCamera(
                    "Exploration Camera", out explorationObject);
                Component corridor = CreateGameplayCamera(
                    "Corridor Camera", out corridorObject);
                Component fixedCamera = CreateGameplayCamera(
                    "Fixed Camera", out fixedObject);

                Type managerType = ReflectionTestUtilities.ResolveRuntimeType("CameraManager");
                managerObject = new GameObject("Test Camera Manager");
                managerObject.SetActive(false);
                Component manager = managerObject.AddComponent(managerType);
                ReflectionTestUtilities.SetInstanceField(manager, "explorationCamera", exploration);
                ReflectionTestUtilities.SetInstanceField(manager, "corridorCamera", corridor);
                ReflectionTestUtilities.SetInstanceField(manager, "fixedCamera", fixedCamera);
                managerObject.SetActive(true);

                Type settingsType =
                    ReflectionTestUtilities.ResolveRuntimeType("RoomCameraSettings");
                roomObject = new GameObject("Test Camera Bounds");
                roomObject.AddComponent<PolygonCollider2D>();
                Component settings = roomObject.AddComponent(settingsType);
                Type cameraType =
                    ReflectionTestUtilities.ResolveRuntimeType("RoomCameraType");
                ReflectionTestUtilities.SetInstanceField(
                    settings,
                    "cameraType",
                    Enum.Parse(cameraType, "Fixed"));

                anchorObject = new GameObject("Test Fixed Camera Anchor");
                anchorObject.transform.SetPositionAndRotation(
                    new Vector3(14f, -3f, -30f),
                    Quaternion.Euler(0f, 0f, 5f));
                ReflectionTestUtilities.SetInstanceField(
                    settings,
                    "fixedCameraAnchor",
                    anchorObject.transform);

                Type dialogueCameraType = FindLoadedType("Unity.Cinemachine.CinemachineCamera");
                dialogueObject = new GameObject("Independent Dialogue Camera");
                Component dialogueCamera = dialogueObject.AddComponent(dialogueCameraType);
                SetPriority(dialogueCamera, 20);

                Assert.That(
                    ReflectionTestUtilities.GetProperty(manager, "ActiveCameraType").ToString(),
                    Is.EqualTo("Exploration"),
                    "Exploration must be selected at startup.");
                AssertUniqueGameplayPriorities(exploration, corridor, fixedCamera);

                Assert.That(
                    ReflectionTestUtilities.InvokeInstance<bool>(
                        manager, "SelectCamera", Enum.Parse(cameraType, "Corridor")),
                    Is.True);
                Assert.That(
                    ReflectionTestUtilities.GetProperty(manager, "ActiveCameraType").ToString(),
                    Is.EqualTo("Corridor"));
                AssertUniqueGameplayPriorities(exploration, corridor, fixedCamera);

                Assert.That(
                    ReflectionTestUtilities.InvokeInstance<bool>(
                        manager, "SelectCamera", Enum.Parse(cameraType, "Fixed")),
                    Is.True);
                Assert.That(
                    ReflectionTestUtilities.GetProperty(manager, "ActiveCameraType").ToString(),
                    Is.EqualTo("Fixed"));
                AssertUniqueGameplayPriorities(exploration, corridor, fixedCamera);

                Assert.That(
                    ReflectionTestUtilities.InvokeInstance<bool>(
                        manager, "ApplyRoomSettings", settings),
                    Is.True);
                Assert.That(
                    ReflectionTestUtilities.GetProperty(manager, "ActiveCameraType").ToString(),
                    Is.EqualTo("Fixed"));
                AssertUniqueGameplayPriorities(exploration, corridor, fixedCamera);

                Assert.That(
                    fixedObject.transform.position,
                    Is.EqualTo(anchorObject.transform.position));
                Assert.That(
                    fixedObject.transform.rotation,
                    Is.EqualTo(anchorObject.transform.rotation));

                Collider2D expectedBounds = roomObject.GetComponent<Collider2D>();
                Assert.That(GetConfinerBounds(exploration), Is.SameAs(expectedBounds));
                Assert.That(GetConfinerBounds(corridor), Is.SameAs(expectedBounds));
                Assert.That(GetConfinerBounds(fixedCamera), Is.SameAs(expectedBounds));

                Vector3 fixedPosition = fixedObject.transform.position;
                ReflectionTestUtilities.InvokeInstance(
                    manager,
                    "ForceFollowingCameraPosition",
                    new Vector3(-50f, 100f, 0f));
                Assert.That(fixedObject.transform.position, Is.EqualTo(fixedPosition));

                Assert.That(GetPriority(dialogueCamera), Is.EqualTo(20),
                    "Gameplay camera selection must not modify Dialogue Camera priority.");

                Assert.That(
                    ReflectionTestUtilities.InvokeInstance<bool>(
                        manager, "SelectCamera", Enum.Parse(cameraType, "Exploration")),
                    Is.True);
                AssertUniqueGameplayPriorities(exploration, corridor, fixedCamera);
            }
            finally
            {
                DestroyIfAlive(dialogueObject);
                DestroyIfAlive(anchorObject);
                DestroyIfAlive(roomObject);
                DestroyIfAlive(managerObject);
                DestroyIfAlive(fixedObject);
                DestroyIfAlive(corridorObject);
                DestroyIfAlive(explorationObject);
            }
        }

        private static Component CreateGameplayCamera(string name, out GameObject cameraObject)
        {
            cameraObject = new GameObject(name);
            Type cameraType = FindLoadedType("Unity.Cinemachine.CinemachineCamera");
            Component camera = cameraObject.AddComponent(cameraType);
            cameraObject.AddComponent(FindLoadedType(
                "Unity.Cinemachine.CinemachineConfiner2D"));
            return camera;
        }

        private static Collider2D GetConfinerBounds(Component camera)
        {
            Component confiner = camera.GetComponent(
                FindLoadedType("Unity.Cinemachine.CinemachineConfiner2D"));
            return ReflectionTestUtilities.GetInstanceField<Collider2D>(
                confiner, "BoundingShape2D");
        }

        private static int GetPriority(Component camera)
        {
            object priority = ReflectionTestUtilities.GetInstanceField(camera, "Priority");
            PropertyInfo value = priority.GetType().GetProperty("Value");
            return (int)value.GetValue(priority);
        }

        private static void SetPriority(Component camera, int value)
        {
            FieldInfo field = camera.GetType().GetField("Priority");
            object priority = field.GetValue(camera);
            PropertyInfo property = priority.GetType().GetProperty("Value");
            property.SetValue(priority, value);
            field.SetValue(camera, priority);
        }

        private static void AssertUniqueGameplayPriorities(params Component[] cameras)
        {
            int[] priorities = cameras.Select(GetPriority).ToArray();
            Assert.That(priorities.Distinct().Count(), Is.EqualTo(cameras.Length));
            Assert.That(priorities.Max(), Is.GreaterThan(priorities.Min()));
        }

        private static Type FindLoadedType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }

            throw new AssertionException($"Type '{fullName}' is not loaded.");
        }

        private static void DestroyIfAlive(GameObject gameObject)
        {
            if (gameObject != null)
                UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
}
