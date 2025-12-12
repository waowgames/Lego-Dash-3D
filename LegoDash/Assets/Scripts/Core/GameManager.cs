using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Coordinates stands, task car convoy, temporary storage, and level win/fail flow.
/// </summary>
public class GameManager : SingletonMonoBehaviour<GameManager>
{
    [Header("Scene References")]
    [SerializeField]
    private List<StandController> _stands = new();

    [SerializeField]
    private TaskCarManager _taskCarManager;

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
        if (_taskCarManager != null)
        {
            _taskCarManager.OnActiveCarChanged += HandleActiveCarChanged;
            _taskCarManager.OnAllCarsCompleted += HandleAllCarsCompleted;
        }
    }

    private void Start()
    {
        if (!_levelStarted && _startLevelOnStart && LevelMissionManager.Instance == null)
        {
            StartLevel(_initialLevelConfig, 0);
        }
    }

    private void OnDestroy()
    {
        if (_taskCarManager != null)
        {
            _taskCarManager.OnActiveCarChanged -= HandleActiveCarChanged;
            _taskCarManager.OnAllCarsCompleted -= HandleAllCarsCompleted;
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
        BuildTasksFromConfig(config);
        MoveTemporaryMatches();
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

    private void BuildTasksFromConfig(LevelConfig config)
    {
        _taskCarManager?.BuildConvoyFromConfig(config.Tasks);
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

        if (stand == null || _taskCarManager == null)
        {
            return;
        }

        var activeCar = _taskCarManager.ActiveCar;
        if (activeCar == null)
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

        if (topColor.Value == activeCar.TaskColor)
        {
            SendBricksToActiveCarWithOverflowHandling(brickGroup, activeCar);
        }
        else
        {
            SendToTemporaryZone(brickGroup);
        }
    }

    private void SendToActiveCar(List<Brick> bricks)
    {
        _taskCarManager?.AddBricksToActiveCar(bricks);
    }

    private void SendBricksToActiveCarWithOverflowHandling(List<Brick> bricks, TaskCar activeCar)
    {
        if (_taskCarManager == null || activeCar == null)
        {
            return;
        }

        int remainingNeed = Mathf.Max(0, activeCar.RequiredBrickCount - activeCar.CurrentBrickCount);

        if (remainingNeed == 0)
        {
            SendToTemporaryZone(bricks);
            return;
        }

        if (bricks.Count <= remainingNeed)
        {
            SendToActiveCar(bricks);
            return;
        }

        var bricksForTask = bricks.Take(remainingNeed).ToList();
        var overflow = bricks.Skip(remainingNeed).ToList();

        if (_temporaryZone != null && !_temporaryZone.CanAccept(overflow.Count))
        {
            FailLevel();
            return;
        }

        SendToActiveCar(bricksForTask);
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

    /// <summary>
    /// When the active car changes, automatically move matching bricks from the temporary zone.
    /// </summary>
    private void MoveTemporaryMatches()
    {
        if (_temporaryZone == null || _taskCarManager == null)
        {
            return;
        }

        var activeCar = _taskCarManager.ActiveCar;
        if (activeCar == null)
        {
            return;
        }

        int remainingNeed = Mathf.Max(0, activeCar.RequiredBrickCount - activeCar.CurrentBrickCount);
        if (remainingNeed == 0)
        {
            return;
        }

        var matchingBricks = _temporaryZone.ExtractBricksOfColor(activeCar.TaskColor, remainingNeed);
        if (matchingBricks.Count == 0)
        {
            return;
        }

        SendToActiveCar(matchingBricks);
    }

    private void HandleActiveCarChanged(TaskCar activeCar)
    {
        MoveTemporaryMatches();
    }

    private void HandleAllCarsCompleted()
    {
        _levelCompleted = true;
        Debug.Log("All tasks completed! Level clear.");
        LevelManager.Instance?.CompleteLevel(true);
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
public struct BrickPrefabMapping
{
    public BrickColor Color;
    public GameObject Prefab;
}
