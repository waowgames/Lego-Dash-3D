// LevelManager.cs (ilgili eklemeler işaretlendi)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using DG.Tweening;
using Sirenix.OdinInspector; // << EKLENDİ: Action için

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    public ParticleSystem confettiParticle;
    public LevelState State { get; private set; } = LevelState.Idle;

    public LevelConfig CurrentLevelConfig { get; private set; }
    public int CurrentLevelIndex { get; private set; }
    public int DisplayedLevelNumber { get; private set; }
    public int CurrentAttempt { get; private set; } = 0;
    public float Elapsed { get; private set; } = 0f;

    private readonly List<ILevelResettable> resettables = new();
    private bool ticking;

    // === TIMER alanları (EKLENDİ) ===
    private bool timerActive = false;
    private bool timerExpired = false;
    private float timeLimit = 0f;
    

    /// <summary> Kalan süre (s) — timer kapalıysa 0 döner. </summary>
    public float RemainingTime => timerActive ? Mathf.Max(0f, timeLimit - Elapsed) : 0f;
    public bool IsTimerActive => timerActive;

    /// <summary> UI için: (remainingSeconds, ratio[0..1]) </summary>
    public event Action<float, float> OnTimerTick;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        // GameAnalytics.Initialize();
        //
        //
        // BaseTenjin instance = Tenjin.getInstance("GSPTCKSIBO7QSXNH4YQNYVWVWZ4BRK2D");
        //
        // instance.Connect();
        // instance.SubscribeAppLovinImpressions();
        //
        // instance.SetAppStoreType(AppStoreType.googleplay);
        //
        // MaxSdkCallbacks.OnSdkInitializedEvent += (MaxSdk.SdkConfiguration sdkConfiguration) => {
        //     // AppLovin SDK is initialized, start loading ads
        // };
        //
        // MaxSdk.InitializeSdk();
        //
    }

    void Update()
    {
        if (State == LevelState.Running && ticking)
        {
            Elapsed += Time.deltaTime;

            // === TIMER Update (EKLENDİ) ===
            if (timerActive)
            {
                float rem = RemainingTime;
                OnTimerTick?.Invoke(rem, timeLimit > 0f ? rem / timeLimit : 0f);

                if (!timerExpired && rem <= 0f)
                {
                    HandleTimerExpired();
                }
            }
        }
    }

    public void RegisterResettable(ILevelResettable r)
    {
        if (!resettables.Contains(r))
        {
            resettables.Add(r);
            if (State == LevelState.Running)
            {
                try { r.ResetForNewLevel(CurrentLevelIndex); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
        }
    }

    public void UnregisterResettable(ILevelResettable r) => resettables.Remove(r);

    void EnsureResettablesCached()
    {
        if (resettables.Count == 0)
        {
            var monos = FindObjectsOfType<MonoBehaviour>(true);
            foreach (var m in monos)
                if (m is ILevelResettable r) RegisterResettable(r);
        }
    }

    public void BeginLevel(LevelConfig config, int levelIndex)
    {
        State = LevelState.Idle;
        CurrentLevelConfig = config;
        CurrentLevelIndex = Mathf.Max(0, levelIndex);
        DisplayedLevelNumber = ProgressPrefs.GetDisplayedLevelOr(CurrentLevelIndex + 1);
        ProgressPrefs.SetDisplayedLevel(DisplayedLevelNumber);
        Elapsed = 0f;
        CurrentAttempt++;

        EnsureResettablesCached();
        foreach (var r in resettables)
        {
            try { r.ResetForNewLevel(CurrentLevelIndex); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        Events.RaiseLevelStarted(new LevelStartPayload
        {
            LevelIndex = CurrentLevelIndex,
            Attempt = CurrentAttempt,
            TimeScale = Time.timeScale
        });

        timerActive = config != null && config.TimeLimitSeconds > 0f;
        timeLimit = config != null ? config.TimeLimitSeconds : 0f;

        timerExpired = false;
        if (timerActive) OnTimerTick?.Invoke(RemainingTime, timeLimit > 0f ? RemainingTime / timeLimit : 0f);

        State = LevelState.Running;
        ticking = true;
    }

    public void CompleteLevel(bool success, int stars = 0)
    {
        if (State != LevelState.Running) return;
        ticking = false;
        State = LevelState.Ended;

        var payload = new LevelEndPayload
        {
            LevelIndex = CurrentLevelIndex,
            Success = success,
            ElapsedSeconds = Elapsed,
            Stars = Mathf.Max(0, stars)
        };

     
        StartCoroutine(EndLevelWithDelay(payload, 1.2f, success));
    }

    private IEnumerator EndLevelWithDelay(LevelEndPayload payload, float seconds, bool success)
    {
      
        if(success && confettiParticle != null)
            confettiParticle.Play();
        
        yield return new WaitForSeconds(seconds);
        Events.RaiseLevelEnded(payload);

    }

    public void RestartLevel() => LevelMissionManager.Instance?.ReloadCurrentLevel();

    public void NextLevel() => LevelMissionManager.Instance?.AdvanceToNextLevel();

    public void FailLevel() => CompleteLevel(false);
    
    [Button]
    public void WinLevel(int stars = 3) => CompleteLevel(true, stars);

    private void HandleTimerExpired()
    {
        timerExpired = true;
        ticking = false;
        State = LevelState.Paused;
        OnTimerTick?.Invoke(0f, 0f);
        Events.RaiseLevelTimeout();
    }

    // === Timer yardımcıları (EKLENDİ) ===
    /// <summary> Geri sayımı anlık artır (ör. bonus zaman). </summary>
    public void AddTime(float seconds)
    {
        if (!timerActive || seconds <= 0f) return;
        timeLimit += seconds;
        OnTimerTick?.Invoke(RemainingTime, timeLimit > 0f ? RemainingTime / timeLimit : 0f);
        //StartCoroutine(TimeAddMovement());
    }

    /// <summary> Geri sayımı anlık tüket (ceza gibi). </summary>
    public void ConsumeTime(float seconds)
    {
        if (!timerActive || seconds <= 0f) return;
        Elapsed += seconds;
        OnTimerTick?.Invoke(RemainingTime, timeLimit > 0f ? RemainingTime / timeLimit : 0f);
        if (!timerExpired && RemainingTime <= 0f)
        {
            HandleTimerExpired();
        }
    }

    public bool ReviveAfterTimeout(float bonusSeconds)
    {
        if (!timerActive || bonusSeconds <= 0f) return false;

        if (!timerExpired)
        {
            AddTime(bonusSeconds);
            return true;
        }

        if (State != LevelState.Paused) return false;

        timeLimit = Mathf.Max(timeLimit, Elapsed) + bonusSeconds;
        timerExpired = false;
        State = LevelState.Running;
        ticking = true;
        OnTimerTick?.Invoke(RemainingTime, timeLimit > 0f ? RemainingTime / timeLimit : 0f);
        return true;
    }

    public int DisplayedLevel1Based => Mathf.Max(1, DisplayedLevelNumber);

    public void IncrementDisplayedLevel()
    {
        DisplayedLevelNumber = Mathf.Max(1, DisplayedLevelNumber + 1);
        ProgressPrefs.SetDisplayedLevel(DisplayedLevelNumber);
    }
    //
    // private IEnumerator TimeAddMovement()
    // {
    //     timeAddedText.SetActive(true);
    //     timeAddedText.transform.localPosition = new Vector3(-191, -705, 0);
    //     timeAddedText.transform.DOLocalMove(new Vector3(-191, -600, 0), .5f);
    //     timeAddedParticles.Play();
    //     yield return new WaitForSeconds(.6f);
    //     timeAddedText.SetActive(false);
    // }
}
