using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BoosterUIManager : MonoBehaviour
{
    [SerializeField] private BoosterSlot[] boosterSlots = Array.Empty<BoosterSlot>();
    [SerializeField] private MonoBehaviour adServiceProvider;
    [SerializeField] private BoosterManager boosterManager;

    private IAdService adService;

    private void Awake()
    {
        CacheAdService();
        CacheBoosterManager();
    }

    private void OnEnable()
    {
        foreach (var slot in boosterSlots)
        {
            slot?.Setup(this);
        }
    }

    private void OnDisable()
    {
        foreach (var slot in boosterSlots)
        {
            slot?.Teardown();
        }
    }

    internal IAdService GetAdService()
    {
        return adService;
    }

    internal BoosterManager GetBoosterManager()
    {
        return boosterManager != null ? boosterManager : BoosterManager.Instance;
    }

    private void CacheAdService()
    {
        if (adServiceProvider is IAdService service)
        {
            adService = service;
            return;
        }

        if (adServiceProvider != null)
        {
            Debug.LogWarning("BoosterUIManager: Assigned ad service does not implement IAdService.");
        }

        foreach (var component in FindObjectsOfType<MonoBehaviour>(true))
        {
            if (component is IAdService fallbackService)
            {
                adService = fallbackService;
                adServiceProvider = component;
                return;
            }
        }

        Debug.LogWarning("BoosterUIManager: No IAdService found. Ad flow will fail.");
    }

    private void CacheBoosterManager()
    {
        if (boosterManager != null)
            return;

        boosterManager = BoosterManager.Instance;
        if (boosterManager == null)
        {
            boosterManager = FindObjectOfType<BoosterManager>(true);
        }
    }

    [Serializable]
    private class BoosterSlot
    {
        [SerializeField] private BoosterType boosterType = BoosterType.None;
        [SerializeField] private Button watchAdButton;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private UnityEvent onBoosterTriggered;

        private BoosterUIManager owner;
        private bool isLoading;

        public void Setup(BoosterUIManager owner)
        {
            this.owner = owner;
            if (watchAdButton != null)
            {
                watchAdButton.onClick.AddListener(HandleWatchAdClicked);
            }

            Refresh();
        }

        public void Teardown()
        {
            if (watchAdButton != null)
            {
                watchAdButton.onClick.RemoveListener(HandleWatchAdClicked);
            }

            isLoading = false;
        }

        public void Refresh()
        {
            if (IsActive())
            {
                SetActive();
                return;
            }

            if (isLoading)
            {
                SetLoading();
                return;
            }

            SetIdle();
        }

        private void HandleWatchAdClicked()
        {
            if (owner == null)
                return;

            if (IsActive())
            {
                Debug.Log("BoosterUIManager: Booster already active, ignoring click.");
                SetActive();
                return;
            }

            var service = owner.GetAdService();
            if (service == null)
            {
                Debug.LogWarning("BoosterUIManager: No ad service available.");
                return;
            }

            string placement = boosterType.ToString();
            if (!service.IsAdReady(placement))
            {
                Debug.LogWarning($"BoosterUIManager: Ad not ready for placement '{placement}'.");
                return;
            }

            StartAdFlow(service, placement);
        }

        private void StartAdFlow(IAdService service, string placement)
        {
            isLoading = true;
            SetLoading();
            Debug.Log($"BoosterUIManager: Starting ad for booster '{boosterType}'.");

            service.ShowRewarded(
                placement,
                OnAdSuccess,
                OnAdFailed);
        }

        private void OnAdSuccess()
        {
            isLoading = false;
            Debug.Log($"BoosterUIManager: Ad success for booster '{boosterType}'. Activating booster.");
            ActivateBooster();
        }

        private void OnAdFailed()
        {
            isLoading = false;
            Debug.LogWarning($"BoosterUIManager: Ad failed or cancelled for booster '{boosterType}'.");
            SetIdle();
        }

        private void ActivateBooster()
        {
            var manager = owner.GetBoosterManager();
            if (manager != null)
            {
                manager.ActivateBooster(boosterType);
            }
            else
            {
                Debug.LogWarning("BoosterUIManager: No BoosterManager available to activate booster.");
            }

            onBoosterTriggered?.Invoke();
            SetActive();
        }

        private bool IsActive()
        {
            var manager = owner?.GetBoosterManager();
            return manager != null && manager.IsBoosterActive(boosterType);
        }

        private void SetIdle()
        {
            if (stateLabel != null)
                stateLabel.text = "Watch Ad";

            if (watchAdButton != null)
                watchAdButton.interactable = true;
        }

        private void SetLoading()
        {
            if (stateLabel != null)
                stateLabel.text = "Loading...";

            if (watchAdButton != null)
                watchAdButton.interactable = false;
        }

        private void SetActive()
        {
            if (stateLabel != null)
                stateLabel.text = "Active";

            if (watchAdButton != null)
                watchAdButton.interactable = false;
        }
    }
}
