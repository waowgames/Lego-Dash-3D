using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Coordinates stands, task zone, temporary storage, and level win/fail flow.
/// </summary>
public class GameManager : MonoBehaviour
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

    private readonly Queue<TaskData> _taskQueue = new();
    private Dictionary<BrickColor, GameObject> _prefabLookup;
    private bool _levelFailed;
    private bool _levelCompleted;

    private void Awake()
    {
        _prefabLookup = _brickPrefabs.ToDictionary(mapping => mapping.Color, mapping => mapping.Prefab);
        if (_taskZone != null)
        {
            _taskZone.OnTaskCompleted += HandleTaskCompleted;
        }
    }

    private void Start()
    {
        SetupExampleLevel();
        BeginNextTask();
    }

    /// <summary>
    /// Example level setup pulled from the provided specification.
    /// </summary>
    private void SetupExampleLevel()
    {
        if (_stands.Count < 2)
        {
            Debug.LogWarning("Not enough stand references configured for the example level.");
            return;
        }

        // Level 1 setup
        var standAColors = new[]
        {
            BrickColor.Blue, BrickColor.Blue, BrickColor.Blue,
            BrickColor.Red, BrickColor.Red, BrickColor.Red,
            BrickColor.Purple, BrickColor.Purple, BrickColor.Purple
        };

        var standBColors = new[]
        {
            BrickColor.Blue, BrickColor.Blue,
            BrickColor.Red,
            BrickColor.Yellow, BrickColor.Yellow
        };

        _stands[0].BuildStand(standAColors, _prefabLookup);
        _stands[1].BuildStand(standBColors, _prefabLookup);

        _taskQueue.Clear();
        _taskQueue.Enqueue(new TaskData(BrickColor.Blue, 9));
        _taskQueue.Enqueue(new TaskData(BrickColor.Red, 5));
        _taskQueue.Enqueue(new TaskData(BrickColor.Yellow, 3));
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

        if (stand == null)
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
            SendToTaskZone(brickGroup);
        }
        else
        {
            SendToTemporaryZone(brickGroup);
        }
    }

    private void SendToTaskZone(List<Brick> bricks)
    {
        _taskZone.AddBricks(bricks);
    }

    private void SendToTemporaryZone(List<Brick> bricks)
    {
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

        // Bricks are stored visually in the temporary zone, so keep their instances.
    }

    private void HandleTaskCompleted()
    {
        StartCoroutine(BeginNextTaskRoutine());
    }

    private IEnumerator BeginNextTaskRoutine()
    {
        // TODO: Completion animation
        yield return new WaitForSeconds(0.25f);
        BeginNextTask();
    }

    private void BeginNextTask()
    {
        if (_taskQueue.Count == 0)
        {
            _levelCompleted = true;
            Debug.Log("All tasks completed! Level clear.");
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
        var matchingBricks = _temporaryZone.ExtractBricksOfColor(_taskZone.CurrentColor);
        if (matchingBricks.Count == 0)
        {
            return;
        }

        SendToTaskZone(matchingBricks);
    }

    private void DestroyBricks(IEnumerable<Brick> bricks)
    {
        foreach (var brick in bricks)
        {
            if (brick.Instance != null)
            {
                Destroy(brick.Instance);
            }
        }
    }

    private void FailLevel()
    {
        if (_levelFailed)
        {
            return;
        }

        _levelFailed = true;
        Debug.LogError("Temporary Zone overflow. Level failed.");
        // TODO: Show fail UI and reset level.
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
