// ProgressPrefs.cs
using UnityEngine;

public static class ProgressPrefs
{
    // ---- Keys
    private const string KEY_CURRENT_LEVEL = "lv_current";
    private const string KEY_MAX_UNLOCKED  = "lv_unlocked_max";
    private static string StarsKey(int lv)    => $"lv_{lv}_stars";
    private static string BestTimeKey(int lv) => $"lv_{lv}_best_time";
 
    const string KEY_DISPLAYED_LEVEL = "pp_displayed_level";  // yeni

    
    public static int GetDisplayedLevelOr(int fallback)
    {
        var stored = PlayerPrefs.GetInt(KEY_DISPLAYED_LEVEL, Mathf.Max(1, fallback));
        return Mathf.Max(1, stored);
    }

    public static void SetDisplayedLevel(int value)
    {
        PlayerPrefs.SetInt(KEY_DISPLAYED_LEVEL, Mathf.Max(1, value));
        PlayerPrefs.Save();
    }

    public static int GetCurrentLevelOr(int fallback)
        => PlayerPrefs.GetInt(KEY_CURRENT_LEVEL, fallback);

    public static void SetCurrentLevel(int value)
    {
        PlayerPrefs.SetInt(KEY_CURRENT_LEVEL, Mathf.Max(0, value));
        PlayerPrefs.Save();
    }
   

    public static int  GetMaxUnlocked() => PlayerPrefs.GetInt(KEY_MAX_UNLOCKED, 0);
    public static void SetMaxUnlocked(int levelIndex)
    {
        PlayerPrefs.SetInt(KEY_MAX_UNLOCKED, Mathf.Max(0, levelIndex));
        PlayerPrefs.Save();
    }

    /// <summary>Parametreyle verilen seviyeye kadar açar (inclusive).</summary>
    public static void UnlockUpTo(int levelIndex)
    {
        int cur = GetMaxUnlocked();
        if (levelIndex > cur)
        {
            PlayerPrefs.SetInt(KEY_MAX_UNLOCKED, levelIndex);
            PlayerPrefs.Save();
        }
    }

    // ---- Per-level metrics (opsiyonel)
    public static int   GetBestStars(int levelIndex) => PlayerPrefs.GetInt(StarsKey(levelIndex), 0);
    public static void  SetBestStarsIfHigher(int levelIndex, int stars)
    {
        stars = Mathf.Clamp(stars, 0, 3);
        if (stars > GetBestStars(levelIndex))
        {
            PlayerPrefs.SetInt(StarsKey(levelIndex), stars);
            PlayerPrefs.Save();
        }
    }

    public static float GetBestTime(int levelIndex) => PlayerPrefs.GetFloat(BestTimeKey(levelIndex), 0f);
    public static void  SetBestTimeIfBetter(int levelIndex, float timeSeconds)
    {
        if (timeSeconds <= 0f) return;
        float best = GetBestTime(levelIndex);
        if (best <= 0f || timeSeconds < best)
        {
            PlayerPrefs.SetFloat(BestTimeKey(levelIndex), timeSeconds);
            PlayerPrefs.Save();
        }
    }

    // İsteğe bağlı: tüm ilerlemeyi sıfırla (debug için)
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(KEY_CURRENT_LEVEL);
        PlayerPrefs.DeleteKey(KEY_MAX_UNLOCKED);
        // Yıldız/Süre anahtarlarını silmek istersen, bilinen seviye aralığında tek tek sil.
        PlayerPrefs.Save();
    }
}
