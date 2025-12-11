using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Tracks the current task requirements and forwards completion events.
/// </summary>
public class TaskZoneController : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _taskText;

    [Header("Animation")]
    [Tooltip("Optional anchor that defines where bricks stack inside the task zone.")]
    [SerializeField]
    private Transform _stackAnchor;

    [Tooltip("Optional explicit slot positions used to lay out task bricks (e.g. horizontally).")]
    [SerializeField]
    private List<Transform> _slotPositions = new();

    [Tooltip("Seconds it takes for a brick to travel into the task zone.")]
    [SerializeField]
    private float _moveDuration = 0.35f;

    [Tooltip("Delay between starting each brick's movement animation.")]
    [SerializeField]
    private float _moveStaggerDelay = 0.05f;

    [Tooltip("Jump height used while bricks travel into the zone.")]
    [SerializeField]
    private float _jumpPower = 0.75f;

    [Tooltip("How much the brick tilts while moving.")]
    [SerializeField]
    private float _movementTiltAngle = 45f;

    [Tooltip("Easing used for the incoming brick jump animation.")]
    [SerializeField]
    private Ease _moveEase = Ease.OutCubic;

    [Tooltip("Vertical spacing between stacked bricks in the task zone.")]
    [SerializeField]
    private float _brickHeightSpacing = 0.25f;

    private readonly List<Brick> _activeBricks = new();

    public BrickColor CurrentColor { get; private set; }
    public int RequiredCount { get; private set; }
    public int CurrentCount { get; private set; }

    public event Action OnTaskCompleted;

    /// <summary>
    /// Initializes the task and refreshes UI.
    /// </summary>
    public void InitTask(BrickColor color, int requiredCount)
    {
        ClearStoredBricks();

        CurrentColor = color;
        RequiredCount = requiredCount;
        CurrentCount = 0;
        UpdateTaskText();
        // TODO: Update zone visuals to match CurrentColor
    }

    /// <summary>
    /// Adds bricks to the task with movement into the zone. Counts increase as each brick arrives.
    /// </summary>
    public void AddBricks(List<Brick> bricks)
    {
        if (bricks == null || bricks.Count == 0)
        {
            return;
        }

        StartCoroutine(MoveBricksToZone(bricks));
    }

    /// <summary>
    /// Adds a raw amount without animation (fallback helper).
    /// </summary>
    public void AddBricks(int amount)
    {
        CurrentCount += amount;
        UpdateTaskText();
        // TODO: Add punch animation for feedback

        if (IsCompleted())
        {
            CompleteTask();
        }
    }

    public bool IsCompleted()
    {
        return CurrentCount >= RequiredCount;
    }

    private void CompleteTask()
    {
        // TODO: Play completion animation
        OnTaskCompleted?.Invoke();
    }

    private void UpdateTaskText()
    {
        if (_taskText != null)
        {
            _taskText.text = $"[{CurrentColor}: {CurrentCount}/{RequiredCount}]";
        }
    }

    private IEnumerator MoveBricksToZone(List<Brick> bricks)
    {
        int startIndex = CurrentCount;
        bool completionTriggered = IsCompleted();

        void OnBrickArrived(Brick arrivedBrick)
        {
            CurrentCount++;
            _activeBricks.Add(arrivedBrick);
            UpdateTaskText();

            if (!completionTriggered && IsCompleted())
            {
                completionTriggered = true;
                CompleteTask();
            }
        }

        bricks.Reverse();
        for (int i = 0; i < bricks.Count; i++)
        {
            var brick = bricks[i];
            int targetIndex = startIndex + i;

            if (brick.Instance != null)
            {
                var brickTransform = brick.Instance.transform;
                brickTransform.SetParent(_stackAnchor == null ? transform : _stackAnchor);

                var targetPosition = GetTargetPosition(targetIndex);
                StartCoroutine(MoveBrick(brickTransform, targetPosition, () => OnBrickArrived(brick)));
            }

            if (brick.Instance == null)
            {
                OnBrickArrived(brick);
            }

            if (_moveStaggerDelay > 0f)
            {
                yield return new WaitForSeconds(_moveStaggerDelay);
            }
        }
    }

    public void ReflowActiveBricks()
    {
        StartCoroutine(ReflowBricksCoroutine());
    }

    private IEnumerator ReflowBricksCoroutine()
    {
        for (int i = 0; i < _activeBricks.Count; i++)
        {
            var brick = _activeBricks[i];
            if (brick.Instance == null)
            {
                continue;
            }

            var targetPosition = GetTargetPosition(i);
            StartCoroutine(MoveBrick(brick.Instance.transform, targetPosition));

            if (_moveStaggerDelay > 0f)
            {
                yield return new WaitForSeconds(_moveStaggerDelay);
            }
        }
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

        // sequence.Join(brickTransform
        //     .DORotateQuaternion(startingRotation * Quaternion.Euler(-_movementTiltAngle, 0f, 0f),
        //         _moveDuration * 0.45f)
        //     .SetLoops(2, LoopType.Yoyo)
        //     .SetEase(Ease.OutSine));

        sequence.Join(brickTransform.DORotate(Vector3.zero, _moveDuration));

        yield return sequence.WaitForCompletion();

        brickTransform.position = targetPosition;
        //brickTransform.rotation = startingRotation;

        onComplete?.Invoke();
    }

    private Vector3 GetTargetPosition(int index)
    {
        if (_slotPositions.Count > 0 && index < _slotPositions.Count)
        {
            return _slotPositions[index].position;
        }

        var anchor = _stackAnchor == null ? transform : _stackAnchor;
        return anchor.position + Vector3.up * (_brickHeightSpacing * index);
    }

    private void ClearStoredBricks()
    {
        foreach (var brick in _activeBricks)
        {
            if (brick.Instance != null)
            {
                Destroy(brick.Instance);
            }
        }

        _activeBricks.Clear();
    }
}
