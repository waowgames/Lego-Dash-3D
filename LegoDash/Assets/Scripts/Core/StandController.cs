using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Controls a vertical stack of bricks on a stand. Top-most brick is at the end of the list.
/// </summary>
public class StandController : MonoBehaviour
{
     
    [SerializeField]
    private Transform _brickParent;

    [Header("Model")]
    [Tooltip("Standın model temsilcisi. Atamayı sahneden yapın.")]
    [SerializeField]
    private Transform _model;

    [SerializeField]
    private float _modelScaleDuration = 0.25f;

    [Tooltip("Vertical spacing applied between bricks when building the stand.")]
    [SerializeField]
    private float _brickHeightSpacing = 0.25f;

    private readonly List<Brick> _bricks = new();
    private Tween _modelScaleTween;

    public Transform Model => _model;
    /// <summary>
    /// Builds the stand using the provided brick colors and prefab mapping.
    /// </summary>
    public void BuildStand(IEnumerable<BrickColor> colors, Dictionary<BrickColor, GameObject> prefabs)
    {
        ClearBricks();

        foreach (var color in colors)
        {
            var prefab = prefabs != null && prefabs.TryGetValue(color, out var value) ? value : null;
            var instance = prefab == null ? new GameObject($"Brick_{color}") : Instantiate(prefab, _brickParent == null ? transform : _brickParent);
            if (instance.transform.parent == null)
            {
                instance.transform.SetParent(_brickParent == null ? transform : _brickParent);
            }

            _bricks.Add(new Brick(color, instance));
            PositionBrick(instance.transform, _bricks.Count - 1);
        }

        UpdateModelScale(false);
    }

    /// <summary>
    /// Returns the color of the top-most brick or null if no bricks exist.
    /// </summary>
    public BrickColor? PeekTopColor()
    {
        if (_bricks.Count == 0)
        {
            return null;
        }

        return _bricks[^1].Color;
    }

    /// <summary>
    /// Removes and returns all contiguous bricks that share the same color starting from the top.
    /// </summary>
    public List<Brick> PopTopGroup()
    {
        var result = new List<Brick>();

        if (_bricks.Count == 0)
        {
            return result;
        }

        var targetColor = _bricks[^1].Color;
        while (_bricks.Count > 0 && _bricks[^1].Color == targetColor)
        {
            var brick = _bricks[^1];
            _bricks.RemoveAt(_bricks.Count - 1);
            result.Add(brick);
        }

        // Preserve original top-first order for downstream systems.
        result.Reverse();

        UpdateModelScale(_bricks.Count == 0);
        return result;
    }

    /// <summary>
    /// Returns bricks back to the top of the stand, preserving their order.
    /// </summary>
    public void ReturnBricksToTop(List<Brick> bricks)
    {
        if (bricks == null || bricks.Count == 0)
        {
            return;
        }

        int startIndex = _bricks.Count;
        for (int i = 0; i < bricks.Count; i++)
        {
            var brick = bricks[i];
            var brickTransform = brick.Instance != null ? brick.Instance.transform : null;

            if (brickTransform != null)
            {
                brickTransform.SetParent(_brickParent == null ? transform : _brickParent);
                PositionBrick(brickTransform, startIndex + i);
            }

            _bricks.Add(brick);
        }

        UpdateModelScale(true);
    }

    /// <summary>
    /// Destroys existing brick instances and clears the stack.
    /// </summary>
    public void ClearBricks()
    {
        foreach (var brick in _bricks)
        {
            if (brick.Instance != null)
            {
                Destroy(brick.Instance);
            }
        }

        _bricks.Clear();

        UpdateModelScale(false);
    }

    public int BrickCount => _bricks.Count;

    private void OnMouseDown()
    {
        // Relay click/tap interactions to the central game manager.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandleStandTapped(this);
        }
    }

    private void OnDestroy()
    {
        if (_modelScaleTween != null && _modelScaleTween.IsActive())
        {
            _modelScaleTween.Kill();
        }
    }

    /// <summary>
    /// Places a brick at the appropriate height based on its index in the stack.
    /// </summary>
    private void PositionBrick(Transform brickTransform, int index)
    {
        if (brickTransform == null)
        {
            return;
        }

        // Lower indices are closer to the base, last index represents the top-most brick.
        brickTransform.localPosition = Vector3.up * (_brickHeightSpacing * index);
    }

    private void UpdateModelScale(bool animate)
    {
        if (_model == null)
        {
            return;
        }

        var targetScale = _bricks.Count > 0 ? Vector3.one : Vector3.zero;

        if (_modelScaleTween != null && _modelScaleTween.IsActive())
        {
            _modelScaleTween.Kill();
        }

        if (animate)
        {
            _modelScaleTween = _model.DOScale(targetScale, _modelScaleDuration);
        }
        else
        {
            _model.localScale = targetScale;
        }
    }
}
