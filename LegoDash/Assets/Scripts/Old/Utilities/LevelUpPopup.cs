using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class LevelUpPopup : MonoBehaviour
{
    public static LevelUpPopup Instance { get; private set; }

    [Header("Refs")] public CanvasGroup rootCg; // popup tamamı
    public TMP_Text levelText; // "Level 2"
    public TMP_Text rewardAmountText; // "x 250" 
    public Button getButton; // opsiyonel; Get basılana kadar pasif bırak
    public Button reward2xButton; 
    [Header("Show/Hide")] public float fadeTime = 0.2f;

    private int _pendingReward;
    private bool _isShowing;
    private bool _reward2xInitialState;

    private Action _onMissionConfirmed;


    private Canvas _canvas;


    private void Awake()
    {
        Instance = this;
        _canvas = GetComponent<Canvas>();
        _canvas.enabled = false;
        if (rootCg != null)
        {
            rootCg.alpha = 0f;
            rootCg.interactable = false;
            rootCg.blocksRaycasts = false;
        }
    }
    
    public void ShowMissionComplete(int displayedLevel, Action onConfirmed)
    {
        if (_isShowing) return;

        _onMissionConfirmed = onConfirmed;
        _pendingReward = Mathf.Max(1, (LevelManager.Instance?.CurrentLevelIndex ?? 0) * 3);

        levelText.text = $"LEVEL {Mathf.Max(1, displayedLevel)}";
        rewardAmountText.text = $"+{_pendingReward}";

        _canvas.enabled = true;
        _isShowing = true;

        _reward2xInitialState = reward2xButton != null && reward2xButton.gameObject.activeSelf;
        if (reward2xButton != null)
        {
            reward2xButton.gameObject.SetActive(true);
            reward2xButton.enabled = true;
            reward2xButton.interactable = true;
        }

        if (rootCg != null)
        {
            rootCg.DOFade(1f, fadeTime).SetUpdate(true);
            rootCg.interactable = true;
            rootCg.blocksRaycasts = true;
        }

        getButton.enabled = true;
        getButton.interactable = true;
        getButton.onClick.RemoveAllListeners();
        getButton.onClick.AddListener(OnGetClicked);
    }

    private void OnGetClicked()
    {
        
        // Önce uçuş efekti, bitince para ekle + kapat
        getButton.enabled = false;
        var callback = _onMissionConfirmed ?? (() => LevelManager.Instance?.NextLevel());
        FlyToUIEffect.Instance.Play(() =>
        {
            callback.Invoke();

            getButton.enabled = true;
            Hide();
        }, _pendingReward);
    }

    private void Reward2XBbuttonClicked()
    {
        // Önce uçuş efekti, bitince para ekle + kapat
        getButton.enabled = false;
        var callback = _onMissionConfirmed ?? (() => LevelManager.Instance?.NextLevel());
        FlyToUIEffect.Instance.Play(() =>
        {
            callback.Invoke();

            getButton.enabled = true;
            Hide();
        }, _pendingReward * 2);
    }

    private void Hide()
    {
        if (!_isShowing) return;
        _isShowing = false;

        if (rootCg != null)
        {
            rootCg.interactable = false;
            rootCg.blocksRaycasts = false;
            rootCg.DOFade(0f, fadeTime).SetUpdate(true)
                .OnComplete(() => { _canvas.enabled = false; });
        }
        else
        {
            _canvas.enabled = false;
            Time.timeScale = 1f;
        }

        if (reward2xButton != null)
        {
            reward2xButton.gameObject.SetActive(_reward2xInitialState);
        }

        _onMissionConfirmed = null;
    }

    private int GetDisplayedLevel1Based()
    {
        if (LevelManager.Instance != null)
            return LevelManager.Instance.DisplayedLevel1Based;

        // LevelManager henüz hazır değilse PlayerPrefs'teki değeri kullan.
        int storedLevel = ProgressPrefs.GetDisplayedLevelOr(1);
        return Mathf.Max(1, storedLevel);
    }

    private void OnMissionConfirmClicked()
    {
        getButton.enabled = false;
        _onMissionConfirmed?.Invoke();
        Hide();
    }
}