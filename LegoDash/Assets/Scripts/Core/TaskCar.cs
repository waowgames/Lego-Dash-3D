using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Represents a single task car in the convoy and handles brick intake/feedback.
/// </summary>
public class TaskCar : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField]
    private GameObject _activeHighlight;

    [Header("Task Setup")]
    [SerializeField]
    private List<Transform> _brickSlots = new();

    [SerializeField, Min(1)]
    private int _requiredBrickCount = 1;

    [Header("Animation")]
    [SerializeField]
    private Transform _stackAnchor;

    [SerializeField]
    private float _moveDuration = 0.35f;

    [SerializeField]
    private float _moveStaggerDelay = 0.05f;

    [SerializeField]
    private float _jumpPower = 0.75f;

    [SerializeField]
    private Ease _moveEase = Ease.OutCubic;

    [SerializeField]
    private float _brickHeightSpacing = 0.25f;

    private readonly List<Brick> _placedBricks = new();
    private int _incomingBricksCount;

    public BrickColor TaskColor { get; private set; }
    public int RequiredBrickCount { get; private set; }
    public int CurrentBrickCount => _placedBricks.Count;
    public bool IsCompleted => CurrentBrickCount >= RequiredBrickCount;

    public event Action<TaskCar> OnCompleted;

    public void Initialize(BrickColor color, int requiredCount)
    {
        TaskColor = color;
        _requiredBrickCount = Mathf.Max(1, requiredCount);
        RequiredBrickCount = _requiredBrickCount;
        ClearStoredBricks();
    }

    public void SetActive(bool isActive)
    {
        if (_activeHighlight != null)
        {
            _activeHighlight.SetActive(isActive);
        }

    }

    public bool CanAcceptBrickColor(BrickColor color)
    {
        return color == TaskColor && CurrentBrickCount < RequiredBrickCount;
    }

    public int RemainingNeed()
    {
        return Mathf.Max(0, RequiredBrickCount - CurrentBrickCount - _incomingBricksCount);
    }

    public List<Brick> CollectPlacedBricks()
    {
        var bricks = new List<Brick>(_placedBricks);
        _placedBricks.Clear();
        return bricks;
    }

    public List<Brick> AddBricks(List<Brick> bricks)
    {
        if (bricks == null || bricks.Count == 0)
        {
            return bricks;
        }

        int capacity = Mathf.Max(0, RequiredBrickCount - CurrentBrickCount - _incomingBricksCount);
        if (_brickSlots.Count > 0)
        {
            capacity = Mathf.Min(capacity, _brickSlots.Count - CurrentBrickCount - _incomingBricksCount);
        }
        if (capacity <= 0)
        {
            return bricks;
        }

        var acceptedBricks = new List<Brick>();
        var rejectedBricks = new List<Brick>();
        int reservedStartIndex = CurrentBrickCount + _incomingBricksCount;
        foreach (var brick in bricks)
        {
            if (acceptedBricks.Count >= capacity || !CanAcceptBrickColor(brick.Color))
            {
                rejectedBricks.Add(brick);
                continue;
            }

            acceptedBricks.Add(brick);
        }

        if (acceptedBricks.Count == 0)
        {
            return bricks;
        }

        _incomingBricksCount += acceptedBricks.Count;
        StartCoroutine(MoveBricksToCar(acceptedBricks, reservedStartIndex));

        return rejectedBricks;
    }

    private IEnumerator MoveBricksToCar(List<Brick> bricks, int startIndex)
    {
        bricks.Reverse();
        bool completionTriggered = IsCompleted;

        void OnBrickArrived(Brick arrivedBrick)
        {
            _incomingBricksCount = Mathf.Max(0, _incomingBricksCount - 1);
            _placedBricks.Add(arrivedBrick);
            if (!completionTriggered && IsCompleted)
            {
                completionTriggered = true;
                OnCompleted?.Invoke(this);
            }
        }

        for (int i = 0; i < bricks.Count; i++)
        {
            var brick = bricks[i];
            int targetIndex = startIndex + i;
            float delay = _moveStaggerDelay * i;

            if (brick.Instance != null)
            {
                var brickTransform = brick.Instance.transform;
                brickTransform.SetParent(_stackAnchor == null ? transform : _stackAnchor);

                var targetPosition = GetTargetPosition(targetIndex);
                StartCoroutine(MoveBrickWithDelay(brickTransform, targetPosition, delay, () => OnBrickArrived(brick)));
            }
            else
            {
                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }

                OnBrickArrived(brick);
            }
        }
    }

    private IEnumerator MoveBrickWithDelay(Transform brickTransform, Vector3 targetPosition, float delay, Action onComplete)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        yield return MoveBrick(brickTransform, targetPosition, onComplete);
    }

    private IEnumerator MoveBrick(Transform brickTransform, Vector3 targetPosition, Action onComplete = null)
    {
        if (brickTransform == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        brickTransform.DOKill();
        var startingRotation = brickTransform.rotation;

        var sequence = DOTween.Sequence();
        sequence.Join(brickTransform
            .DOJump(targetPosition, _jumpPower, 1, _moveDuration)
            .SetEase(_moveEase));

        sequence.Join(brickTransform.DORotate(new Vector3(0, 0, -90), _moveDuration));

        yield return sequence.WaitForCompletion();

        brickTransform.position = targetPosition; 

        onComplete?.Invoke();
    }

    private Vector3 GetTargetPosition(int index)
    {
        if (_brickSlots.Count > 0 && index < _brickSlots.Count)
        {
            return _brickSlots[index].position;
        }

        var anchor = _stackAnchor == null ? transform : _stackAnchor;
        return anchor.position + Vector3.up * (_brickHeightSpacing * index);
    }

    private void ClearStoredBricks()
    {
        foreach (var brick in _placedBricks)
        {
            if (brick.Instance != null)
            {
                Destroy(brick.Instance);
            }
        }

        _placedBricks.Clear();
        _incomingBricksCount = 0;
    }
}

