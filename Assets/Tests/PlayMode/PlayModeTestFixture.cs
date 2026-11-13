using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Reusable PlayMode test fixture. It isolates per-test state without
    /// altering production code: static tracking state is reset, scenes loaded
    /// by the fixture are unloaded again, and the active scene, timescale and
    /// any existing GameSceneManager are restored on cleanup.
    ///
    /// Cleanup is an IEnumerator because additive scene unloading is
    /// asynchronous and cannot complete inside a synchronous Dispose.
    /// </summary>
    public sealed class PlayModeTestFixture
    {
        private const string GameSceneManagerTypeName = "GameSceneManager";
        private const string DeadEnemyTrackerTypeName = "SpearWander.Abilities.DeadEnemyTracker";
        private const string EnemyRoomTrackingKeyTypeName = "EnemyRoomTrackingKey";

        private const string CurrentRoomSceneField = "_currentRoomScene";
        private const string CurrentRoomDefinitionField = "_currentRoomDefinition";
        private const string TargetDoorIdField = "_targetDoorID";
        private const string IsTransitioningField = "_isTransitioning";
        private const string ConnectedTransitionFaultInjectorField =
            "_connectedTransitionFaultInjector";
        private const string CatalogFailureScenesField = "CatalogFailureScenes";

        private readonly List<Scene> _ownedScenes = new List<Scene>();
        private readonly List<GameObject> _ownedGameObjects = new List<GameObject>();

        private PlayModeLogCapture _logCapture;
        private Scene _previousActiveScene;
        private float _previousTimeScale = 1f;

        private object _manager;
        private bool _managerSnapshotCaptured;
        private string _managerCurrentRoomScene;
        private object _managerCurrentRoomDefinition;
        private string _managerTargetDoorId;
        private bool _managerIsTransitioning;

        private bool _initialized;
        private bool _cleanedUp;

        public PlayModeLogCapture LogCapture => _logCapture;

        public Scene PreviousActiveScene => _previousActiveScene;

        /// <summary>
        /// Captures the state that must be restored, resets static tracking
        /// state, and leaves the test in a known starting condition.
        /// </summary>
        public void SetUp()
        {
            if (_initialized)
                throw new InvalidOperationException("PlayModeTestFixture.SetUp was called more than once.");
            if (!Application.isPlaying)
                throw new InvalidOperationException("PlayModeTestFixture can only be used in PlayMode.");

            _initialized = true;
            _cleanedUp = false;

            _previousActiveScene = SceneManager.GetActiveScene();
            _previousTimeScale = Time.timeScale;

            CaptureManagerSnapshot();
            ResetStaticState();

            if (HasLiveManager())
                SetManagerTransitioning(false);
        }

        /// <summary>
        /// Clears static state that survives between PlayMode tests because
        /// Domain Reload and Scene Reload are disabled.
        /// </summary>
        public static void ResetStaticState()
        {
            Type managerType = ReflectionTestUtilities.ResolveRuntimeType(GameSceneManagerTypeName);
            ReflectionTestUtilities.SetStaticField(
                managerType,
                ConnectedTransitionFaultInjectorField,
                null);

            Type deadEnemyTracker = ReflectionTestUtilities.ResolveRuntimeType(DeadEnemyTrackerTypeName);
            ReflectionTestUtilities.InvokeStatic(deadEnemyTracker, "ClearAll");

            Type trackingKey = ReflectionTestUtilities.ResolveRuntimeType(EnemyRoomTrackingKeyTypeName);
            object failureScenes = ReflectionTestUtilities.GetStaticField(
                trackingKey,
                CatalogFailureScenesField);

            if (!(failureScenes is ICollection<string> catalogFailureScenes))
            {
                throw new AssertionException(
                    $"'{EnemyRoomTrackingKeyTypeName}.{CatalogFailureScenesField}' was expected " +
                    "to implement ICollection<string>.");
            }

            catalogFailureScenes.Clear();
            Time.timeScale = 1f;
        }

        /// <summary>
        /// Loads an exact scene asset path additively and waits for completion.
        /// Only scenes loaded through this method are unloaded during cleanup.
        /// </summary>
        public IEnumerator LoadSceneAdditive(string scenePath, Action<Scene> onLoaded = null)
        {
            EnsureActive();
            ValidateScenePath(scenePath);

            if (IsSceneLoaded(scenePath))
            {
                throw new AssertionException(
                    $"Scene '{scenePath}' is already loaded; the fixture will not load it twice.");
            }

            AsyncOperation operation = null;
            Exception startException = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                startException = exception;
            }

            if (startException != null)
            {
                throw new AssertionException(
                    $"Could not start loading scene '{scenePath}': {startException.Message}");
            }

            if (operation == null)
            {
                throw new AssertionException(
                    $"Unity returned no load operation for scene '{scenePath}'. " +
                    "Confirm the scene path is exact and the scene is in Build Settings.");
            }

            while (!operation.isDone)
                yield return null;

            Scene loadedScene = GetLoadedScene(scenePath);
            if (!_ownedScenes.Contains(loadedScene))
                _ownedScenes.Add(loadedScene);

            onLoaded?.Invoke(loadedScene);
        }

        /// <summary>
        /// Takes ownership of a scene that was loaded outside the fixture, such
        /// as a destination loaded by the production transition. Cleanup will
        /// unload it if it is still loaded.
        /// </summary>
        public void TakeOwnership(Scene scene)
        {
            EnsureActive();

            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new AssertionException(
                    $"Scene '{scene.path}' must be loaded before the fixture can take ownership.");
            }

            if (scene == _previousActiveScene)
            {
                throw new AssertionException(
                    $"Scene '{scene.path}' is the scene that was active before the test and must be preserved.");
            }

            if (!_ownedScenes.Contains(scene))
                _ownedScenes.Add(scene);
        }

        /// <summary>
        /// Resolves exactly one loaded scene with the supplied exact asset path.
        /// </summary>
        public Scene GetLoadedScene(string scenePath)
        {
            EnsureActive();
            ValidateScenePath(scenePath);

            int matches = 0;
            Scene found = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate.isLoaded &&
                    string.Equals(candidate.path, scenePath, StringComparison.Ordinal))
                {
                    found = candidate;
                    matches++;
                }
            }

            if (matches == 1)
                return found;

            throw new AssertionException(
                matches == 0
                    ? $"No loaded scene has the exact path '{scenePath}'."
                    : $"Scene path '{scenePath}' is loaded {matches} times; exactly one was expected.");
        }

        public bool IsSceneLoaded(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
                return false;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate.isLoaded &&
                    string.Equals(candidate.path, scenePath, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Unloads a scene that was previously loaded by this fixture.
        /// </summary>
        public IEnumerator UnloadScene(Scene scene)
        {
            EnsureActive();

            if (!_ownedScenes.Contains(scene))
            {
                throw new AssertionException(
                    $"Scene '{scene.path}' was not loaded by this fixture and cannot be unloaded by it.");
            }

            if (scene == _previousActiveScene)
            {
                throw new AssertionException(
                    $"Scene '{scene.path}' is the scene that was active before the test and must be preserved.");
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                _ownedScenes.Remove(scene);
                yield break;
            }

            AsyncOperation operation = null;
            Exception startException = null;
            try
            {
                operation = SceneManager.UnloadSceneAsync(scene);
            }
            catch (Exception exception)
            {
                startException = exception;
            }

            if (startException != null)
            {
                throw new AssertionException(
                    $"Could not start unloading scene '{scene.path}': {startException.Message}");
            }

            if (operation == null)
            {
                throw new AssertionException(
                    $"Unity returned no unload operation for scene '{scene.path}'.");
            }

            while (!operation.isDone)
                yield return null;

            _ownedScenes.Remove(scene);

            if (scene.IsValid() && scene.isLoaded)
            {
                throw new AssertionException(
                    $"Scene '{scene.path}' remained loaded after the unload operation completed.");
            }
        }

        /// <summary>
        /// Creates a GameObject that cleanup will destroy.
        /// </summary>
        public GameObject CreateGameObject(string name)
        {
            EnsureActive();

            GameObject gameObject = new GameObject(name);
            _ownedGameObjects.Add(gameObject);
            return gameObject;
        }

        public PlayModeLogCapture StartLogCapture()
        {
            EnsureActive();

            if (_logCapture != null)
                throw new InvalidOperationException("Log capture is already active.");

            _logCapture = new PlayModeLogCapture();
            return _logCapture;
        }

        public void StopLogCapture()
        {
            if (_logCapture == null)
                return;

            _logCapture.Dispose();
            _logCapture = null;
        }

        /// <summary>
        /// Restores all fixture-managed state. Safe to call once explicitly from
        /// a test; subsequent calls from [UnityTearDown] are no-ops.
        /// Each step is attempted even if an earlier step fails.
        /// </summary>
        public IEnumerator Cleanup()
        {
            if (_cleanedUp)
                yield break;

            _cleanedUp = true;

            List<string> errors = new List<string>();

            try
            {
                Time.timeScale = 1f;
            }
            catch (Exception exception)
            {
                errors.Add("Could not reset Time.timeScale: " + exception.Message);
            }

            try
            {
                StopLogCapture();
            }
            catch (Exception exception)
            {
                errors.Add("Could not stop log capture: " + exception.Message);
            }

            bool destroyedAnyGameObjects = false;
            for (int i = _ownedGameObjects.Count - 1; i >= 0; i--)
            {
                GameObject gameObject = _ownedGameObjects[i];
                if (gameObject == null)
                    continue;

                try
                {
                    UnityEngine.Object.Destroy(gameObject);
                    destroyedAnyGameObjects = true;
                }
                catch (Exception exception)
                {
                    errors.Add($"Could not destroy test GameObject '{gameObject.name}': {exception.Message}");
                }
            }

            _ownedGameObjects.Clear();

            if (destroyedAnyGameObjects)
                yield return null;

            RestoreActiveScene(errors);

            for (int i = _ownedScenes.Count - 1; i >= 0; i--)
            {
                Scene scene = _ownedScenes[i];
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    _ownedScenes.RemoveAt(i);
                    continue;
                }

                AsyncOperation operation = null;
                Exception startException = null;
                try
                {
                    operation = SceneManager.UnloadSceneAsync(scene);
                }
                catch (Exception exception)
                {
                    startException = exception;
                }

                if (startException != null)
                {
                    errors.Add($"Could not start unloading test scene '{scene.path}': {startException.Message}");
                    _ownedScenes.RemoveAt(i);
                    continue;
                }

                if (operation != null)
                {
                    while (!operation.isDone)
                        yield return null;
                }

                _ownedScenes.RemoveAt(i);

                if (scene.IsValid() && scene.isLoaded)
                    errors.Add($"Test scene '{scene.path}' remained loaded after cleanup.");
            }

            RestoreActiveScene(errors);

            try
            {
                ResetStaticState();
            }
            catch (Exception exception)
            {
                errors.Add("Could not reset static test state: " + exception.Message);
            }

            try
            {
                RestoreManagerSnapshot();
            }
            catch (Exception exception)
            {
                errors.Add("Could not restore GameSceneManager state: " + exception.Message);
            }

            try
            {
                Time.timeScale = _previousTimeScale;
            }
            catch (Exception exception)
            {
                errors.Add("Could not restore Time.timeScale: " + exception.Message);
            }

            if (errors.Count > 0)
            {
                throw new AssertionException(
                    "PlayMode fixture cleanup reported errors:" + Environment.NewLine +
                    string.Join(Environment.NewLine, errors));
            }
        }

        private static void ValidateScenePath(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
                throw new AssertionException("A scene path is required.");

            if (scenePath.IndexOf('/') < 0 && scenePath.IndexOf('\\') < 0)
                throw new AssertionException($"Scene name '{scenePath}' is not an exact scene path.");

            if (!scenePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !scenePath.EndsWith(".unity", StringComparison.Ordinal))
            {
                throw new AssertionException(
                    $"Scene path '{scenePath}' must be an exact 'Assets/....unity' asset path.");
            }
        }

        private void RestoreActiveScene(List<string> errors)
        {
            if (!_previousActiveScene.IsValid() || !_previousActiveScene.isLoaded)
                return;

            if (SceneManager.GetActiveScene() == _previousActiveScene)
                return;

            try
            {
                if (!SceneManager.SetActiveScene(_previousActiveScene))
                {
                    errors.Add(
                        $"Unity refused to restore active scene '{_previousActiveScene.path}'.");
                }
            }
            catch (Exception exception)
            {
                errors.Add($"Could not restore active scene '{_previousActiveScene.path}': {exception.Message}");
            }
        }

        private void CaptureManagerSnapshot()
        {
            Type managerType = ReflectionTestUtilities.ResolveRuntimeType(GameSceneManagerTypeName);
            object candidate = ReflectionTestUtilities.GetStaticProperty(managerType, "Instance");

            UnityEngine.Object unityObject = candidate as UnityEngine.Object;
            if (candidate == null || unityObject == null)
                return;

            _manager = candidate;
            _managerCurrentRoomScene =
                ReflectionTestUtilities.GetInstanceField<string>(_manager, CurrentRoomSceneField);
            _managerCurrentRoomDefinition =
                ReflectionTestUtilities.GetInstanceField(_manager, CurrentRoomDefinitionField);
            _managerTargetDoorId =
                ReflectionTestUtilities.GetInstanceField<string>(_manager, TargetDoorIdField);
            _managerIsTransitioning =
                ReflectionTestUtilities.GetInstanceField<bool>(_manager, IsTransitioningField);
            _managerSnapshotCaptured = true;
        }

        private void RestoreManagerSnapshot()
        {
            if (!_managerSnapshotCaptured || !HasLiveManager())
                return;

            ReflectionTestUtilities.SetInstanceField(
                _manager,
                CurrentRoomSceneField,
                _managerCurrentRoomScene);
            ReflectionTestUtilities.SetInstanceField(
                _manager,
                CurrentRoomDefinitionField,
                _managerCurrentRoomDefinition);
            ReflectionTestUtilities.SetInstanceField(
                _manager,
                TargetDoorIdField,
                _managerTargetDoorId);
            ReflectionTestUtilities.SetInstanceField(
                _manager,
                IsTransitioningField,
                _managerIsTransitioning);
        }

        private bool HasLiveManager()
        {
            UnityEngine.Object unityObject = _manager as UnityEngine.Object;
            return _manager != null && unityObject != null;
        }

        private void SetManagerTransitioning(bool isTransitioning)
        {
            if (!HasLiveManager())
                return;

            ReflectionTestUtilities.SetInstanceField(_manager, IsTransitioningField, isTransitioning);
        }

        private void EnsureActive()
        {
            if (!_initialized)
                throw new InvalidOperationException("PlayModeTestFixture.SetUp must be called before use.");

            if (_cleanedUp)
                throw new InvalidOperationException("PlayModeTestFixture has already been cleaned up.");
        }
    }
}
