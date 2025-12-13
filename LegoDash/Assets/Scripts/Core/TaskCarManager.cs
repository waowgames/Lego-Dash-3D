using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Manages a convoy of task cars, ensures only the leading car is active, and advances
/// the line when a car is completed.
/// </summary>
public class TaskCarManager : MonoBehaviour
{
    [SerializeField]
    private TaskCar _taskCarPrefab;

    [SerializeField]
    private Transform _convoyRoot;

    [SerializeField]
    private float _carSpacing = 3f;

    [SerializeField]
    private float _convoyMoveDuration = 0.4f;

    [SerializeField]
    private Ease _convoyMoveEase = Ease.OutCubic;

    [SerializeField]
    private bool _recycleCompletedCars;

    [Header("Task Setup")]
    [SerializeField, Min(1)]
    private int _bricksPerTask = 9;

    [Header("Construction")]
    [SerializeField]
    private Construction _construction;

    [Header("Completed Car Exit")]
    [SerializeField]
    private float _completedTravelDistance = 5f;

    [SerializeField]
    private float _completedTravelDuration = 0.5f;

    [SerializeField]
    private Ease _completedTravelEase = Ease.OutCubic;

    private readonly List<TaskCar> _cars = new();
    private bool _isAdvancing;

    public TaskCar ActiveCar => _cars.Count > 0 ? _cars[0] : null;

    public event Action<TaskCar> OnActiveCarChanged;
    public event Action OnAllCarsCompleted;

    public void SetConstruction(Construction construction)
    {
        _construction = construction;
    }

    public void BuildConvoyFromConfig(IReadOnlyList<LevelTaskDefinition> tasks)
    {
        ClearExistingCars();

        if (_taskCarPrefab == null)
        {
            Debug.LogWarning("TaskCar prefab eksik.");
            OnActiveCarChanged?.Invoke(null);
            OnAllCarsCompleted?.Invoke();
            return;
        }

        if (tasks == null || tasks.Count == 0)
        {
            OnActiveCarChanged?.Invoke(null);
            OnAllCarsCompleted?.Invoke();
            return;
        }

        for (int i = 0; i < tasks.Count; i++)
        {
            var def = tasks[i];
            var car = Instantiate(_taskCarPrefab, _convoyRoot == null ? transform : _convoyRoot);
            car.transform.localPosition = GetLocalPositionForIndex(i);
            car.Initialize(def.Color, _bricksPerTask);
            car.SetActive(false);
            car.OnCompleted += HandleCarCompleted;
            _cars.Add(car);
        }

        RefreshActiveCar();
    }

    public List<Brick> AddBricksToActiveCar(List<Brick> bricks)
    {
        if (_isAdvancing || ActiveCar == null || bricks == null || bricks.Count == 0)
        {
            return bricks;
        }

        return ActiveCar.AddBricks(bricks);
    }

    public int GetActiveRemainingNeed()
    {
        return ActiveCar == null ? 0 : ActiveCar.RemainingNeed();
    }

    private void HandleCarCompleted(TaskCar completedCar)
    {
        if (completedCar != ActiveCar || _isAdvancing)
        {
            return;
        }

        StartCoroutine(AdvanceConvoyCoroutine(completedCar));
    }

    private IEnumerator AdvanceConvoyCoroutine(TaskCar completedCar)
    {
        _isAdvancing = true;
        completedCar.SetActive(false);

        if (_construction != null)
        { 
            var collectedBricks = completedCar.CollectPlacedBricks();
            yield return _construction.BuildWithBricks(collectedBricks);
        }

        _cars.Remove(completedCar);

        if (_recycleCompletedCars)
        {
            _cars.Add(completedCar);
        }
        else
        {
            yield return MoveCompletedCarOut(completedCar);
        }

        var moveSequence = DOTween.Sequence();
        for (int i = 0; i < _cars.Count; i++)
        {
            var car = _cars[i];
            var target = GetLocalPositionForIndex(i);
            moveSequence.Join(car.transform.DOLocalMove(target, _convoyMoveDuration).SetEase(_convoyMoveEase));
        }

        yield return moveSequence.WaitForCompletion();

        if (_recycleCompletedCars)
        {
            int recycledIndex = Mathf.Max(0, _cars.Count - 1);
            var recycledCar = _cars[recycledIndex];
            recycledCar.transform.localPosition = GetLocalPositionForIndex(recycledIndex);
            recycledCar.Initialize(recycledCar.TaskColor, recycledCar.RequiredBrickCount);
        }

        _isAdvancing = false;

        if (_cars.Count == 0)
        {
            OnActiveCarChanged?.Invoke(null);
            OnAllCarsCompleted?.Invoke();
            yield break;
        }

        RefreshActiveCar();
    }

    private Vector3 GetLocalPositionForIndex(int index)
    {
        var forward = (_convoyRoot == null ? transform : _convoyRoot).right;
        return -forward.normalized * (_carSpacing * index);
    }

    private void RefreshActiveCar()
    {
        for (int i = 0; i < _cars.Count; i++)
        {
            _cars[i].SetActive(i == 0);
        }

        OnActiveCarChanged?.Invoke(ActiveCar);
    }

    private IEnumerator MoveCompletedCarOut(TaskCar completedCar)
    {
        if (completedCar == null)
        {
            yield break;
        }

        var parent = _convoyRoot == null ? transform : _convoyRoot;
        var forward = parent.right.normalized;
        var exitTarget = completedCar.transform.localPosition + forward * _completedTravelDistance;

        yield return completedCar.transform
            .DOLocalMove(exitTarget, _completedTravelDuration)
            .SetEase(_completedTravelEase)
            .WaitForCompletion();

        Destroy(completedCar.gameObject);
    }

    private void ClearExistingCars()
    {
        foreach (var car in _cars)
        {
            car.OnCompleted -= HandleCarCompleted;
            if (car != null)
            {
                Destroy(car.gameObject);
            }
        }

        _cars.Clear();
    }
}
