using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Booster that automatically collects required bricks from stands and feeds them to the active task car.
/// </summary>
public class AutoFillBooster : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private TaskCarManager _taskCarManager;

    [SerializeField]
    private List<StandController> _standControllers = new();

    [Header("Animation")]
    [SerializeField]
    private float _launchDelay = 0.05f;

    [SerializeField]
    private AnimationCurve _flyCurve;

    [SerializeField]
    private bool _sequential = true;

    private bool _isRunning;

    [Button]
    public void Use()
    {
        if (_isRunning)
        {
            return;
        }

        StartCoroutine(FillActiveTaskCarRoutine());
    }

    private IEnumerator FillActiveTaskCarRoutine()
    {
        _isRunning = true;

        EnsureDependencies();

        if (_taskCarManager == null)
        {
            Debug.LogWarning("AutoFillBooster: TaskCarManager bulunamadı.");
            _isRunning = false;
            yield break;
        }

        var taskCar = _taskCarManager.ActiveCar;
        if (taskCar == null)
        {
            Debug.LogWarning("AutoFillBooster: Aktif TaskCar yok.");
            _isRunning = false;
            yield break;
        }

        int remainingNeed = taskCar.RemainingNeed();
        if (remainingNeed <= 0)
        {
            Debug.Log("AutoFillBooster: Aktif görev zaten dolu.");
            _isRunning = false;
            yield break;
        }

        var stands = GetActiveStands();
        if (stands.Count == 0)
        {
            Debug.LogWarning("AutoFillBooster: Sahne üzerinde StandController bulunamadı.");
            _isRunning = false;
            yield break;
        }

        var collected = CollectBricks(taskCar.TaskColor, remainingNeed, stands);
        if (collected.Count == 0)
        {
            Debug.Log("AutoFillBooster: Uygun renkte brick bulunamadı.");
            _isRunning = false;
            yield break;
        }

        if (_sequential)
        {
            float delay = Mathf.Max(0f, _launchDelay);
            foreach (var entry in collected)
            {
                var rejected = SendToActiveCar(new List<Brick> { entry.Brick });
                HandleRejectedBricks(rejected, collected);

                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }
            }
        }
        else
        {
            var bricks = collected.Select(c => c.Brick).ToList();
            var rejected = SendToActiveCar(bricks);
            HandleRejectedBricks(rejected, collected);
        }

        _isRunning = false;
    }

    private void EnsureDependencies()
    {
        if (_taskCarManager == null)
        {
            _taskCarManager = FindObjectOfType<TaskCarManager>();
        }

        _standControllers.RemoveAll(s => s == null);
        if (_standControllers.Count == 0)
        {
            _standControllers.AddRange(FindObjectsByType<StandController>(FindObjectsSortMode.None));
        }
    }

    private List<StandController> GetActiveStands()
    {
        _standControllers.RemoveAll(s => s == null);
        return _standControllers;
    }

    private List<SelectedBrick> CollectBricks(BrickColor color, int needed, IEnumerable<StandController> stands)
    {
        var collected = new List<SelectedBrick>();

        foreach (var stand in stands)
        {
            if (stand == null)
            {
                continue;
            }

            while (collected.Count < needed && stand.TryPopBrickByColorFromBottom(color, out var brick))
            {
                collected.Add(new SelectedBrick(brick, stand));
            }

            if (collected.Count >= needed)
            {
                break;
            }
        }

        return collected;
    }

    private List<Brick> SendToActiveCar(List<Brick> bricks)
    {
        if (_taskCarManager == null || bricks == null || bricks.Count == 0)
        {
            return bricks ?? new List<Brick>();
        }

        var rejected = _taskCarManager.AddBricksToActiveCar(bricks);
        return rejected ?? new List<Brick>();
    }

    private void HandleRejectedBricks(List<Brick> rejected, List<SelectedBrick> selection)
    {
        if (rejected == null || rejected.Count == 0)
        {
            return;
        }

        var mapping = selection
            .Where(entry => entry.Brick != null && entry.SourceStand != null)
            .ToDictionary(entry => entry.Brick, entry => entry.SourceStand);

        foreach (var brick in rejected)
        {
            if (brick == null)
            {
                continue;
            }

            if (mapping.TryGetValue(brick, out var stand) && stand != null)
            {
                stand.ReturnBricksToTop(new List<Brick> { brick });
            }
            else if (brick.Instance != null)
            {
                Destroy(brick.Instance);
            }
        }
    }

    private readonly struct SelectedBrick
    {
        public Brick Brick { get; }
        public StandController SourceStand { get; }

        public SelectedBrick(Brick brick, StandController sourceStand)
        {
            Brick = brick;
            SourceStand = sourceStand;
        }
    }
}
