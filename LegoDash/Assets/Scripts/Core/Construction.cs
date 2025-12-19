using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Solo.MOST_IN_ONE;
using UnityEngine;

/// <summary>
/// Handles spawning the construction prefab and revealing its pieces
/// using incoming task bricks.
/// </summary>
public class Construction : MonoBehaviour
{
    [Header("Setup")] [SerializeField] private Transform _builtObjectRootOverride;

    [Tooltip("BuiltObjectRoot child adı. Override atanmadıysa buradan aranır.")] [SerializeField]
    private string _builtObjectRootName = "BuiltObjectRoot";

    [Header("Animation")] [SerializeField] private float _brickTravelDuration = 0.5f;

    [SerializeField] private float _brickJumpPower = 1f;

    [SerializeField] private float _brickStagger = 0.05f;

    [SerializeField] private Ease _brickTravelEase = Ease.OutQuad;

    [Header("Piece Reveal Animation")] [SerializeField]
    private float _piecePunchDuration = 0.35f;

    [SerializeField] private float _piecePunchStrength = 0.2f;

    [SerializeField] private int _piecePunchVibrato = 10;

    [SerializeField] private float _piecePunchElasticity = 0.8f;

    private Transform _builtObjectRoot;
    private readonly List<Transform> _pieces = new();
    private readonly Dictionary<Transform, Vector3> _pieceBaseScales = new();
    private int _nextPieceIndex;
    private bool _completionBroadcasted;

    public bool IsComplete => _builtObjectRoot != null && _nextPieceIndex >= _pieces.Count;

    public event Action OnConstructionCompleted;

    public void InitializeForLevel(LevelConfig config)
    {
        _pieces.Clear();
        _pieceBaseScales.Clear();
        _builtObjectRoot = null;
        _nextPieceIndex = 0;
        _completionBroadcasted = false;

        _builtObjectRoot = ResolveBuiltObjectRoot(transform);

        if (_builtObjectRoot == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("BuiltObjectRoot bulunamadı.");
#endif
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
        int piecesToOpen = Mathf.Min(availablePieces, bricks.Count);
        int pieceIndex = _nextPieceIndex;
        int openedPieces = 0;
        int completedBricks = 0;

        for (int i = 0; i < bricks.Count; i++)
        {
            var brick = bricks[i];
            Transform targetPiece = openedPieces + i < piecesToOpen ? _pieces[pieceIndex + i] : null;
            float delay = _brickStagger * i;

            StartCoroutine(AnimateBrickTransferWithDelay(brick, targetPiece, delay, () =>
            {
                if (targetPiece != null)
                {
                    targetPiece.gameObject.SetActive(true);
                    MOST_HapticFeedback.GenerateWithCooldown(MOST_HapticFeedback.HapticTypes.Selection,.02f);
                    PlayPieceRevealAnimation(targetPiece);
                    SoundManager.Instance.PlaySfx(GameManager.Instance.brickPlacementSound);
                    Instantiate(GameManager.Instance.particlePoof, targetPiece.position, Quaternion.identity);
                    openedPieces++;
                }

                completedBricks++;
            }));
        }

        while (completedBricks < bricks.Count)
        {
            yield return null;
        }

        _nextPieceIndex += openedPieces;
        CheckForCompletion();
    }

    public IEnumerator PlayCompletionCelebration(float pieceDelay, float punchStrength, float duration)
    {
        if (_builtObjectRoot == null)
        {
            yield break;
        }

        if (_pieces.Count == 0)
        {
            CachePieces();
        }

        foreach (var piece in _pieces)
        {
            if (piece == null)
            {
                continue;
            }

            piece.gameObject.SetActive(true);
            piece.DOKill();

            if (_pieceBaseScales.TryGetValue(piece, out var baseScale))
            {
                piece.localScale = baseScale;
            }

            piece
                .DOPunchScale(Vector3.one * punchStrength, duration, _piecePunchVibrato, _piecePunchElasticity)
                .SetEase(Ease.OutQuad);

            if (pieceDelay > 0f)
            {
                yield return new WaitForSeconds(pieceDelay);
            }
        }
    }

    private void CachePieces()
    {
        _pieces.Clear();
        foreach (Transform child in _builtObjectRoot)
        {
            _pieces.Add(child);
            _pieceBaseScales[child] = child.localScale;
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

    private IEnumerator AnimateBrickTransferWithDelay(Brick brick, Transform targetPiece, float delay,
        Action onComplete)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (brick == null || brick.Instance == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        var brickTransform = brick.Instance.transform;
        var destination = targetPiece != null
            ? targetPiece.position
            : (_builtObjectRoot != null ? _builtObjectRoot.position : brickTransform.position);
        brickTransform.SetParent(_builtObjectRoot == null ? brickTransform.parent : _builtObjectRoot);
        brickTransform.DOKill();

        var tween = brickTransform
            .DOJump(destination, _brickJumpPower, 1, _brickTravelDuration)
            .SetEase(_brickTravelEase);

        var rotateTravelDuration = _brickTravelDuration / 100 * 90;

        brickTransform.DORotate(Vector3.zero, rotateTravelDuration);

        yield return tween.WaitForCompletion();

        brickTransform.position = destination;
        brick.Instance.SetActive(false);
        onComplete?.Invoke();
    }

    private void PlayPieceRevealAnimation(Transform piece)
    {
        if (piece == null)
        {
            return;
        }

        piece.DOKill();

        if (_pieceBaseScales.TryGetValue(piece, out var baseScale))
        {
            piece.localScale = baseScale;
        }

        piece
            .DOPunchScale(Vector3.one * _piecePunchStrength, _piecePunchDuration, _piecePunchVibrato,
                _piecePunchElasticity)
            .SetEase(Ease.OutQuad);
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