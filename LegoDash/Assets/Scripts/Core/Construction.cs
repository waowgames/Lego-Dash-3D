using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Handles spawning the construction prefab and revealing its pieces
/// using incoming task bricks.
/// </summary>
public class Construction : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField]
    private GameObject _defaultConstructionPrefab;

    [SerializeField]
    private Transform _constructionParent;

    [SerializeField]
    private Transform _builtObjectRootOverride;

    [Tooltip("BuiltObjectRoot child adı. Override atanmadıysa buradan aranır.")]
    [SerializeField]
    private string _builtObjectRootName = "BuiltObjectRoot";

    [Tooltip("Bir görev tamamlandığında açılacak parça sayısı.")]
    [SerializeField, Min(1)]
    private int _piecesPerTaskCompletion = 1;

    [Header("Animation")]
    [SerializeField]
    private float _brickTravelDuration = 0.5f;

    [SerializeField]
    private float _brickJumpPower = 1f;

    [SerializeField]
    private float _brickStagger = 0.05f;

    [SerializeField]
    private Ease _brickTravelEase = Ease.OutQuad;

    private GameObject _activeConstructionInstance;
    private Transform _builtObjectRoot;
    private readonly List<Transform> _pieces = new();
    private int _nextPieceIndex;
    private bool _completionBroadcasted;

    public bool IsComplete => _builtObjectRoot != null && _nextPieceIndex >= _pieces.Count;

    public event Action OnConstructionCompleted;

    public void InitializeForLevel(LevelConfig config)
    {
        if (_activeConstructionInstance != null)
        {
            Destroy(_activeConstructionInstance);
            _activeConstructionInstance = null;
        }

        _pieces.Clear();
        _builtObjectRoot = null;
        _nextPieceIndex = 0;
        _completionBroadcasted = false;

        if (config != null)
        {
            _piecesPerTaskCompletion = Mathf.Max(1, config.PiecesPerTask);
        }

        var prefab = config != null && config.ConstructionPrefab != null
            ? config.ConstructionPrefab
            : _defaultConstructionPrefab;

        if (prefab == null)
        {
            Debug.LogWarning("Inşaat prefab'ı atanmadı.");
            return;
        }

        _activeConstructionInstance = Instantiate(prefab, _constructionParent == null ? transform : _constructionParent);
        _builtObjectRoot = ResolveBuiltObjectRoot(_activeConstructionInstance.transform);

        if (_builtObjectRoot == null)
        {
            Debug.LogWarning("BuiltObjectRoot bulunamadı.");
            return;
        }

        CachePieces();
    }

    public IEnumerator BuildWithBricks(List<Brick> bricks)
    {
        if (_builtObjectRoot == null)
        {
            yield break;
        }

        if (bricks == null || bricks.Count == 0)
        {
            yield break;
        }

        int availablePieces = Mathf.Max(0, _pieces.Count - _nextPieceIndex);
        int piecesToOpen = Mathf.Min(_piecesPerTaskCompletion, availablePieces, bricks.Count);

        for (int i = 0; i < bricks.Count; i++)
        {
            var brick = bricks[i];
            Transform targetPiece = i < piecesToOpen ? _pieces[_nextPieceIndex + i] : null;
            if (brick == null || brick.Instance == null)
            {
                if (targetPiece != null)
                {
                    targetPiece.gameObject.SetActive(true);
                    _nextPieceIndex++;
                }

                continue;
            }

            yield return AnimateBrickTransfer(brick, targetPiece);

            if (targetPiece != null)
            {
                targetPiece.gameObject.SetActive(true);
                _nextPieceIndex++;
            }
        }

        CheckForCompletion();
    }

    private void CachePieces()
    {
        _pieces.Clear();
        foreach (Transform child in _builtObjectRoot)
        {
            _pieces.Add(child);
            child.gameObject.SetActive(false);
        }
    }

    private Transform ResolveBuiltObjectRoot(Transform root)
    {
        if (_builtObjectRootOverride != null)
        {
            return _builtObjectRootOverride;
        }

        if (root == null)
        {
            return null;
        }

        var found = root.Find(_builtObjectRootName);
        if (found != null)
        {
            return found;
        }

        return root.childCount > 0 ? root.GetChild(0) : null;
    }

    private IEnumerator AnimateBrickTransfer(Brick brick, Transform targetPiece)
    {
        if (brick == null || brick.Instance == null)
        {
            yield break;
        }

        var brickTransform = brick.Instance.transform;
        var destination = targetPiece != null ? targetPiece.position : (_builtObjectRoot != null ? _builtObjectRoot.position : brickTransform.position);
        brickTransform.SetParent(_builtObjectRoot == null ? brickTransform.parent : _builtObjectRoot);
        brickTransform.DOKill();

        var tween = brickTransform
            .DOJump(destination, _brickJumpPower, 1, _brickTravelDuration)
            .SetEase(_brickTravelEase);

        yield return tween.WaitForCompletion();

        brickTransform.position = destination;
        brick.Instance.SetActive(false);

        if (_brickStagger > 0f)
        {
            yield return new WaitForSeconds(_brickStagger);
        }
    }

    private void CheckForCompletion()
    {
        if (IsComplete)
        {
            if (!_completionBroadcasted)
            {
                _completionBroadcasted = true;
                OnConstructionCompleted?.Invoke();
            }
        }
    }
}
