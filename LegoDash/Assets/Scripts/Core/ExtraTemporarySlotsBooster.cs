using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Booster that grants additional temporary storage slots for the remainder of the level.
/// </summary>
public class ExtraTemporarySlotsBooster : MonoBehaviour
{
    [SerializeField]
    private TemporaryZoneController _temporaryZone;

    [SerializeField]
    private int _extraSlotCount = 3;

    private bool _boosterActive;

    [SerializeField] private Transform modelToMove;
    

    private void OnEnable()
    {
        Events.LevelStarted += HandleLevelStarted;
        Events.LevelEnded += HandleLevelEnded;
    }

    private void OnDisable()
    {
        Events.LevelStarted -= HandleLevelStarted;
        Events.LevelEnded -= HandleLevelEnded;
    }

    [Button]
    public void ActivateExtraTemporarySlotsBooster()
    {
        if (_boosterActive)
        {
            return;
        }

        EnsureZoneReference();
        if (_temporaryZone == null)
        {
            Debug.LogWarning("ExtraTemporarySlotsBooster: Temporary zone not found.");
            return;
        }

        if (_temporaryZone.AddExtraSlots(_extraSlotCount))
        {
            _boosterActive = true;
            modelToMove.DOLocalMove(Vector3.left*4.5f,.325f);
        }
    }

    private void HandleLevelStarted(LevelStartPayload payload)
    {
        ResetBoosterState();
        modelToMove.localPosition = Vector3.zero;
    }

    private void HandleLevelEnded(LevelEndPayload payload)
    {
        ResetBoosterState();
        modelToMove.localPosition = Vector3.zero;
    }

    private void ResetBoosterState()
    {
        EnsureZoneReference();
        if (_temporaryZone == null)
        {
            return;
        }

        _temporaryZone.RemoveExtraSlots();
        _temporaryZone.RestoreOriginalCapacity();
        _boosterActive = false;
    }

    private void EnsureZoneReference()
    {
        if (_temporaryZone != null)
        {
            return;
        }

        _temporaryZone = FindAnyObjectByType<TemporaryZoneController>();
    }
}
