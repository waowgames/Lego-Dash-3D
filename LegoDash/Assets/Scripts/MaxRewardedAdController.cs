using System;
using AppLovinMax;
using UnityEngine;

/// <summary>
/// Handles AppLovin MAX rewarded ad loading/showing.
/// Attach to a persistent GameObject in the first scene.
/// </summary>
public class MaxRewardedAdController : MonoBehaviour, IAdService
{
    public static MaxRewardedAdController Instance { get; private set; }

    [Header("Rewarded Ad Unit Ids")]
    [Tooltip("Rewarded Ad Unit Id for Android builds.")]
    [SerializeField] private string rewardedAdUnitIdAndroid = "REPLACE_WITH_ANDROID_AD_UNIT_ID";

    [Tooltip("Rewarded Ad Unit Id for iOS builds.")]
    [SerializeField] private string rewardedAdUnitIdiOS = "REPLACE_WITH_IOS_AD_UNIT_ID";

    private string _adUnitId;
    private bool _isInitialized;
    private bool _rewardEarned;
    private Action _onRewardEarned;
    private Action _onAdClosed;
    private bool _useMockAds;

    [Header("Mock Ads (Editor)")]
    [SerializeField] private bool useMockAdsInEditor = true;
    [SerializeField] private MockAdService mockAdService;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        _adUnitId = ResolveAdUnitId();
        _useMockAds = ShouldUseMockAds();

        if (_useMockAds)
        {
            EnsureMockAdService();
            return;
        }

        MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;
        MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoaded;
        MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdFailedToLoad;
        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdFailedToDisplay;
        MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayed;
        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdHidden;
        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedReward;
    }

    private void OnDestroy()
    {
        if (Instance == this && !_useMockAds)
        {
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= OnRewardedAdLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= OnRewardedAdFailedToLoad;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= OnRewardedAdFailedToDisplay;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent -= OnRewardedAdDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= OnRewardedAdHidden;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= OnRewardedAdReceivedReward;
        }
    }

    public bool IsRewardedAdReady()
    {
        if (_useMockAds)
        {
            return mockAdService != null && mockAdService.IsAdReady(GetMockPlacement());
        }

        return !string.IsNullOrEmpty(_adUnitId) && MaxSdk.IsRewardedAdReady(_adUnitId);
    }

    public bool IsAdReady(string placement)
    {
        if (_useMockAds)
        {
            return mockAdService != null && mockAdService.IsAdReady(placement);
        }

        return IsRewardedAdReady();
    }
    private void Start()
    {
        if (_useMockAds)
        {
            return;
        }

        // SDK might already be initialized by the time this component starts.
        if (MaxSdk.IsInitialized())
        {
            _isInitialized = true;
            LoadRewardedAd();
        }
    }

    private void OnSdkInitialized(MaxSdkBase.SdkConfiguration obj)
    {
        _isInitialized = true;
        LoadRewardedAd();
    }

    private void OnRewardedAdLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        if (!IsRelevantAdUnit(adUnitId)) return;
        Debug.Log("MAX Rewarded: Ad loaded.");
    }

    private void OnRewardedAdFailedToLoad(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
    {
        if (!IsRelevantAdUnit(adUnitId)) return;
        Debug.LogWarning($"MAX Rewarded: Failed to load ({errorInfo.Code}) - {errorInfo.Message}");

        // Retry with exponential backoff capped at 15 seconds.
        float retryDelay = Mathf.Pow(2, Mathf.Min(4, 6));
        retryDelay = Mathf.Min(15f, retryDelay);
        Invoke(nameof(LoadRewardedAd), retryDelay);
    }

    private void OnRewardedAdFailedToDisplay(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
    {
        if (!IsRelevantAdUnit(adUnitId)) return;
        Debug.LogWarning($"MAX Rewarded: Failed to display ({errorInfo.Code}) - {errorInfo.Message}");
        HandleAdFinished(false);
        LoadRewardedAd();
    }

    private void OnRewardedAdDisplayed(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        if (!IsRelevantAdUnit(adUnitId)) return;
        Debug.Log("MAX Rewarded: Ad displayed.");
    }

    private void OnRewardedAdHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
    {
        if (!IsRelevantAdUnit(adUnitId)) return;
        Debug.Log("MAX Rewarded: Ad hidden.");
        HandleAdFinished(_rewardEarned);
        LoadRewardedAd();
    }

    private void OnRewardedAdReceivedReward(string adUnitId, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo adInfo)
    {
        if (!IsRelevantAdUnit(adUnitId)) return;
        _rewardEarned = true;
    }

    /// <summary>
    /// Attempts to show a rewarded ad. Returns false if the ad is not ready.
    /// </summary>
    public bool TryShowRewardedAd(Action onRewardEarned, Action onAdClosed = null)
    {
        if (_useMockAds)
        {
            if (mockAdService == null)
            {
                Debug.LogWarning("MAX Rewarded: Mock ad service missing.");
                return false;
            }

            _rewardEarned = false;
            _onRewardEarned = onRewardEarned;
            _onAdClosed = onAdClosed;

            mockAdService.ShowRewarded(
                GetMockPlacement(),
                () =>
                {
                    _rewardEarned = true;
                    HandleAdFinished(true);
                },
                () => HandleAdFinished(false));

            return true;
        }

        if (string.IsNullOrEmpty(_adUnitId) || !MaxSdk.IsRewardedAdReady(_adUnitId))
        {
            return false;
        }

        _rewardEarned = false;
        _onRewardEarned = onRewardEarned;
        _onAdClosed = onAdClosed;
        MaxSdk.ShowRewardedAd(_adUnitId);
        return true;
    }

    public void ShowRewarded(string placement, Action onSuccess, Action onFail)
    {
        if (_useMockAds)
        {
            if (mockAdService == null)
            {
                Debug.LogWarning("MAX Rewarded: Mock ad service missing.");
                onFail?.Invoke();
                return;
            }

            mockAdService.ShowRewarded(placement, onSuccess, onFail);
            return;
        }

        bool started = TryShowRewardedAd(onSuccess, onFail);
        if (!started)
        {
            onFail?.Invoke();
        }
    }
    

    private void HandleAdFinished(bool rewardEarned)
    {
        if (rewardEarned)
        {
            try
            {
                _onRewardEarned?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        try
        {
            _onAdClosed?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        _onRewardEarned = null;
        _onAdClosed = null;
        _rewardEarned = false;
    }

    private void LoadRewardedAd()
    {
        if (_useMockAds || !_isInitialized || string.IsNullOrEmpty(_adUnitId))
            return;

        MaxSdk.LoadRewardedAd(_adUnitId);
    }

    private string ResolveAdUnitId()
    {
#if UNITY_ANDROID
        return rewardedAdUnitIdAndroid;
#elif UNITY_IOS
        return rewardedAdUnitIdiOS;
#else
        return rewardedAdUnitIdAndroid;
#endif
    }

    private bool IsRelevantAdUnit(string adUnitId)
    {
        return !string.IsNullOrEmpty(_adUnitId) && string.Equals(_adUnitId, adUnitId, StringComparison.Ordinal);
    }

    private bool ShouldUseMockAds()
    {
#if UNITY_EDITOR
        return useMockAdsInEditor;
#else
        return false;
#endif
    }

    private void EnsureMockAdService()
    {
        if (mockAdService == null)
        {
            mockAdService = GetComponent<MockAdService>();
        }

        if (mockAdService == null)
        {
            Debug.LogWarning("MAX Rewarded: Mock ad service not found on GameObject.");
        }
    }

    private string GetMockPlacement()
    {
        return "max_rewarded";
    }
}
