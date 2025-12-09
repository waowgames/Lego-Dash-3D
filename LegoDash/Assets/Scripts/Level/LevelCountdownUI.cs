using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;

public class LevelCountdownUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Slider countdownSlider;
    [SerializeField] private bool hideWhenTimerInactive = true;

    private bool subscribed;
    private Coroutine waiter;

    void OnEnable()
    {
        SceneManager.activeSceneChanged += OnSceneChanged;
        TryAttachOrWait();
    }

    void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        Detach();
    }

    void OnSceneChanged(Scene a, Scene b)
    {
        // Sahne değişince yeniden bağlan
        Detach();
        TryAttachOrWait();
    }

    void TryAttachOrWait()
    {
        if (waiter != null) StopCoroutine(waiter);
        if (LevelManager.Instance != null) Attach();
        else waiter = StartCoroutine(WaitAndAttach());
    }

    IEnumerator WaitAndAttach()
    {
        // LevelManager hazır olana kadar bekle (gerekirse birkaç frame)
        while (LevelManager.Instance == null)
            yield return null;

        Attach();
        waiter = null;
    }

    void Attach()
    {
        if (subscribed || LevelManager.Instance == null) return;

        LevelManager.Instance.OnTimerTick += HandleTick;
        subscribed = true;

        // İlk görüntüyü anında güncelle
        RefreshImmediate();
        UpdateVisibility();
    }

    void Detach()
    {
        if (waiter != null) { StopCoroutine(waiter); waiter = null; }
        if (subscribed && LevelManager.Instance != null)
            LevelManager.Instance.OnTimerTick -= HandleTick;

        subscribed = false;
    }

    void HandleTick(float remaining, float ratio)
    {
        if (!label) return;
        int sec = Mathf.CeilToInt(Mathf.Max(0f, remaining));
        int m = sec / 60;
        int s = sec % 60;
        label.text = $"{m:00}:{s:00}";
        if (countdownSlider)
            countdownSlider.value = Mathf.Clamp01(ratio);
        UpdateVisibility();
    }

    void RefreshImmediate()
    {
        if (!label || LevelManager.Instance == null) return;
        int sec = Mathf.CeilToInt(LevelManager.Instance.RemainingTime);
        int m = sec / 60;
        int s = sec % 60;
        label.text = $"{m:00}:{s:00}";
        if (countdownSlider && LevelManager.Instance.CurrentLevelConfig != null)
        {
            float total = Mathf.Max(0.0001f, LevelManager.Instance.CurrentLevelConfig.TimeLimitSeconds);
            countdownSlider.value = Mathf.Clamp01(LevelManager.Instance.RemainingTime / total);
        }
    }

    void UpdateVisibility()
    {
        if (!hideWhenTimerInactive || LevelManager.Instance == null) return;
        bool active = LevelManager.Instance.IsTimerActive;
        if (label) label.gameObject.SetActive(active);
        if (countdownSlider) countdownSlider.gameObject.SetActive(active);
    }
}
