using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    [Tooltip("Optional parent used to auto-discover additional slot positions.")]
    [SerializeField]
    private Transform _slotPositionsRoot;

    [Tooltip("Seconds it takes for a brick to travel into the temporary zone.")]
    [SerializeField]
    private float _moveDuration = 0.35f;

    [Tooltip("Delay between starting each brick's movement animation.")]
    [SerializeField]
    private float _moveStaggerDelay = 0.05f;

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

    private readonly List<Transform> _addedSlots = new();
    private readonly List<Brick> _storedBricks = new();
    private int _baseCapacity;
    private int _baseSlotCount;
    private List<Transform> _orderedSlotPositions = new();

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
        RemoveExtraSlots();
        RestoreOriginalCapacity();

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
        _baseCapacity = Mathf.Max(1, capacity);
        _maxCapacity = _baseCapacity + _addedSlots.Count;
    }

    public void RestoreOriginalCapacity()
    {
        _maxCapacity = Mathf.Max(1, _baseCapacity);
    }

    public bool AddExtraSlots(int count)
    {
        if (count <= 0 || _addedSlots.Count > 0)
        {
            return false;
        }

        CacheOrderedSlots();

        var availableSlots = _orderedSlotPositions
            .Where(slot => slot != null && !_slotPositions.Contains(slot))
            .ToList();

        if (availableSlots.Count == 0)
        {
            Debug.LogWarning("TemporaryZoneController: No spare slot positions available for expansion.");
            return false;
        }

        int toAdd = Mathf.Clamp(count, 0, availableSlots.Count);
        var slotsToAppend = availableSlots.Take(toAdd).ToList();

        _slotPositions.AddRange(slotsToAppend);
        _addedSlots.AddRange(slotsToAppend);

        _maxCapacity = _baseCapacity + _addedSlots.Count;
        return _addedSlots.Count > 0;
    }

    public void RemoveExtraSlots()
    {
        if (_addedSlots.Count == 0 && _slotPositions.Count <= _baseSlotCount)
        {
            return;
        }

        int slotLimit = _baseSlotCount > 0 ? _baseSlotCount : _slotPositions.Count;
        int targetCapacity = Mathf.Max(0, Mathf.Min(_baseCapacity, slotLimit));
        if (_storedBricks.Count > targetCapacity)
        {
            for (int i = targetCapacity; i < _storedBricks.Count; i++)
            {
                var brick = _storedBricks[i];
                if (brick?.Instance != null)
                {
                    Destroy(brick.Instance);
                }
            }

            _storedBricks.RemoveRange(targetCapacity, _storedBricks.Count - targetCapacity);
        }

        if (_slotPositions.Count > _baseSlotCount)
        {
            _slotPositions.RemoveRange(_baseSlotCount, _slotPositions.Count - _baseSlotCount);
        }

        _addedSlots.Clear();
        RestoreOriginalCapacity();
        StartCoroutine(ReflowStoredBricks());
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
                StartCoroutine(MoveBrick(brickTransform, targetPosition));
            }

            if (_moveStaggerDelay > 0f)
            {
                yield return new WaitForSeconds(_moveStaggerDelay);
            }
        }
    }

    private IEnumerator MoveBrick(Transform brickTransform, Vector3 targetPosition)
    {
        if (brickTransform == null)
        {
            yield break;
        }

        brickTransform.DOKill();
        var startingRotation = brickTransform.rotation;
        Vibration.Vibrate();
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

        if (brickTransform == null)
        {
            yield break;
        }

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
            StartCoroutine(MoveBrick(brick.Instance.transform, targetPosition));

            if (_moveStaggerDelay > 0f)
            {
                yield return new WaitForSeconds(_moveStaggerDelay);
            }
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

    private void Awake()
    {
        _baseCapacity = Mathf.Max(1, _maxCapacity);
        _baseSlotCount = _slotPositions.Count;
        CacheOrderedSlots();
    }

    private void CacheOrderedSlots()
    {
        if (_slotPositionsRoot == null && _slotPositions.Count > 0)
        {
            _slotPositionsRoot = _slotPositions[0].parent;
        }

        _orderedSlotPositions.Clear();

        if (_slotPositionsRoot != null)
        {
            for (int i = 0; i < _slotPositionsRoot.childCount; i++)
            {
                _orderedSlotPositions.Add(_slotPositionsRoot.GetChild(i));
            }
        }
        else
        {
            _orderedSlotPositions.AddRange(_slotPositions);
        }
    }
}
