using System.Collections;
using TMPro;
using UnityEngine;

public class AreaTitleController : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private float startDelay = 0.4f;
    [SerializeField] private float fadeIn = 0.8f;
    [SerializeField] private float hold = 2.5f;
    [SerializeField] private float fadeOut = 1.0f;

    private GameSceneManager _sceneManager;
    private AreaDefinition _lastArea;
    private Coroutine _titleRoutine;

    private void Start()
    {
        if (group != null)
            group.alpha = 0f;

        _sceneManager = GameSceneManager.Instance;
        if (_sceneManager != null)
        {
            _sceneManager.RoomEntered += OnRoomEntered;
            OnRoomEntered(_sceneManager.CurrentRoomDefinition);
        }
    }

    private void OnDestroy()
    {
        if (_sceneManager != null)
            _sceneManager.RoomEntered -= OnRoomEntered;
    }

    private void OnRoomEntered(RoomDefinition room)
    {
        AreaDefinition area = room != null ? room.Area : null;
        if (area == null || !area.ShowTitle)
            return;

        if (area == _lastArea)
            return;

        _lastArea = area;
        if (_titleRoutine != null)
            StopCoroutine(_titleRoutine);
        _titleRoutine = StartCoroutine(ShowAreaTitle(area));
    }

    private IEnumerator ShowAreaTitle(AreaDefinition area)
    {
        if (titleText != null)
            titleText.text = area.Title;

        if (subtitleText != null)
        {
            subtitleText.text = area.Subtitle;
            subtitleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(area.Subtitle));
        }

        if (group != null)
            group.alpha = 0f;

        yield return new WaitForSecondsRealtime(startDelay);
        yield return FadeGroup(1f, fadeIn);
        yield return new WaitForSecondsRealtime(hold);
        yield return FadeGroup(0f, fadeOut);

        _titleRoutine = null;
    }

    private IEnumerator FadeGroup(float targetAlpha, float duration)
    {
        if (group == null)
            yield break;

        float startAlpha = group.alpha;
        if (duration <= 0f)
        {
            group.alpha = targetAlpha;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        group.alpha = targetAlpha;
    }
}
