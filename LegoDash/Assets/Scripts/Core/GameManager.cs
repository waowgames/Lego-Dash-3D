using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Coordinates stands, task zone, temporary storage, and level win/fail flow.
/// </summary>
public class GameManager : SingletonMonoBehaviour<GameManager>
{
    [Header("Scene References")]
    [SerializeField]
    private List<StandController> _stands = new();

    [SerializeField]
    private TaskZoneController _taskZone;

    [SerializeField]
    private TemporaryZoneController _temporaryZone;

    [Header("Bricks")]
    [SerializeField]
    private List<BrickPrefabMapping> _brickPrefabs = new();

    [Header("Level")]
    [Tooltip("LevelMissionManager yoksa başlangıçta yüklenir.")]
    [SerializeField]
    private LevelConfig _initialLevelConfig;

    [SerializeField]
    private bool _startLevelOnStart = true;

    private readonly Queue<TaskData> _taskQueue = new();
    private Dictionary<BrickColor, GameObject> _prefabLookup;
    private bool _levelFailed;
    private bool _levelCompleted;
    private bool _levelStarted;
    private LevelConfig _activeLevelConfig;
    private int _activeLevelIndex;

    protected override void Awake()
    {
        base.Awake();
        FillTheStands();
        _prefabLookup = _brickPrefabs.ToDictionary(mapping => mapping.Color, mapping => mapping.Prefab);
        if (_taskZone != null)
        {
            _taskZone.OnTaskCompleted += HandleTaskCompleted;
        }
    }

    private void Start()
    {
        if (!_levelStarted && _startLevelOnStart && LevelMissionManager.Instance == null)
        {
            StartLevel(_initialLevelConfig, 0);
        }
    }

    private void FillTheStands()
    {
        _stands = FindObjectsByType<StandController>(FindObjectsSortMode.None).ToList();
    }

    public void StartLevel(LevelConfig config, int levelIndex)
    {
        _levelStarted = true;
        _levelFailed = false;
        _levelCompleted = false;
        _activeLevelConfig = config;
        _activeLevelIndex = levelIndex;

        _taskQueue.Clear();
        _temporaryZone?.ResetZone();

        if (config == null)
        {
            Debug.LogWarning("LevelConfig atanmadı; örnek kurulum kullanılmayacak.");
            return;
        }

        if (_temporaryZone != null)
        {
            _temporaryZone.SetCapacity(config.StorageCapacity);
        }

        BuildStandsFromConfig(config);
        EnqueueTasksFromConfig(config);
        BeginNextTask();
    }

    private void BuildStandsFromConfig(LevelConfig config)
    {
        FillTheStands();
        var standLayouts = config.Stands;

        for (int i = 0; i < _stands.Count; i++)
        {
            var layout = i < standLayouts.Count ? standLayouts[i] : null;
            var bricks = layout != null ? layout.Bricks : Array.Empty<BrickColor>();
            _stands[i].BuildStand(bricks, _prefabLookup);
        }

        if (_stands.Count < standLayouts.Count)
        {
            Debug.LogWarning("Sahnedeki stant sayısı LevelConfig içindeki tanımdan az.");
        }
    }

    private void EnqueueTasksFromConfig(LevelConfig config)
    {
        foreach (var task in config.Tasks)
        {
            _taskQueue.Enqueue(new TaskData(task.Color, task.RequiredCount));
        }
    }

    /// <summary>
    /// Public entry point for UI buttons or input handlers when a stand is tapped.
    /// </summary>
    public void HandleStandTapped(StandController stand)
    {
        if (_levelFailed || _levelCompleted)
        {
            return;
        }

        if (stand == null || _taskZone == null)
        {
            return;
        }

        var topColor = stand.PeekTopColor();
        if (!topColor.HasValue)
        {
            return;
        }

        var brickGroup = stand.PopTopGroup();
        if (brickGroup.Count == 0)
        {
            return;
        }

        if (topColor.Value == _taskZone.CurrentColor)
        {
            SendBricksToTaskWithOverflowHandling(brickGroup);
        }
        else
        {
            SendToTemporaryZone(brickGroup);
        }
    }

    private void SendToTaskZone(List<Brick> bricks)
    {
        _taskZone?.AddBricks(bricks);
    }

    private void SendBricksToTaskWithOverflowHandling(List<Brick> bricks)
    {
        if (_taskZone == null)
        {
            return;
        }

        int remainingNeed = Mathf.Max(0, _taskZone.RequiredCount - _taskZone.CurrentCount);

        if (remainingNeed == 0)
        {
            SendToTemporaryZone(bricks);
            return;
        }

        if (bricks.Count <= remainingNeed)
        {
            SendToTaskZone(bricks);
            return;
        }

        var bricksForTask = bricks.Take(remainingNeed).ToList();
        var overflow = bricks.Skip(remainingNeed).ToList();

        if (_temporaryZone != null && !_temporaryZone.CanAccept(overflow.Count))
        {
            FailLevel();
            return;
        }

        SendToTaskZone(bricksForTask);
        SendToTemporaryZone(overflow);
    }

    private void SendToTemporaryZone(List<Brick> bricks)
    {
        if (_temporaryZone == null)
        {
            return;
        }

        if (!_temporaryZone.CanAccept(bricks.Count))
        {
            FailLevel();
            return;
        }

        var stored = _temporaryZone.AddBricks(bricks);
        if (!stored)
        {
            FailLevel();
            return;
        }
    }

    private void HandleTaskCompleted()
    {
        StartCoroutine(BeginNextTaskRoutine());
    }

    private IEnumerator BeginNextTaskRoutine()
    {
        yield return new WaitForSeconds(0.25f);
        BeginNextTask();
    }

    private void BeginNextTask()
    {
        if (_taskQueue.Count == 0)
        {
            _levelCompleted = true;
            Debug.Log("All tasks completed! Level clear.");
            LevelManager.Instance?.CompleteLevel(true);
            return;
        }

        if (_taskZone == null)
        {
            Debug.LogWarning("TaskZoneController bulunamadı.");
            return;
        }

        var task = _taskQueue.Dequeue();
        _taskZone.InitTask(task.Color, task.RequiredCount);
        MoveTemporaryMatches();
    }

    /// <summary>
    /// When the task color changes, automatically move matching bricks from the temporary zone.
    /// </summary>
    private void MoveTemporaryMatches()
    {
        if (_temporaryZone == null || _taskZone == null)
        {
            return;
        }

        int remainingNeed = Mathf.Max(0, _taskZone.RequiredCount - _taskZone.CurrentCount);
        if (remainingNeed == 0)
        {
            return;
        }

        var matchingBricks = _temporaryZone.ExtractBricksOfColor(_taskZone.CurrentColor, remainingNeed);
        if (matchingBricks.Count == 0)
        {
            return;
        }

        SendToTaskZone(matchingBricks);
    }

    private void FailLevel()
    {
        if (_levelFailed)
        {
            return;
        }

        _levelFailed = true;
        Debug.LogError("Temporary Zone overflow. Level failed.");
        LevelManager.Instance?.FailLevel();
    }
}

[System.Serializable]
public struct TaskData
{
    public BrickColor Color;
    public int RequiredCount;

    public TaskData(BrickColor color, int requiredCount)
    {
        Color = color;
        RequiredCount = requiredCount;
    }
}

[System.Serializable]
public struct BrickPrefabMapping
{
    public BrickColor Color;
    public GameObject Prefab;
}
