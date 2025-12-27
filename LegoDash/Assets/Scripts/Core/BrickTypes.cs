using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Available brick colors for the LegoDash core mechanics.
/// </summary>
public enum BrickColor
{
    Blue,
    Red,
    Yellow,
    Purple,
    Green,
    Pink,
    Orange,
    
}

/// <summary>
/// Simple data holder for a spawned brick instance.
/// </summary>
public class Brick
{
    public BrickColor Color { get; }
    public GameObject Instance { get; }
    public bool IsLocked { get; private set; }

    private readonly List<BrickRendererState> _rendererStates = new();
    private GameObject _lockIconInstance;

    public Brick(BrickColor color, GameObject instance)
    {
        Color = color;
        Instance = instance;
        CacheRendererStates();
    }

    public void SetLocked(bool locked, Color lockedColor, Sprite lockSprite, Vector3 iconOffset, float iconScale)
    {
        if (IsLocked == locked)
        {
            return;
        }

        IsLocked = locked;

        if (locked)
        {
            ApplyLockedVisuals(lockedColor, lockSprite, iconOffset, iconScale);
        }
        else
        {
            RestoreVisuals();
        }
    }

    private void CacheRendererStates()
    {
        if (Instance == null)
        {
            return;
        }

        var renderers = Instance.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null || renderer.sharedMaterial == null)
            {
                continue;
            }

            if (TryGetColorProperty(renderer.sharedMaterial, out var colorProperty))
            {
                var originalColor = renderer.sharedMaterial.GetColor(colorProperty);
                _rendererStates.Add(new BrickRendererState(renderer, colorProperty, originalColor));
            }
        }
    }

    private void ApplyLockedVisuals(Color lockedColor, Sprite lockSprite, Vector3 iconOffset, float iconScale)
    {
        foreach (var state in _rendererStates)
        {
            state.PropertyBlock.Clear();
            state.PropertyBlock.SetColor(state.ColorProperty, lockedColor);
            state.Renderer.SetPropertyBlock(state.PropertyBlock);
        }

        if (lockSprite != null && Instance != null)
        {
            if (_lockIconInstance == null)
            {
                _lockIconInstance = new GameObject("LockIcon");
                _lockIconInstance.transform.SetParent(Instance.transform, false);
                _lockIconInstance.transform.localPosition = iconOffset;
                _lockIconInstance.transform.localRotation = Quaternion.identity;
                _lockIconInstance.transform.localScale = Vector3.one * iconScale;

                var spriteRenderer = _lockIconInstance.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = lockSprite;
                spriteRenderer.sortingOrder = 10;
            }
            else
            {
                _lockIconInstance.transform.localPosition = iconOffset;
                _lockIconInstance.transform.localScale = Vector3.one * iconScale;
            }
        }
    }

    private void RestoreVisuals()
    {
        foreach (var state in _rendererStates)
        {
            state.PropertyBlock.Clear();
            state.PropertyBlock.SetColor(state.ColorProperty, state.OriginalColor);
            state.Renderer.SetPropertyBlock(state.PropertyBlock);
        }

        if (_lockIconInstance != null)
        {
            Object.Destroy(_lockIconInstance);
            _lockIconInstance = null;
        }
    }

    private static bool TryGetColorProperty(Material material, out string property)
    {
        if (material.HasProperty("_BaseColor"))
        {
            property = "_BaseColor";
            return true;
        }

        if (material.HasProperty("_Color"))
        {
            property = "_Color";
            return true;
        }

        property = null;
        return false;
    }

    private sealed class BrickRendererState
    {
        public Renderer Renderer { get; }
        public string ColorProperty { get; }
        public Color OriginalColor { get; }
        public MaterialPropertyBlock PropertyBlock { get; }

        public BrickRendererState(Renderer renderer, string colorProperty, Color originalColor)
        {
            Renderer = renderer;
            ColorProperty = colorProperty;
            OriginalColor = originalColor;
            PropertyBlock = new MaterialPropertyBlock();
        }
    }
}
