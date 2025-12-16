using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks mission progress for the active level and advances to the next level
/// when all mission requirements are cleared.
/// </summary>
public class LevelMissionManager : SingletonMonoBehaviour<LevelMissionManager>
{
    [SerializeField] private List<LevelConfig> levels = new();
    [SerializeField] private int startingLevelIndex = 0;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool autoAdvanceOnSuccess = false;
    [SerializeField] private float advanceDelaySeconds = 0.75f;

    public LevelConfig CurrentLevelConfig { get; private set; }
    public int CurrentLevelIndex { get; private set; }

    private void OnEnable()
    {
        Events.LevelEnded += HandleLevelEnded;
    }

    private void OnDisable()
    {
        Events.LevelEnded -= HandleLevelEnded;
    }

    private void Awake()
    {
        autoAdvanceOnSuccess = false;
    }

    private void Start()
    {
        if (autoStart)
        {
            LoadLevel(Mathf.Clamp(startingLevelIndex, 0, Mathf.Max(0, levels.Count - 1)));
        }
    }

    public void ReloadCurrentLevel()
    {
        LoadLevel(CurrentLevelIndex);
    }

    public void AdvanceToNextLevel()
    {
        if (levels.Count == 0)
        {
            Debug.LogWarning("AdvanceToNextLevel called but level list is empty.");
            return;
        }

        int nextIndex = Mathf.Clamp(CurrentLevelIndex + 1, 0, levels.Count - 1);
        LoadLevel(nextIndex);
    }

    private void HandleLevelEnded(LevelEndPayload payload)
    {
        if (!autoAdvanceOnSuccess || !payload.Success)
        {
            return;
        }

        StartCoroutine(AdvanceAfterDelay());
    }

    private IEnumerator AdvanceAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, advanceDelaySeconds));
        AdvanceToNextLevel();
    }

    private void LoadLevel(int index)
    {
        if (levels.Count == 0)
        {
            Debug.LogWarning("Level list is empty; cannot load level.");
            return;
        }

        int clampedIndex = Mathf.Clamp(index, 0, levels.Count - 1);
        CurrentLevelIndex = clampedIndex;
        CurrentLevelConfig = levels[clampedIndex];

        if (CurrentLevelConfig == null)
        {
            Debug.LogWarning($"Level {clampedIndex} asset is missing.");
            return;
        }

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.BeginLevel(CurrentLevelConfig, CurrentLevelIndex);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartLevel(CurrentLevelConfig, CurrentLevelIndex);
            Events.RaiseLevelStarted(new LevelStartPayload());
        }
    }
}
