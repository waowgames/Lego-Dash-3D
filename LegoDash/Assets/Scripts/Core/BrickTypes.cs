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
    private bool _hasCachedMaterials;

    public Brick(BrickColor color, GameObject instance)
    {
        Color = color;
        Instance = instance;
        CacheRendererStates();
    }

    public void SetLocked(bool locked, Material lockedMaterial, Sprite lockSprite, Vector3 iconOffset, float iconScale)
    {
        if (IsLocked == locked)
        {
            return;
        }

        IsLocked = locked;

        if (locked)
        {
            ApplyLockedVisuals(lockedMaterial, lockSprite, iconOffset, iconScale);
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

            _rendererStates.Add(new BrickRendererState(renderer));
        }

        _hasCachedMaterials = _rendererStates.Count > 0;
    }

    private void ApplyLockedVisuals(Material lockedMaterial, Sprite lockSprite, Vector3 iconOffset, float iconScale)
    {
        foreach (var state in _rendererStates)
        {
            if (!_hasCachedMaterials)
            {
                continue;
            }

            if (lockedMaterial != null)
            {
                state.Renderer.sharedMaterials = state.BuildLockedMaterials(lockedMaterial);
            }
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
            if (!_hasCachedMaterials)
            {
                continue;
            }

            state.Renderer.sharedMaterials = state.OriginalMaterials;
        }

        if (_lockIconInstance != null)
        {
            Object.Destroy(_lockIconInstance);
            _lockIconInstance = null;
        }
    }

    private sealed class BrickRendererState
    {
        public Renderer Renderer { get; }
        public Material[] OriginalMaterials { get; }

        public BrickRendererState(Renderer renderer)
        {
            Renderer = renderer;
            OriginalMaterials = renderer != null ? renderer.sharedMaterials : System.Array.Empty<Material>();
        }

        public Material[] BuildLockedMaterials(Material lockedMaterial)
        {
            if (OriginalMaterials == null || OriginalMaterials.Length == 0)
            {
                return new[] { lockedMaterial };
            }

            var lockedMaterials = new Material[OriginalMaterials.Length];
            for (int i = 0; i < lockedMaterials.Length; i++)
            {
                lockedMaterials[i] = lockedMaterial;
            }

            return lockedMaterials;
        }
    }
}
