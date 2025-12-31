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

    [SerializeField] private Material _lockedBrickMaterial;

    [Header("Task Car Models")] [SerializeField]
    private List<TaskCarPrefabMapping> _taskCarPrefabs = new();

    [Header("Level")] [Tooltip("LevelMissionManager yoksa başlangıçta yüklenir.")] [SerializeField]
    private LevelConfig _initialLevelConfig;

    [SerializeField] private bool _startLevelOnStart = true;

    public AudioClip brickPlacementSound;
    public GameObject particlePoof;
    

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
#if UNITY_EDITOR
            Debug.LogWarning("ConstructionManager atanmadı; inşaat kurulumu atlanacak.");
#endif
        }

        _activeConstruction = _constructionManager != null ? _constructionManager.InitializeForLevel(config) : null;
        _taskCarManager?.SetConstruction(_activeConstruction);

        if (_activeConstruction != null)
        {
            _activeConstruction.OnConstructionCompleted += HandleConstructionCompleted;
        }

        if (config == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("LevelConfig atanmadı; örnek kurulum kullanılmayacak.");
#endif
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
#if UNITY_EDITOR
            Debug.LogWarning("Restart requested but no level has been initialized.");
#endif
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

        ApplyLockedBricks(config);

        if (_stands.Count < standLayouts.Count)
        {
#if UNITY_EDITOR
            Debug.LogWarning("Sahnedeki stant sayısı LevelConfig içindeki tanımdan az.");
#endif
        }
    }

    private void ApplyLockedBricks(LevelConfig config)
    {
        if (config == null)
        {
            return;
        }

        int totalLocked = config.LockedBrickCount;
        if (totalLocked <= 0)
        {
            return;
        }

        var eligibleStands = _stands.Where(stand => stand != null && stand.BrickCount > 0).ToList();
        if (eligibleStands.Count == 0)
        {
            return;
        }

        int totalBricks = eligibleStands.Sum(stand => stand.BrickCount);
        totalLocked = Mathf.Clamp(totalLocked, 0, totalBricks);
        if (totalLocked == 0)
        {
            return;
        }

        var rng = new System.Random(unchecked(_currentLevelIndex * 397 ^ totalLocked));
        var standOrder = new List<StandController>(eligibleStands);
        if (config.RandomizeLockedBricks)
        {
            Shuffle(standOrder, rng);
        }

        var assigned = new Dictionary<StandController, int>(standOrder.Count);
        foreach (var stand in standOrder)
        {
            assigned[stand] = 0;
        }

        int remaining = totalLocked;
        while (remaining > 0)
        {
            bool assignedAny = false;

            foreach (var stand in standOrder)
            {
                if (remaining == 0)
                {
                    break;
                }

                int chunkSize = stand.GetLockChunkSize(assigned[stand]);
                if (chunkSize <= 0)
                {
                    continue;
                }

                if (chunkSize > remaining)
                {
                    continue;
                }

                assigned[stand] += chunkSize;
                remaining -= chunkSize;
                assignedAny = true;
            }

            if (assignedAny)
            {
                continue;
            }

            StandController fallbackStand = null;
            int fallbackSize = int.MaxValue;
            foreach (var stand in standOrder)
            {
                int chunkSize = stand.GetLockChunkSize(assigned[stand]);
                if (chunkSize <= 0)
                {
                    continue;
                }

                if (chunkSize < fallbackSize)
                {
                    fallbackSize = chunkSize;
                    fallbackStand = stand;
                }
            }

            if (fallbackStand == null)
            {
                break;
            }

            assigned[fallbackStand] += fallbackSize;
            remaining -= fallbackSize;
        }

        foreach (var entry in assigned)
        {
            if (entry.Value <= 0)
            {
                continue;
            }

            if (config.AllowLockedInMiddle)
            {
                int maxStart = Mathf.Max(0, entry.Key.BrickCount - entry.Value);
                int startIndex = maxStart == 0 ? 0 : rng.Next(0, maxStart + 1);
                entry.Key.LockRange(startIndex, entry.Value, _lockedBrickMaterial);
            }
            else
            {
                entry.Key.LockBottomBricks(entry.Value, _lockedBrickMaterial);
            }
        }
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = rng.Next(i + 1);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
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

        if (!stand.TryBeginCollect())
        {
            return;
        }

        try
        {
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
        finally
        {
            stand.EndCollect();
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
#if UNITY_EDITOR
            Debug.Log("All tasks completed! Level clear.");
#endif
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
#if UNITY_EDITOR
        Debug.Log("Construction finished! Level clear.");
#endif
        LevelManager.Instance?.CompleteLevel(true);
    }

    private void FailLevel()
    {
        if (_levelFailed)
        {
            return;
        }

        _levelFailed = true;
#if UNITY_EDITOR
        Debug.LogError("Temporary Zone overflow. Level failed.");
#endif
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

#if UNITY_EDITOR
        Debug.LogWarning($"Task car prefab for color {color} not found.");
#endif
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
