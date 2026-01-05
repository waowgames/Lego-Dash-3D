using System.Collections.Generic;
using UnityEngine;

public class DebugLevelControls : MonoBehaviour
{
    public void ToggleAds(bool enabled)
    {
        foreach (var service in FindAdServices())
        {
            service.enabled = enabled;
        }
    }

    private List<MonoBehaviour> FindAdServices()
    {
        var services = new List<MonoBehaviour>();
        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var behaviour in behaviours)
        {
            if (behaviour is IAdService)
            {
                services.Add(behaviour);
            }
        }

        return services;
    }

    public void GoToNextLevel()
    {
        if (LevelMissionManager.Instance != null)
        {
            LevelMissionManager.Instance.AdvanceToNextLevel();
            return;
        }

        LevelManager.Instance?.NextLevel();
    }

    public void GoToPreviousLevel()
    {
        if (LevelMissionManager.Instance != null)
        {
            LevelMissionManager.Instance.ReturnToPreviousLevel();
        }
    }
}
