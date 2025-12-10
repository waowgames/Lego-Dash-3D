using System;
using System.Collections;
using System.Collections.Generic;
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

    [Tooltip("Seconds it takes for a brick to travel into the task zone.")]
    [SerializeField]
    private float _moveDuration = 0.35f;

    [Tooltip("Vertical spacing between stacked bricks in the task zone.")]
    [SerializeField]
    private float _brickHeightSpacing = 0.25f;

    public BrickColor CurrentColor { get; private set; }
    public int RequiredCount { get; private set; }
    public int CurrentCount { get; private set; }

    public event Action OnTaskCompleted;

    /// <summary>
    /// Initializes the task and refreshes UI.
    /// </summary>
    public void InitTask(BrickColor color, int requiredCount)
    {
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

        for (int i = 0; i < bricks.Count; i++)
        {
            var brick = bricks[i];
            int targetIndex = startIndex + i;

            if (brick.Instance != null)
            {
                var brickTransform = brick.Instance.transform;
                brickTransform.SetParent(_stackAnchor == null ? transform : _stackAnchor);

                var targetPosition = GetTargetPosition(targetIndex);
                yield return StartCoroutine(MoveBrick(brickTransform, targetPosition));

                Destroy(brick.Instance);
            }

            CurrentCount++;
            UpdateTaskText();

            if (!completionTriggered && IsCompleted())
            {
                completionTriggered = true;
                CompleteTask();
            }
        }
    }

    private IEnumerator MoveBrick(Transform brickTransform, Vector3 targetPosition)
    {
        if (brickTransform == null)
        {
            yield break;
        }

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
        var anchor = _stackAnchor == null ? transform : _stackAnchor;
        return anchor.position + Vector3.up * (_brickHeightSpacing * index);
    }
}
