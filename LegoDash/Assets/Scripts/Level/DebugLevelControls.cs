using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DebugLevelControls : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text adsToggleLabel;
    [SerializeField] private string adsOnLabel = "On";
    [SerializeField] private string adsOffLabel = "Off";

    [Header("Level References")]
    [SerializeField] private LevelMissionManager levelMissionManager;

    private void Awake()
    {
        if (adsToggleLabel == null)
        {
            adsToggleLabel = GetComponentInChildren<TMP_Text>();
        }
    }

    public void ToggleAds(bool enabled)
    {
        foreach (var service in FindAdServices())
        {
            if (service is MaxRewardedAdController maxRewarded)
            {
                maxRewarded.SetUseMockAds(!enabled);
            }
        }

        UpdateAdsLabel(enabled);
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
        var manager = levelMissionManager != null ? levelMissionManager : LevelMissionManager.Instance;
        if (manager != null)
        {
            manager.AdvanceToNextLevel();
            return;
        }

        LevelManager.Instance?.NextLevel();
    }

    public void GoToPreviousLevel()
    {
        var manager = levelMissionManager != null ? levelMissionManager : LevelMissionManager.Instance;
        if (manager != null)
        {
            manager.ReturnToPreviousLevel();
        }
    }

    private void UpdateAdsLabel(bool adsEnabled)
    {
        if (adsToggleLabel == null)
        {
            return;
        }

        adsToggleLabel.text = adsEnabled ? adsOnLabel : adsOffLabel;
    }
}
