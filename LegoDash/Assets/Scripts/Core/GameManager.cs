using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Coordinates stands, task car convoy, temporary storage, and level win/fail flow.
/// </summary>
public class GameManager : SingletonMonoBehaviour<GameManager>
{
    [Header("Scene References")] [SerializeField]
    private List<StandController> _stands = new();

    [SerializeField] private TaskCarManager _taskCarManager;

    [SerializeField] private TemporaryZoneController _temporaryZone;

    [Header("Construction")] [SerializeField]
    private ConstructionManager _constructionManager;

    [Header("Bricks")] [SerializeField] private List<BrickPrefabMapping> _brickPrefabs = new();

    [Header("Task Car Models")] [SerializeField]
    private List<TaskCarPrefabMapping> _taskCarPrefabs = new();

    [Header("Level")] [Tooltip("LevelMissionManager yoksa başlangıçta yüklenir.")] [SerializeField]
    private LevelConfig _initialLevelConfig;

    [SerializeField] private bool _startLevelOnStart = true;

    public AudioClip brickPlacementSound;
    

    private Dictionary<BrickColor, GameObject> _prefabLookup;
    private Dictionary<BrickColor, GameObject> _taskCarPrefabLookup;
    private bool _levelFailed;
    private bool _levelCompleted;
    private bool _levelStarted;
    private bool _inputLocked;
    private Construction _activeConstruction;
    private LevelConfig _currentLevelConfig;
    private int _currentLevelIndex;

    protected override void Awake()
    {
        base.Awake();
        FillTheStands();
        _prefabLookup = _brickPrefabs.ToDictionary(mapping => mapping.Color, mapping => mapping.Prefab);
        _taskCarPrefabLookup = _taskCarPrefabs.ToDictionary(mapping => mapping.Color, mapping => mapping.Prefab);
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

        if (_activeConstruction != null)
        {
            _activeConstruction.OnConstructionCompleted -= HandleConstructionCompleted;
        }
    }

    private void FillTheStands()
    {
        var stands = FindObjectsByType<StandController>(FindObjectsSortMode.None).ToList();
        foreach (var stand in stands)
        {
            if(_stands.Contains(stand)) continue;
            else _stands.Add(stand);
        }
    }

    public void StartLevel(LevelConfig config, int levelIndex)
    {
        _levelStarted = true;
        _levelFailed = false;
        _levelCompleted = false; 

        _currentLevelConfig = config;
        _currentLevelIndex = levelIndex;

        _temporaryZone?.ResetZone();
 

        if (_constructionManager == null)
        {
            Debug.LogWarning("ConstructionManager atanmadı; inşaat kurulumu atlanacak.");
        }

        _activeConstruction = _constructionManager != null ? _constructionManager.InitializeForLevel(config) : null;
        _taskCarManager?.SetConstruction(_activeConstruction);

        if (_activeConstruction != null)
        {
            _activeConstruction.OnConstructionCompleted += HandleConstructionCompleted;
        }

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

        // LevelMissionManager yoksa, LevelManager'ı manuel başlat ki deneme sayısı,
        // timer ve event akışı her yeniden başlatmada doğru sıfırlansın.
        if (LevelMissionManager.Instance == null && LevelManager.Instance != null)
        {
            LevelManager.Instance.BeginLevel(config, levelIndex);
        }
    }

    public void RestartActiveLevel()
    {
        if (_currentLevelConfig == null)
        {
            Debug.LogWarning("Restart requested but no level has been initialized.");
            return;
        }

        StartLevel(_currentLevelConfig, _currentLevelIndex);
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
        if (_inputLocked || _levelFailed || _levelCompleted)
        {
            return;
        }

        if (stand == null || _taskCarManager == null)
        {
            return;
        }

        if (_taskCarManager.IsAdvancing)
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
            SendBricksToActiveCarWithOverflowHandling(brickGroup, activeCar, stand);
        }
        else
        {
            SendToTemporaryZone(brickGroup);
        }
    }

    private List<Brick> SendToActiveCar(List<Brick> bricks)
    {
        if (_taskCarManager == null)
        {
            return bricks;
        }

        return _taskCarManager.AddBricksToActiveCar(bricks) ?? new List<Brick>();
    }

    private void SendBricksToActiveCarWithOverflowHandling(List<Brick> bricks, TaskCar activeCar,
        StandController sourceStand)
    {
        if (_taskCarManager == null || activeCar == null)
        {
            return;
        }

        int remainingNeed = activeCar.RemainingNeed();

        if (remainingNeed == 0)
        {
            SendToTemporaryZone(bricks);
            return;
        }

        if (bricks.Count <= remainingNeed)
        {
            var rejected = SendToActiveCar(bricks);
            if (rejected.Count > 0)
            {
                sourceStand?.ReturnBricksToTop(rejected);
            }
            return;
        }

        var bricksForTask = bricks.Take(remainingNeed).ToList();
        var overflow = bricks.Skip(remainingNeed).ToList();

        var leftovers = SendToActiveCar(bricksForTask);

        if (leftovers.Count > 0)
        {
            overflow.AddRange(leftovers);
        }

        if (overflow.Count == 0)
        {
            return;
        }

        if (_temporaryZone != null && _temporaryZone.CanAccept(overflow.Count))
        {
            SendToTemporaryZone(overflow);
            return;
        }

        sourceStand?.ReturnBricksToTop(overflow);
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

        int remainingNeed = activeCar.RemainingNeed();
        if (remainingNeed == 0)
        {
            return;
        }

        var matchingBricks = _temporaryZone.ExtractBricksOfColor(activeCar.TaskColor, remainingNeed);
        if (matchingBricks.Count == 0)
        {
            return;
        }

        var rejected = SendToActiveCar(matchingBricks);
        if (rejected.Count > 0)
        {
            var restored = _temporaryZone.AddBricks(rejected);
            if (!restored)
            {
                FailLevel();
            }
        }
    }

    private void HandleActiveCarChanged(TaskCar activeCar)
    {
        MoveTemporaryMatches();
    }

    private void HandleAllCarsCompleted()
    {
        if (_activeConstruction == null)
        {
            _levelCompleted = true;
            Debug.Log("All tasks completed! Level clear.");
            LevelManager.Instance?.CompleteLevel(true);
            return;
        }

        if (_activeConstruction.IsComplete)
        {
            HandleConstructionCompleted();
        }
    }

    private void HandleConstructionCompleted()
    {
        if (_levelCompleted)
        {
            return;
        }

        _levelCompleted = true;
        Debug.Log("Construction finished! Level clear.");
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

        // Emniyet: LevelEnded event'i tetiklenmezse bile başarısızlık popup'ı açılsın.
        LevelFailPopup.Instance?.Show();
    }

    public void SetInputLocked(bool locked)
    {
        _inputLocked = locked;
    }

    public Construction ActiveConstruction => _activeConstruction;

    public GameObject GetTaskCarPrefab(BrickColor color)
    {
        if (_taskCarPrefabLookup != null && _taskCarPrefabLookup.TryGetValue(color, out var prefab))
        {
            return prefab;
        }

        Debug.LogWarning($"Task car prefab for color {color} not found.");
        return null;
    }
}

[System.Serializable]
public struct BrickPrefabMapping
{
    public BrickColor Color;
    public GameObject Prefab;
}

[System.Serializable]
public struct TaskCarPrefabMapping
{
    public BrickColor Color;
    public GameObject Prefab;
}
