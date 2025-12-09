using UnityEngine;
using UnityEngine.UI;

public class SoundManager : SingletonMonoBehaviour<SoundManager>
{
    [Header("Audio Sources")] [SerializeField]
    private AudioSource bgMusicSource; // Arkaplan müziği

    [SerializeField] private AudioSource sfxSource; // Etkileşim (SFX) sesleri

    [Header("UI Toggles")] [SerializeField]
    private Toggle bgMusicToggle;

    [SerializeField] private Toggle sfxToggle;
    [SerializeField] private Toggle vibrationToggle;

    // Ayar anahtarları
    private const string PrefBgMusic = "BgMusicOn";
    private const string PrefSfx = "SfxOn";
    private const string PrefVibration = "VibrationOn";

    private bool isBgMusicOn;
    private bool isSfxOn;
    private bool isVibrationOn;

    [SerializeField] private Button retryButton;

    private void Retry()
    {
        LevelManager.Instance.RestartLevel();
    }

    private void Start()
    {
        LoadPreferences();
        InitToggles();
        ApplySettings();
        retryButton.onClick.AddListener(Retry);
    }

    // PlayerPrefs'den yükle
    private void LoadPreferences()
    {
        isBgMusicOn = PlayerPrefs.GetInt(PrefBgMusic, 1) == 1;
        isSfxOn = PlayerPrefs.GetInt(PrefSfx, 1) == 1;
        isVibrationOn = PlayerPrefs.GetInt(PrefVibration, 1) == 1;
    }

    // UI Toggle'ları başlangıç değerleriyle ayarla, listener ekle
    private void InitToggles()
    {
        if (bgMusicToggle != null)
        {
            bgMusicToggle.isOn = isBgMusicOn;
            bgMusicToggle.onValueChanged.AddListener(ToggleBgMusic);
        }

        if (sfxToggle != null)
        {
            sfxToggle.isOn = isSfxOn;
            sfxToggle.onValueChanged.AddListener(ToggleSfx);
        }

        if (vibrationToggle != null)
        {
            vibrationToggle.isOn = isVibrationOn;
            vibrationToggle.onValueChanged.AddListener(ToggleVibration);
        }
    }

    // Ayarları AudioSource’lara uygula
    private void ApplySettings()
    {
        if (bgMusicSource != null) bgMusicSource.mute = !isBgMusicOn;
        if (sfxSource != null) sfxSource.mute = !isSfxOn;
    }

    // Toggle callback’leri
    public void ToggleBgMusic(bool on)
    {
        isBgMusicOn = on;
        if (bgMusicSource != null) bgMusicSource.mute = !on;
        PlayerPrefs.SetInt(PrefBgMusic, on ? 1 : 0);
    }

    public void ToggleSfx(bool on)
    {
        isSfxOn = on;
        if (sfxSource != null) sfxSource.mute = !on;
        PlayerPrefs.SetInt(PrefSfx, on ? 1 : 0);
    }

    public void ToggleVibration(bool on)
    {
        isVibrationOn = on;
        PlayerPrefs.SetInt(PrefVibration, on ? 1 : 0);
        Vibration.isActive = isVibrationOn;
    }

    // Oyun içi SFX çalma metodu
    public void PlaySfx(AudioClip clip)
    {
        if (isSfxOn && clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    // Titreşim tetikleme
    public void Vibrate()
    {
        if (isVibrationOn)
            Handheld.Vibrate();
    }
}