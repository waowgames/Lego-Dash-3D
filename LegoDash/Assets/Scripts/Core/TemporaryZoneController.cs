using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Stores non-matching bricks until a later task consumes them.
/// </summary>
public class TemporaryZoneController : MonoBehaviour
{
    [SerializeField]
    private int _maxCapacity = 7;

    [Header("Animation")]
    [Tooltip("Optional anchor used as the origin for stacking stored bricks.")]
    [SerializeField]
    private Transform _storageAnchor;

    [Tooltip("Optional explicit slot positions used to lay out stored bricks (e.g. horizontally).")]
    [SerializeField]
    private List<Transform> _slotPositions = new();

    [Tooltip("Seconds it takes for a brick to travel into the temporary zone.")]
    [SerializeField]
    private float _moveDuration = 0.35f;

    [Tooltip("Jump height used while bricks travel into the temporary zone.")]
    [SerializeField]
    private float _jumpPower = 0.75f;

    [Tooltip("How much the brick tilts while moving.")]
    [SerializeField]
    private float _movementTiltAngle = 45f;

    [Tooltip("Easing used for the incoming brick jump animation.")]
    [SerializeField]
    private Ease _moveEase = Ease.OutCubic;

    [Tooltip("Vertical spacing between stored bricks.")]
    [SerializeField]
    private float _brickHeightSpacing = 0.25f;

    private readonly List<Brick> _storedBricks = new();

    public int MaxCapacity => _maxCapacity;
    public int CurrentCount => _storedBricks.Count;

    /// <summary>
    /// Returns false if adding the specified amount would overflow capacity.
    /// </summary>
    public bool CanAccept(int amount)
    {
        return CurrentCount + amount <= GetCapacityLimit();
    }

    public bool AddBricks(List<Brick> bricks)
    {
        if (!CanAccept(bricks.Count))
        {
            return false;
        }

        int startIndex = _storedBricks.Count;
        _storedBricks.AddRange(bricks);
        StartCoroutine(MoveBricksToStorage(bricks, startIndex));
        return true;
    }

    /// <summary>
    /// Extracts bricks matching the requested color from storage.
    /// </summary>
    public List<Brick> ExtractBricksOfColor(BrickColor color, int maxCount = int.MaxValue)
    {
        if (maxCount <= 0)
        {
            return new List<Brick>();
        }

        var extracted = new List<Brick>();

        for (int i = 0; i < _storedBricks.Count && extracted.Count < maxCount; i++)
        {
            var brick = _storedBricks[i];
            if (brick.Color != color)
            {
                continue;
            }

            extracted.Add(brick);
            _storedBricks.RemoveAt(i);
            i--;
        }

        if (extracted.Count > 0)
        {
            StartCoroutine(ReflowStoredBricks());
        }

        return extracted;
    }

    public void ResetZone()
    {
        foreach (var brick in _storedBricks)
        {
            if (brick.Instance != null)
            {
                Destroy(brick.Instance);
            }
        }

        _storedBricks.Clear();
    }

    public void SetCapacity(int capacity)
    {
        _maxCapacity = Mathf.Max(1, capacity);
    }

    private IEnumerator MoveBricksToStorage(List<Brick> bricks, int startIndex)
    {
        bricks.Reverse();
        // Move the incoming bricks to their stacked positions, preserving arrival order.
        for (int i = 0; i < bricks.Count; i++)
        {
            var brick = bricks[i];
            var targetIndex = startIndex + i;

            if (brick.Instance != null)
            {
                var brickTransform = brick.Instance.transform;
                brickTransform.SetParent(_storageAnchor == null ? transform : _storageAnchor);
                var targetPosition = GetTargetPosition(targetIndex);
                yield return StartCoroutine(MoveBrick(brickTransform, targetPosition));
            }
        }
    }

    private IEnumerator MoveBrick(Transform brickTransform, Vector3 targetPosition)
    {
        brickTransform.DOKill();
        var startingRotation = brickTransform.rotation;

        var sequence = DOTween.Sequence();
        sequence.Join(brickTransform
            .DOJump(targetPosition, _jumpPower, 1, _moveDuration)
            .SetEase(_moveEase));

        sequence.Join(brickTransform.DORotate(new Vector3(0, 0, -90), _moveDuration));
        // sequence.Join(brickTransform
        //     .DORotateQuaternion(startingRotation * Quaternion.Euler(-_movementTiltAngle, 0f, 0f),
        //         _moveDuration * 0.45f)
        //     .SetLoops(2, LoopType.Yoyo)
        //     .SetEase(Ease.OutSine));

        yield return sequence.WaitForCompletion();

        brickTransform.position = targetPosition;
      //  brickTransform.rotation = startingRotation;
    }

    private IEnumerator ReflowStoredBricks()
    {
        for (int i = 0; i < _storedBricks.Count; i++)
        {
            var brick = _storedBricks[i];
            if (brick.Instance == null)
            {
                continue;
            }

            var targetPosition = GetTargetPosition(i);
            yield return StartCoroutine(MoveBrick(brick.Instance.transform, targetPosition));
        }
    }

    private Vector3 GetTargetPosition(int index)
    {
        if (_slotPositions.Count > 0 && index < _slotPositions.Count)
        {
            return _slotPositions[index].position;
        }

        var anchor = _storageAnchor == null ? transform : _storageAnchor;
        return anchor.position + Vector3.up * (_brickHeightSpacing * index);
    }

    private int GetCapacityLimit()
    {
        if (_slotPositions.Count > 0)
        {
            return Mathf.Min(_maxCapacity, _slotPositions.Count);
        }

        return _maxCapacity;
    }
}
