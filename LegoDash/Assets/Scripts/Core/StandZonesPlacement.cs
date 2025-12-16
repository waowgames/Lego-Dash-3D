using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Repositions active <see cref="StandController"/> instances into a centered formation.
/// </summary>
public class StandZonesPlacement : MonoBehaviour
{
    [Tooltip("Assign exactly 16 stand references in their desired visual order.")] [SerializeField]
    private StandController[] _stands = new StandController[16];

    [Header("Layout Settings")] [SerializeField]
    private float _spacingX = 2f;

    [SerializeField] private float _spacingZ = 2f;

    [Tooltip("Optional anchor for determining the layout center and orientation.")] [SerializeField]
    private Transform _centerAnchor;

    [Tooltip("Fallback center point when no anchor is provided.")] [SerializeField]
    private Vector3 _centerPoint = Vector3.zero;

    [Header("Behavior")] [Tooltip("Preserve each stand's current Y position when re-centering.")] [SerializeField]
    private bool _keepY = true;

    [Tooltip("Animate repositioning with DOTween when available.")] [SerializeField]
    private bool _animate = true;

    [SerializeField] private float _animDuration = 0.35f;

    private Vector3[] _originalLocalPositions;
    private bool _hasCachedOriginalPositions;

    private void Awake()
    {
        CacheOriginalPositions();
        Refresh();
        Events.LevelStarted += RefreshOnLevelEnd;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(0.1f);
        Refresh();
    }

    private void OnValidate()
    {
        // Keep the preview responsive in the editor without heavy runtime behavior.
        if (!Application.isPlaying)
        {
            CacheOriginalPositions();
            Refresh();
        }
    }

    private void RefreshOnLevelEnd(LevelStartPayload l)
    {
        Refresh();
    }

    /// <summary>
    /// Rebuild the layout based on currently active stands.
    /// </summary>
    [Button]
    [ContextMenu("Refresh")]
    public void Refresh()
    {
        if (_stands == null || _stands.Length == 0)
        {
            return;
        }

        CacheOriginalPositions();

        var activeStands = new List<StandController>(_stands.Length);
        for (int i = 0; i < _stands.Length; i++)
        {
            var stand = _stands[i];
            stand.GetComponent<Collider>().enabled = false;
            if (stand != null && stand.Model.localScale != Vector3.zero)
            {
                activeStands.Add(stand);
                stand.GetComponent<Collider>().enabled = true;
            }
        }

        int activeCount = activeStands.Count;

        if (activeCount == 0)
        {
            return;
        }

        if (activeCount == _stands.Length && _hasCachedOriginalPositions)
        {
            RestoreOriginalPositions();
            return;
        }

        var targets = ComputeTargets(activeCount);

        for (int i = 0; i < activeCount; i++)
        {
            var stand = activeStands[i];
            if (stand == null)
            {
                continue;
            }

            ApplyPosition(stand.transform, targets[i]);
        }
    }

    private void CacheOriginalPositions()
    {
        if (_hasCachedOriginalPositions || _stands == null)
        {
            return;
        }

        bool missingStand = false;
        _originalLocalPositions = new Vector3[_stands.Length];
        for (int i = 0; i < _stands.Length; i++)
        {
            var stand = _stands[i];
            if (stand == null)
            {
                missingStand = true;
                continue;
            }

            _originalLocalPositions[i] = stand.transform.localPosition;
        }

        // Only lock the cache once all stands are assigned to avoid persisting placeholder values.
        _hasCachedOriginalPositions = !missingStand;
    }

    private void RestoreOriginalPositions()
    {
        for (int i = 0; i < _stands.Length; i++)
        {
            var stand = _stands[i];
            if (stand == null)
            {
                continue;
            }

            Vector3 targetLocal = _originalLocalPositions != null && i < _originalLocalPositions.Length
                ? _originalLocalPositions[i]
                : stand.transform.localPosition;

            if (_keepY)
            {
                targetLocal.y = stand.transform.localPosition.y;
            }

            MoveTransform(stand.transform, ToWorldPosition(stand.transform.parent, targetLocal));
        }
    }

    private List<Vector3> ComputeTargets(int activeCount)
    {
        var targets = new List<Vector3>(activeCount);
        Vector3 centerWorld = _centerAnchor != null ? _centerAnchor.position : _centerPoint;
        bool singleRow = activeCount <= 8;

        if (activeCount <= 1)
        {
            targets.Add(centerWorld);
            return targets;
        }

        if (singleRow)
        {
            float width = (activeCount - 1) * _spacingX;
            float startX = -width * 0.5f;

            for (int i = 0; i < activeCount; i++)
            {
                float x = startX + i * _spacingX;
                targets.Add(OffsetFromCenter(centerWorld, new Vector3(x, 0f, 0f)));
            }

            return targets;
        }

        int firstRowCount = Mathf.CeilToInt(activeCount / 2f);
        int secondRowCount = activeCount - firstRowCount;
        float rowOffset = _spacingZ * 0.5f;

        for (int i = 0; i < activeCount; i++)
        {
            bool isFirstRow = i < firstRowCount;
            int indexInRow = isFirstRow ? i : i - firstRowCount;
            int currentRowCount = isFirstRow ? firstRowCount : secondRowCount;

            float width = (currentRowCount - 1) * _spacingX;
            float startX = currentRowCount > 1 ? -width * 0.5f : 0f;
            float x = startX + indexInRow * _spacingX;
            float z = isFirstRow ? -rowOffset : rowOffset;

            targets.Add(OffsetFromCenter(centerWorld, new Vector3(x, 0f, z)));
        }

        return targets;
    }

    private Vector3 OffsetFromCenter(Vector3 centerWorld, Vector3 localOffset)
    {
        if (_centerAnchor != null)
        {
            return _centerAnchor.TransformPoint(localOffset);
        }

        return centerWorld + localOffset;
    }

    private Vector3 ToWorldPosition(Transform parent, Vector3 localPosition)
    {
        if (parent == null)
        {
            return localPosition;
        }

        return parent.TransformPoint(localPosition);
    }

    private void ApplyPosition(Transform targetTransform, Vector3 targetWorldPosition)
    {
        if (targetTransform == null)
        {
            return;
        }

        if (_keepY)
        {
            targetWorldPosition.y = targetTransform.position.y;
        }

        MoveTransform(targetTransform, targetWorldPosition);
    }

    private void MoveTransform(Transform targetTransform, Vector3 targetWorldPosition)
    {
        if (_animate && Application.isPlaying)
        {
            targetTransform.DOMove(targetWorldPosition, _animDuration);
        }
        else
        {
            targetTransform.position = targetWorldPosition;
        }
    }
}