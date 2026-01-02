using TMPro;
using UnityEngine;

public class LevelTextUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private string format = "Level {0}";

    private void Awake()
    {
        if (label == null)
        {
            label = GetComponent<TextMeshProUGUI>();
        }
    }

    private void OnEnable()
    {
        Events.LevelStarted += HandleLevelStarted;
        Refresh();
    }

    private void OnDisable()
    {
        Events.LevelStarted -= HandleLevelStarted;
    }

    private void HandleLevelStarted(LevelStartPayload payload)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (label == null)
        {
            return;
        }

        int displayedLevel = 1;
        if (LevelManager.Instance != null)
        {
            displayedLevel = LevelManager.Instance.DisplayedLevel1Based;
        }
        else
        {
            displayedLevel = ProgressPrefs.GetDisplayedLevelOr(1);
        }

        label.text = string.Format(format, displayedLevel);
    }
}
