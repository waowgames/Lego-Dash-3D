using UnityEngine;

/// <summary>
/// Spawns and manages the active Construction instance for the current level.
/// </summary>
public class ConstructionManager : MonoBehaviour
{
    [SerializeField] private Construction _fallbackConstructionPrefab;
    [SerializeField] private Transform _constructionParent;

    private Construction _activeConstruction;

    public Construction ActiveConstruction => _activeConstruction;

    public Construction InitializeForLevel(LevelConfig config)
    {
        ClearActiveConstruction();

        var prefab = config != null && config.ConstructionPrefab != null
            ? config.ConstructionPrefab
            : _fallbackConstructionPrefab;

        if (prefab == null)
        {
            Debug.LogWarning("Aktif edilecek Construction prefab'ı bulunamadı.");
            return null;
        }

        _activeConstruction = Instantiate(prefab, _constructionParent == null ? transform : _constructionParent);
        _activeConstruction.InitializeForLevel(config);
        return _activeConstruction;
    }

    private void ClearActiveConstruction()
    {
        if (_activeConstruction != null)
        {
            Destroy(_activeConstruction.gameObject);
            _activeConstruction = null;
        }
    }
}
