using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BoosterUIManager : MonoBehaviour
{
    [SerializeField] private BoosterSlot[] boosterSlots = Array.Empty<BoosterSlot>();

    private bool subscribedToScore;
    private Coroutine waitRoutine;


    private void Start()
    {
        InvokeRepeating(nameof(RefreshAll),1,2);
    }

    private void OnEnable()
    {
        foreach (var slot in boosterSlots)
            slot?.Setup(this);

        TrySubscribeScore();
        RefreshAll();
    }

    private void OnDisable()
    {
        foreach (var slot in boosterSlots)
            slot?.Teardown();

        if (subscribedToScore && UIManager.Instance != null)
        {
            UIManager.Instance.ScoreChanged -= HandleScoreChanged;
            subscribedToScore = false;
        }

        if (waitRoutine != null)
        {
            StopCoroutine(waitRoutine);
            waitRoutine = null;
        }
    }

    private void TrySubscribeScore()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ScoreChanged += HandleScoreChanged;
            subscribedToScore = true;
        }
        else if (waitRoutine == null)
        {
            waitRoutine = StartCoroutine(WaitForScoreManager());
        }
    }

    private IEnumerator WaitForScoreManager()
    {
        while (UIManager.Instance == null)
            yield return null;

        UIManager.Instance.ScoreChanged += HandleScoreChanged;
        subscribedToScore = true;
        waitRoutine = null;
        RefreshAll();
    }

    private void HandleScoreChanged(int _)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        foreach (var slot in boosterSlots)
            slot?.Refresh();
    }

    internal bool TrySpend(int cost)
    {
        if (UIManager.Instance == null)
            return false;

        if (UIManager.Instance.Score < cost)
            return false;

        UIManager.Instance.ScoreAdd(-cost);
        return true;
    }

    [Serializable]
    private class BoosterSlot
    {
        [SerializeField] private string id = "";
        [SerializeField] private Button boosterButton;
        [SerializeField] private UnityEvent onBoosterTriggered;
        [SerializeField] private int price = 10;

        [Header("Purchase UI")]
        [SerializeField] private GameObject purchaseContainer;
        [SerializeField] private GameObject watchIcon;
        [SerializeField] private Button purchaseButton;
        [SerializeField] private TextMeshProUGUI priceLabel;

        [Header("Owned UI")]
        [SerializeField] private GameObject ownedContainer;
        [SerializeField] private TextMeshProUGUI ownedCountLabel;

        private BoosterUIManager owner;
        private int ownedCount;

        public void Setup(BoosterUIManager owner)
        {
            this.owner = owner;
            ownedCount = LoadOwnedCount();

            if (boosterButton != null)
                boosterButton.onClick.AddListener(UseBooster);

            if (purchaseButton != null)
                purchaseButton.onClick.AddListener(PurchaseBooster);

            if (priceLabel != null)
                priceLabel.text = price.ToString();

            Refresh();
        }

        public void Teardown()
        {
            if (boosterButton != null)
                boosterButton.onClick.RemoveListener(UseBooster);

            if (purchaseButton != null)
                purchaseButton.onClick.RemoveListener(PurchaseBooster);
        }
        

        public void Refresh()
        {
            ownedCount = LoadOwnedCount();

            bool hasBooster = ownedCount > 0;

            if (purchaseContainer != null)
                purchaseContainer.SetActive(!hasBooster);

            if (ownedContainer != null)
                ownedContainer.SetActive(hasBooster);

            if (ownedCountLabel != null)
                ownedCountLabel.text = ownedCount.ToString();

            if (priceLabel != null)
                priceLabel.text = price.ToString();

            if (purchaseButton != null)
                purchaseButton.interactable = CanAffordBooster() || IsRewardedAdReady();

            if (boosterButton != null)
                boosterButton.interactable = hasBooster;
        }

        private void UseBooster()
        {
            if (ownedCount <= 0)
                return;

            bool used = true; 

            if (!used)
                return;

            ownedCount--;
            SaveOwnedCount();
            Refresh();
            onBoosterTriggered?.Invoke();
        }

 
        private void PurchaseBooster()
        {
            if (owner == null)
                return;
 
            if (owner.TrySpend(price))
            {
                GrantBooster();
                return;
            }

            if (TryPurchaseWithAd())
            {
                return;
            }
            
            Refresh();
        }

        private int LoadOwnedCount()
        {
            return PlayerPrefs.GetInt(GetPrefKey(), 0);
        }

        private void SaveOwnedCount()
        {
            PlayerPrefs.SetInt(GetPrefKey(), Mathf.Max(0, ownedCount));
            PlayerPrefs.Save();
        }

        private bool CanAffordBooster()
        {
            if (UIManager.Instance != null && UIManager.Instance.Score < price)
            {
                watchIcon.SetActive(true);
                priceLabel.text = "Watch AD";
            }
            else
            {
                watchIcon.SetActive(false);
                priceLabel.text = price.ToString();
            }
            return UIManager.Instance != null && UIManager.Instance.Score >= price;
        }

        private bool IsRewardedAdReady()
        {
            return false;
        }

        private void GrantBooster()
        {
            ownedCount++;
            SaveOwnedCount();
            Refresh();
        }

        private bool TryPurchaseWithAd()
        {
        

            return false;
        }

        private string GetPrefKey()
        {
            if (string.IsNullOrEmpty(id))
                id = boosterButton != null ? boosterButton.name : GetHashCode().ToString();

            return $"booster_{id}_count";
        }
    }
}
