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
    private Renderer _bodyRenderer;

    [SerializeField]
    private GameObject _activeHighlight;

    [SerializeField]
    private List<TaskCarMaterialMapping> _colorMaterials = new();

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
    private Dictionary<BrickColor, Material> _materialLookup;
    private Color _baseColor = Color.white;

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
        BuildMaterialLookup();
        ApplyColor();
        ClearStoredBricks();
    }

    public void SetActive(bool isActive)
    {
        if (_activeHighlight != null)
        {
            _activeHighlight.SetActive(isActive);
        }

        if (_bodyRenderer != null)
        {
            var targetColor = isActive ? _baseColor : _baseColor * 0.75f;
            _bodyRenderer.material.DOColor(targetColor, 0.2f);
        }
    }

    public bool CanAcceptBrickColor(BrickColor color)
    {
        return color == TaskColor && CurrentBrickCount < RequiredBrickCount;
    }

    public int RemainingNeed()
    {
        return Mathf.Max(0, RequiredBrickCount - CurrentBrickCount);
    }

    public void AddBricks(List<Brick> bricks)
    {
        if (bricks == null || bricks.Count == 0)
        {
            return;
        }

        int capacity = RemainingNeed();
        if (_brickSlots.Count > 0)
        {
            capacity = Mathf.Min(capacity, _brickSlots.Count - CurrentBrickCount);
        }
        if (capacity <= 0)
        {
            return;
        }

        var acceptedBricks = new List<Brick>();
        foreach (var brick in bricks)
        {
            if (!CanAcceptBrickColor(brick.Color))
            {
                continue;
            }

            acceptedBricks.Add(brick);
            if (acceptedBricks.Count >= capacity)
            {
                break;
            }
        }

        if (acceptedBricks.Count == 0)
        {
            return;
        }

        StartCoroutine(MoveBricksToCar(acceptedBricks));
    }

    private void BuildMaterialLookup()
    {
        _materialLookup = new Dictionary<BrickColor, Material>();
        foreach (var mapping in _colorMaterials)
        {
            if (mapping.Material == null)
            {
                continue;
            }

            _materialLookup[mapping.Color] = mapping.Material;
        }
    }

    private void ApplyColor()
    {
        if (_bodyRenderer == null)
        {
            return;
        }

        if (_materialLookup != null && _materialLookup.TryGetValue(TaskColor, out var mat))
        {
            _bodyRenderer.sharedMaterial = mat;
        }

        _baseColor = _bodyRenderer.material.color;
    }

    private IEnumerator MoveBricksToCar(List<Brick> bricks)
    {
        bricks.Reverse();
        int startIndex = CurrentBrickCount;
        bool completionTriggered = IsCompleted;

        void OnBrickArrived(Brick arrivedBrick)
        {
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
        brickTransform.rotation = startingRotation;

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
    }
}

[Serializable]
public struct TaskCarMaterialMapping
{
    public BrickColor Color;
    public Material Material;
}
