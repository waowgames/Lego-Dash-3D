using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// Extracts all bricks matching the requested color from storage.
    /// </summary>
    public List<Brick> ExtractBricksOfColor(BrickColor color)
    {
        var extracted = _storedBricks.Where(brick => brick.Color == color).ToList();
        _storedBricks.RemoveAll(brick => brick.Color == color);
        return extracted;
    }

    private IEnumerator MoveBricksToStorage(List<Brick> bricks, int startIndex)
    {
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
        var startPosition = brickTransform.position;
        float elapsed = 0f;

        while (elapsed < _moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _moveDuration);
            brickTransform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        brickTransform.position = targetPosition;
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
