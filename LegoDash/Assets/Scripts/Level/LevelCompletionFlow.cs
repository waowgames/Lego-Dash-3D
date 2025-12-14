using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

public class LevelCompletionFlow : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private LevelMissionManager levelMissionManager;
    [SerializeField] private LevelUpPopup levelUpPopup;

    [Header("Gameplay Objects to Toggle")]
    [SerializeField] private List<GameObject> gameplayObjects = new();

    [Header("Cameras")]
    [SerializeField] private CinemachineBrain cinemachineBrain;
    [SerializeField] private CinemachineVirtualCamera gameplayCamera;
    [SerializeField] private CinemachineVirtualCamera constructionCamera;

    [Header("Construction Celebration")]
    [SerializeField] private float celebrationPieceDelay = 0.05f;
    [SerializeField] private float celebrationPunchStrength = 0.08f;
    [SerializeField] private float celebrationDuration = 0.15f;

    private readonly Dictionary<GameObject, bool> _objectActiveStates = new();
    private bool _isSequenceRunning;
    private bool _waitingForNext;
    private int _cachedGameplayPriority;
    private int _cachedConstructionPriority;

    private void OnEnable()
    {
        Events.LevelEnded += HandleLevelEnded;
    }

    private void OnDisable()
    {
        Events.LevelEnded -= HandleLevelEnded;
    }

    private void HandleLevelEnded(LevelEndPayload payload)
    {
        if (!payload.Success || _isSequenceRunning)
        {
            return;
        }

        StartCoroutine(RunCompletionSequence());
    }

    private IEnumerator RunCompletionSequence()
    {
        _isSequenceRunning = true;
        gameManager?.SetInputLocked(true);

        CacheAndToggleGameplayObjects(false);

        yield return SwitchToConstructionCamera();

        var construction = gameManager != null ? gameManager.ActiveConstruction : null;
        if (construction != null)
        {
            yield return construction.PlayCompletionCelebration(celebrationPieceDelay, celebrationPunchStrength,
                celebrationDuration);
        }

        yield return ShowLevelUpPopup();
    }

    private void CacheAndToggleGameplayObjects(bool active)
    {
        if (!active)
        {
            _objectActiveStates.Clear();
        }

        foreach (var go in gameplayObjects)
        {
            if (go == null)
            {
                continue;
            }

            if (!active)
            {
                _objectActiveStates[go] = go.activeSelf;
            }

            var shouldBeActive = active && _objectActiveStates.TryGetValue(go, out var wasActive)
                ? wasActive
                : active;
            go.SetActive(shouldBeActive);
        }
    }

    private IEnumerator SwitchToConstructionCamera()
    {
        if (constructionCamera == null || gameplayCamera == null)
        {
            yield break;
        }

        _cachedConstructionPriority = constructionCamera.Priority;
        _cachedGameplayPriority = gameplayCamera.Priority;

        constructionCamera.Priority = Mathf.Max(_cachedConstructionPriority, _cachedGameplayPriority) + 1;

        yield return WaitForCameraBlend();
    }

    private IEnumerator SwitchToGameplayCamera()
    {
        if (constructionCamera == null || gameplayCamera == null)
        {
            yield break;
        }

        gameplayCamera.Priority = Mathf.Max(_cachedConstructionPriority, _cachedGameplayPriority) + 1;
        constructionCamera.Priority = _cachedConstructionPriority;

        yield return WaitForCameraBlend();

        gameplayCamera.Priority = _cachedGameplayPriority;
    }

    private IEnumerator WaitForCameraBlend()
    {
        if (cinemachineBrain == null)
        {
            yield break;
        }

        while (cinemachineBrain.IsBlending)
        {
            yield return null;
        }
    }

    private IEnumerator ShowLevelUpPopup()
    {
        _waitingForNext = true;

        if (levelUpPopup != null)
        {
            var displayedLevel = LevelManager.Instance != null
                ? LevelManager.Instance.DisplayedLevelNumber
                : 1;

            levelUpPopup.ShowMissionComplete(displayedLevel, HandleNextClicked);
        }
        else
        {
            HandleNextClicked();
        }

        while (_waitingForNext)
        {
            yield return null;
        }
    }

    private void HandleNextClicked()
    {
        if (!_isSequenceRunning)
        {
            return;
        }

        _waitingForNext = false;
        StartCoroutine(ResumeGameplay());
    }

    private IEnumerator ResumeGameplay()
    {
        CacheAndToggleGameplayObjects(true);

        yield return SwitchToGameplayCamera();

        levelMissionManager?.AdvanceToNextLevel();
        gameManager?.SetInputLocked(false);

        _objectActiveStates.Clear();
        _isSequenceRunning = false;
    }
}
