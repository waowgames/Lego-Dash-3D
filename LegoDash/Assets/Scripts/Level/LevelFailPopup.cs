using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.Serialization;

public class LevelFailPopup : MonoBehaviour
{
    public static LevelFailPopup Instance { get; private set; }

    [Header("Refs")]
    public CanvasGroup rootCg;                 // Popup 
    public Button retryButton;                 // Zorunlu
    [FormerlySerializedAs("homeButton")] public Button addTimeButton;                  // Opsiyonel (boş bırakılabilir)

    [Header("Revive Settings")]
    [Tooltip("Zaman ekle butonuna basıldığında verilecek bonus süre (saniye).")]
    public float reviveBonusSeconds = 15f;

    [Header("Show/Hide")]
    public float fadeTime = 0.2f;

    private bool _isShowing;
    private Canvas _canvas;

    private void Awake()
    {
        Instance = this;
        _canvas = GetComponent<Canvas>();
        if (_canvas != null) _canvas.enabled = false;

        if (rootCg != null)
        {
            rootCg.alpha = 0f;
            rootCg.interactable = false;
            rootCg.blocksRaycasts = false;
        }
    }

    private void OnEnable()
    {
        Events.LevelEnded += OnLevelEnded;
        Events.LevelTimeout += OnLevelTimeout;
    }

    private void OnDisable()
    {
        Events.LevelEnded -= OnLevelEnded;
        Events.LevelTimeout -= OnLevelTimeout;
    }


    private void OnLevelEnded(LevelEndPayload payload)
    {
        if (!payload.Success)
        {
            // İstersen reasonText'i zaman bitti vb. metinle doldur.
            // payload üzerinde spesifik bir neden yoksa boş geçilir.
            Show();
        }
    }

    public void Show()
    {
        _isShowing = true;

        if (_canvas != null) _canvas.enabled = true;
        if (rootCg != null)
        {
            rootCg.DOFade(1f, fadeTime).SetUpdate(true);
            rootCg.interactable = true;
            rootCg.blocksRaycasts = true;
        }

        // Butonları bağla
        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(OnRetryClicked);
        }

        if (addTimeButton != null)
        {
            addTimeButton.onClick.RemoveAllListeners();
            addTimeButton.onClick.AddListener(OnAddTimeClicked);
        }
    }

    private void OnRetryClicked()
    {
        // Level'ı yeniden başlat
        LevelManager.Instance.RestartLevel();
        Hide();
    }

    private void OnAddTimeClicked()
    {
        if (LevelManager.Instance == null) return;

      
    }

    private void OnLevelTimeout()
    {
        Show();
    }

    public void Hide()
    {
        if (!_isShowing) return;
        _isShowing = false;

        if (rootCg != null)
        {
            rootCg.interactable = false;
            rootCg.blocksRaycasts = false;
            rootCg.DOFade(0f, fadeTime).SetUpdate(true)
                .OnComplete(() =>
                {
                    if (_canvas != null) _canvas.enabled = false;
                });
        }
        else
        {
            if (_canvas != null) _canvas.enabled = false;
        }
    }
}
