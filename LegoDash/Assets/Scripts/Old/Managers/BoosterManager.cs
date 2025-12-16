using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum BoosterType
{
    None,
    AutoFill,
    ExtraSlots,
    Custom
}

[System.Serializable]
public class BoosterActivatedEvent : UnityEvent<BoosterType> { }

public class BoosterManager : MonoBehaviour
{
    public static BoosterManager Instance { get; private set; }

    [SerializeField] private BoosterActivatedEvent onBoosterActivated;

    private readonly HashSet<BoosterType> activeBoosters = new HashSet<BoosterType>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("BoosterManager: Another instance detected, destroying duplicate.");
            Destroy(this);
            return;
        }

        Instance = this;
    }

    public bool IsBoosterActive(BoosterType type)
    {
        if (!IsSingleUseBooster(type))
        {
            return false;
        }

        return activeBoosters.Contains(type);
    }

    public bool IsSingleUseBooster(BoosterType type)
    {
        return type == BoosterType.ExtraSlots;
    }

    public void ActivateBooster(BoosterType type)
    {
        if (type == BoosterType.None)
        {
            Debug.LogWarning("BoosterManager: Cannot activate 'None' booster type.");
            return;
        }

        bool isSingleUse = IsSingleUseBooster(type);

        if (isSingleUse && IsBoosterActive(type))
        {
            Debug.Log($"BoosterManager: Booster '{type}' is already active.");
            return;
        }

        if (isSingleUse)
        {
            activeBoosters.Add(type);
        }
        else
        {
            activeBoosters.Remove(type);
        }
        Debug.Log($"BoosterManager: Activated booster '{type}'.");
        onBoosterActivated?.Invoke(type);
    }

    public void ResetBooster(BoosterType type)
    {
        activeBoosters.Remove(type);
    }

    public void ResetAllBoosters()
    {
        activeBoosters.Clear();
    }
}
