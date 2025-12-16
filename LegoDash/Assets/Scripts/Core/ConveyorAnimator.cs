using UnityEngine;

public class ConveyorAnimator : MonoBehaviour
{
    [SerializeField]
    private TaskCarManager _taskCarManager;

    [SerializeField]
    private Renderer _conveyorRenderer;

    [SerializeField]
    private float _scrollSpeed = 0.5f;

    private Material _materialInstance;
    private float _currentYOffset;
    private bool _isMoving;

    private void Awake()
    {
        if (_conveyorRenderer != null)
        {
            _materialInstance = _conveyorRenderer.material;
            if (_materialInstance != null)
            {
                _currentYOffset = _materialInstance.mainTextureOffset.y;
            }
        }
    }

    private void OnEnable()
    {
        SubscribeToManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromManager();
    }

    private void Update()
    {
        if (!_isMoving || _materialInstance == null)
        {
            return;
        }

        _currentYOffset += _scrollSpeed * Time.deltaTime;
        var offset = _materialInstance.mainTextureOffset;
        offset.y = _currentYOffset;
        _materialInstance.mainTextureOffset = offset;
    }

    private void SubscribeToManager()
    {
        if (_taskCarManager == null)
        {
            _taskCarManager = FindObjectOfType<TaskCarManager>();
        }

        if (_taskCarManager != null)
        {
            _taskCarManager.OnConvoyMovementStarted += HandleConvoyMovementStarted;
            _taskCarManager.OnConvoyMovementCompleted += HandleConvoyMovementCompleted;
        }
    }

    private void UnsubscribeFromManager()
    {
        if (_taskCarManager != null)
        {
            _taskCarManager.OnConvoyMovementStarted -= HandleConvoyMovementStarted;
            _taskCarManager.OnConvoyMovementCompleted -= HandleConvoyMovementCompleted;
        }
    }

    private void HandleConvoyMovementStarted()
    {
        _isMoving = true;
    }

    private void HandleConvoyMovementCompleted()
    {
        _isMoving = false;
    }
}
