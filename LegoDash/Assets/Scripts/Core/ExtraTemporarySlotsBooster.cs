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
        }
    }

    private void HandleLevelStarted(LevelStartPayload payload)
    {
        ResetBoosterState();
    }

    private void HandleLevelEnded(LevelEndPayload payload)
    {
        ResetBoosterState();
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
