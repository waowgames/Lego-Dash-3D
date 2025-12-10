using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Tracks the current task requirements and forwards completion events.
/// </summary>
public class TaskZoneController : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _taskText;

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
    /// Adds bricks to the task and triggers completion when goal reached.
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
}
