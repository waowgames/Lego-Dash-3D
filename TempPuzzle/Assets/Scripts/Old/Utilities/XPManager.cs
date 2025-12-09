using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;

public class XPManager : SingletonMonoBehaviour<XPManager>
{
    [Header("Level & XP")] public int currentLevel = 1;
    public int currentXP = 0;
    public int[] xpThresholds; // Inspector'da doldurulacak: {100, 200, 350, 550, ...}

    [Header("UI")] public Slider xpBar; // XP çubuğu UI Slider
    public TextMeshProUGUI levelText; // "Level 3" vs.
    public TextMeshProUGUI xpText; // "1/15" gibi gösterim için

    [Header("Effects")] public GameObject starPrefab; // Spawn edilecek yıldız prefab'ı (UI Image)
    private RectTransform targetUI; // Yıldızların gideceği UI elemanı (ör. levelText.rectTransform)
    public Canvas canvas;

    private const string PREF_LEVEL = "PlayerLevel";
    private const string PREF_XP = "PlayerXP";

    void Start()
    {
        LoadProgress();
        // Slider ayarları: boş başlama
        xpBar.minValue = 0;
        UpdateUI();
        targetUI = xpText.GetComponent<RectTransform>();
    }

    /// <summary>
    /// XP ekle ve gerekirse seviye atla.
    /// </summary>
    [Button]
    public void AddXP(int amount)
    {
        if (amount > 0)
            SpawnStars(amount);
        currentXP += amount;

        while (xpThresholds != null && xpThresholds.Length > 0 &&
               currentLevel - 1 < xpThresholds.Length &&
               currentXP >= xpThresholds[currentLevel - 1])
        {
            currentXP -= xpThresholds[currentLevel - 1];
            currentLevel++;
            OnLevelUp();
        }

        UpdateUI();

        // 2) Değerleri kaydet
        SaveProgress();
    }

    private void OnApplicationQuit()
    {
        // Uygulama kapanırken de kaydetmek için
        SaveProgress();
    }

    /// <summary>
    /// Seviye atlama anında ödül verme vb. işlemler.
    /// </summary>
    private void OnLevelUp()
    {
        Events.OnPlayerLevelUp?.Invoke();
   
    }

    /// <summary>
    /// PlayerPrefs’e kaydet.
    /// </summary>
    private void SaveProgress()
    {
        PlayerPrefs.SetInt(PREF_LEVEL, currentLevel);
        PlayerPrefs.SetInt(PREF_XP, currentXP);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// PlayerPrefs’ten yükle.
    /// </summary>
    private void LoadProgress()
    {
        // Eğer kayıt varsa çek, yoksa Inspector değerini kullan
        currentLevel = PlayerPrefs.GetInt(PREF_LEVEL, currentLevel);
        currentXP = PlayerPrefs.GetInt(PREF_XP, currentXP);
        Events.OnPlayerLevelUp?.Invoke();
    }

    /// <summary>
    /// XP bar ve seviye yazısını güncelle.
    /// </summary>
    private void UpdateUI()
    {
        levelText.text = $"{currentLevel}";

        // XP eşiğini güvenli bir şekilde al
        int threshold = 1;
        if (xpThresholds != null && xpThresholds.Length > 0)
        {
            if (currentLevel - 1 < xpThresholds.Length)
                threshold = xpThresholds[currentLevel - 1];
            else
                threshold = xpThresholds[xpThresholds.Length - 1];

            if (threshold <= 0)
            {
                Debug.LogWarning(
                    "XP threshold değeri 0 veya negatif: Inspector'da xpThresholds değerlerini kontrol edin.");
                threshold = 1;
            }
        }
        else
        {
            Debug.LogWarning("xpThresholds dizisi boş veya null: Inspector'da değer atayın.");
            threshold = Mathf.Max(currentXP, 1);
        }

        // Slider'ın maksimum değerini güncelle
        xpBar.maxValue = threshold;

        // Slider değerini güncelle (bar içi doluluk)
        xpBar.value = Mathf.Clamp(currentXP, 0, threshold);

        // XP metnini güncelle (örneğin "10/100")
        xpText.text = $"{currentXP}/{threshold}";
    }

    [Button]
    private void SpawnStars(int count)
    {
        if (starPrefab == null || targetUI == null || canvas == null)
            return;

        Vector3 canvasCenter = new Vector3(0, 0, 0);


        for (int i = 0; i < count; i++)
        {
            // Küçük rastgele ofsetler (örneğin ±50 piksel)
            Vector3 offset = new Vector3(Random.Range(-150f, 150f), 0, Random.Range(-150f, 150f));
            Vector3 spawnPos = canvasCenter + offset;

            GameObject star = Instantiate(starPrefab, canvas.transform);
            star.transform.localPosition = spawnPos;

            Vector3 targetPos = targetUI.position;
            float delay = i * 0.1f;
            float duration = 1.6f;

            // Logaritmik hareket hissi için Ease.OutExpo kullanıyoruz

            star.transform.DOMove(targetPos, duration)
                .SetEase(Ease.OutExpo)
                .SetDelay(delay)
                .OnComplete(() => Destroy(star));

            star.transform.DOScale(Vector3.zero, duration / 2).SetDelay(duration - .8f);
        }
    }
}