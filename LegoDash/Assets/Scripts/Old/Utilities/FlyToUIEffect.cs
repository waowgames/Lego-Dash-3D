using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// Coin'leri sahneye saçıp (scatter) kısa bekledikten sonra hedefe "U/yay" ile uçurur.
/// Ekstra efektler: stagger launch, spin, squash&stretch, hedef shake, fade+shrink, hit VFX, SFX.
/// </summary>
public class FlyToUIEffect : MonoBehaviour
{
    public static FlyToUIEffect Instance;

    [Header("Pool & Prefab")] public RectTransform coinPrefab;
    [Min(1)] public int coinAmount = 12;

    [Header("Target")] public RectTransform target;

    [Header("Timing")]
    [Tooltip("Spawn arası gecikme (gerçek zaman). Uçuş başlangıcını launch delay ve introHold belirler.")]
    public float spawnInterval = 0.05f;

    [Tooltip("Temel uçuş süresi (her coin, jitter ile çeşitlenir).")]
    public float travelTime = 0.75f;

    [Header("Intro Scatter & Hold")] [Tooltip("Spawn anında coin'ler merkez yerine rastgele konumlara saçılır.")]
    public bool enableIntroScatter = true;

    [Tooltip("Saçılma yarıçapı (px).")] public float scatterRadius = 140f;

    [Tooltip("Eliptik dağılım ölçeği (1,1 = daire). Örn: (1,0.6) yatay elips.")]
    public Vector2 scatterEllipse = new Vector2(1f, 0.8f);

    [Tooltip("Spawn'da minik pop ölçeği.")]
    public float scatterPopScale = 1.12f;

    [Tooltip("Pop animasyon süresi.")] public float scatterPopDuration = 0.12f;

    [Tooltip("Spawnlar bitince uçuşa başlamadan önce bekleme (sn).")]
    public float introHold = 0.35f;

    [Tooltip("Intro beklemesi için ± rastgele ek (0..value).")]
    public float introHoldJitter = 0.15f;

    [Header("Stagger (Başlatma Gecikmesi)")] [Tooltip("Her coin için i*launchDelayPerCoin kadar gecikme eklenir.")]
    public float launchDelayPerCoin = 0.035f;

    [Tooltip("Her coin'e eklenen ± rastgele gecikme (0..value).")]
    public float launchDelayJitter = 0.025f;

    [Tooltip("Uçuş süresine ± rastgele ekleme (0..value).")]
    public float travelTimeJitter = 0.08f;

    [Header("Trajectory (U/Yay)")] [Tooltip("Yay yüksekliği (px).")]
    public float arcAmplitude = 160f;

    [Tooltip("Yay boyunca ileri/geri itme (px).")]
    public float arcForwardPush = 60f;

    [Range(0f, 1f)] public float arcIntensity = 1f;

    [Tooltip("Yaya ± rastgele yüzde (0..1 eşleniği). Örn: 0.15 => ±%15 oynama.")]
    public float arcJitter = 0.15f;

    [Header("Extra Motion")]
    public AnimationCurve heightCurve; // İsteğe bağlı ekstra yükseklik eğrisi (pos.y üzerine ek)

    [Tooltip("Her coin için küçük yanal rastgelelik.")]
    public float lateralJitterScale = 0.2f;

    [Header("Spin")] public bool enableSpin = true;

    [Tooltip("Toplam tur sayısı (0.0 => spin yok).")]
    public float spinTurns = 2f;

    [Tooltip("Spin tur sayısına ± oynama (ör. 0.4 => ±0.4 tur).")]
    public float spinJitter = 0.4f;

    [Header("Squash & Stretch")] public bool enableSquashStretch = true;

    [Tooltip("Başlangıçta squash süresi.")]
    public float squashIn = 0.12f;

    [Tooltip("Başlangıçta stretch geri dönüş süresi.")]
    public float stretchBack = 0.18f;

    [Tooltip("Orta kısımda minik breathe süresi.")]
    public float midBreathe = 0.10f;

    [Tooltip("Sonda minik settle süresi.")]
    public float endSettle = 0.12f;

    [Header("Fade + Shrink (Finish)")] public bool enableFadeShrink = true;

    [Tooltip("Fade/Shrink'in başladığı normalize t (0-1).")] [Range(0.0f, 1.0f)]
    public float fadeStartT = 0.7f;

    [Tooltip("Fade ve shrink süresi (sn).")]
    public float fadeShrinkDuration = 0.22f;

    [Header("Target Shake & VFX")] public bool enableTargetShake = true;
    [Tooltip("Hedef shake süresi (sn).")] public float shakeDuration = 0.18f;
    [Tooltip("Hedef shake kuvveti (px).")] public Vector2 shakeStrength = new Vector2(18f, 14f);

    [Tooltip("Hedefte patlayacak VFX (UI altına koyacaksanız WorldSpace/ScreenSpace-Camera uyumlu olmalı).")]
    public GameObject hitVfxPrefab; // optional

    [Tooltip("Hit VFX ömrü (sn). -1 ise otomatik bırak")]
    public float hitVfxLifetime = 1.5f;

    [Header("Sound (Optional)")] public AudioClip sfxSpawn;
    public AudioClip sfxCollect;

    private readonly List<RectTransform> _pool = new();
    private Coroutine _coroutine;
    void Awake() => Instance = this;

    /// <summary>
    /// Efekti başlatır.
    /// </summary>
    [Button]
    public void Play(Action onComplete, int money)
    {
        if (_coroutine != null) StopCoroutine(_coroutine);
        _coroutine = StartCoroutine(PlayRoutine(onComplete, money));
        
    }

    private IEnumerator PlayRoutine(Action onComplete, int money)
    {
        if (coinPrefab == null || target == null)
        {
            Debug.LogWarning("[FlyToUIEffect] coinPrefab veya target atanmamış.");
            yield break;
        }

        _pool.Clear();
        int completed = 0;
        var moneyRemaining = money - 15;
        if (money > 15) money = 15;
        // === SPAWN + SCATTER ===
        for (int i = 0; i < money; i++)
        {
            var coin = Instantiate(coinPrefab, transform);

            Vector3 spawnPos = Vector3.zero;
            if (enableIntroScatter && scatterRadius > 0.01f)
            {
                // Eliptik rastgele nokta
                Vector2 uv = UnityEngine.Random.insideUnitCircle;
                uv = new Vector2(uv.x * scatterEllipse.x, uv.y * scatterEllipse.y);
                spawnPos = new Vector3(uv.x, uv.y, 0f) * scatterRadius;
            }

            coin.anchoredPosition3D = spawnPos;
            coin.localScale = Vector3.one;

            // Fade için CanvasGroup
            var cg = coin.GetComponent<CanvasGroup>();
            if (cg == null) cg = coin.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            _pool.Add(coin);

            // Spawn SFX
            if (sfxSpawn) TryPlaySfx(sfxSpawn);

            // Spawn "pop"
            if (scatterPopDuration > 0f && scatterPopScale > 1f)
            {
                coin.localScale = Vector3.one * 0.85f;
                coin.DOScale(Vector3.one * scatterPopScale, scatterPopDuration).SetUpdate(true)
                    .SetEase(Ease.OutBack, 1.6f)
                    .OnComplete(() => coin.DOScale(Vector3.one, 0.08f).SetUpdate(true));
            }

            // İsteğe bağlı squash başlangıcı
            if (enableSquashStretch)
                coin.localScale = new Vector3(0.92f, 1.08f, 1f);

            yield return new WaitForSecondsRealtime(spawnInterval);
        }

        // === INTRO HOLD ===
        float hold = Mathf.Max(0f, introHold + UnityEngine.Random.Range(0f, introHoldJitter));
        if (hold > 0f)
            yield return new WaitForSecondsRealtime(hold);

        // === UÇUŞLAR ===
        int topCount = Mathf.CeilToInt(_pool.Count * 0.5f);
        int bottomCount = _pool.Count - topCount;

        for (int i = 0; i < _pool.Count; i++)
        {
            RectTransform coin = _pool[i];

            Vector3 start = coin.anchoredPosition3D;
            Vector3 end = coin.parent.InverseTransformPoint(target.position);

            Vector3 dir = end - start;
            float len = dir.magnitude;
            if (len < 0.0001f) len = 0.0001f;
            dir /= len;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

            // Grup: üst/alt
            bool isTop = i < topCount;
            int groupIndex = isTop ? i : (i - topCount);
            int groupSize = isTop ? topCount : bottomCount;

            float within = (groupSize > 1) ? (groupIndex / Mathf.Max(1f, groupSize - 1f)) : 0f;
            float baseStrength = Mathf.Lerp(0.35f, 1f, within) * arcIntensity;
            float sign = isTop ? +1f : -1f;

            // Jitter'lı güç ve yay
            float strengthJitter = 1f + UnityEngine.Random.Range(-arcJitter, arcJitter);
            float strength = Mathf.Max(0f, baseStrength * strengthJitter);

            float arcAmpJ = arcAmplitude * (1f + UnityEngine.Random.Range(-arcJitter, arcJitter));
            float arcPushJ = arcForwardPush * (1f + UnityEngine.Random.Range(-arcJitter, arcJitter));

            Vector3 mid = (start + end) * 0.5f;
            Vector3 forwardPush1 = dir * (-arcPushJ * 0.6f) * strength;
            Vector3 forwardPush2 = dir * (arcPushJ * 0.6f) * strength;

            // Kontrollü U/yay kontrol noktası
            float lateralJitter = (UnityEngine.Random.value - 0.5f) * lateralJitterScale; // -0.1..0.1 tipik
            Vector3 control =
                mid +
                perp * (sign * arcAmpJ * strength) +
                forwardPush1 +
                (perp * lateralJitter * (arcAmpJ * 0.3f));

            // Coin'e özel travel süresi
            float tTime = travelTime + UnityEngine.Random.Range(-travelTimeJitter, travelTimeJitter);
            if (tTime < 0.15f) tTime = 0.15f;

            // Coin'e özel başlatma gecikmesi (stagger)
            float delay = (i * launchDelayPerCoin) + UnityEngine.Random.Range(0f, launchDelayJitter);

            // === Ana Sequence ===
            var seq = DOTween.Sequence().SetUpdate(true).SetDelay(delay);

            // 1) Pozisyon bezier tween
            var posTween = DOTween.To(() => 0f, t =>
                {
                    // Quadratic Bezier: p0, p1, p2
                    Vector3 p0 = start;
                    Vector3 p1 = control + dir * (arcPushJ * strength * t) + forwardPush2 * (1f - t);
                    Vector3 p2 = end;

                    float it = 1f - t;
                    Vector3 pos = (it * it) * p0 + (2f * it * t) * p1 + (t * t) * p2;

                    if (heightCurve != null)
                        pos.y += heightCurve.Evaluate(t) * 40f;

                    coin.anchoredPosition3D = pos;
                }, 1f, tTime)
                .SetEase(Ease.InOutSine);

            seq.Join(posTween);

            // 2) Spin (Z ekseni)
            if (enableSpin && Mathf.Abs(spinTurns) > 0.0001f)
            {
                float zTurns = spinTurns + UnityEngine.Random.Range(-spinJitter, spinJitter);
                float z = 360f * zTurns * (UnityEngine.Random.Range(0.95f, 1.05f));
                var spinTween = coin.DOLocalRotate(
                    new Vector3(0f, 0f, z),
                    tTime,
                    RotateMode.FastBeyond360
                ).SetEase(Ease.Linear);
                seq.Join(spinTween);
            }

            // 3) Squash & Stretch (başlangıç ve orta settle)
            if (enableSquashStretch)
            {
                var squashSeq = DOTween.Sequence().SetUpdate(true).SetDelay(delay);
                // giriş squash
                squashSeq.Append(coin.DOScale(new Vector3(1.18f, 0.84f, 1f), squashIn).SetEase(Ease.OutQuad));
                squashSeq.Append(coin.DOScale(Vector3.one, stretchBack).SetEase(Ease.OutBack));
                // orta nefes
                squashSeq.AppendInterval(Mathf.Max(0f, tTime * 0.4f));
                squashSeq.Append(coin.DOScale(new Vector3(0.94f, 1.08f, 1f), midBreathe).SetEase(Ease.InOutSine));
                squashSeq.Append(coin.DOScale(Vector3.one, endSettle).SetEase(Ease.OutSine));
            }

            // 4) Fade + Shrink (uçuşun son kısmı)
            if (enableFadeShrink)
            {
                var cg = coin.GetComponent<CanvasGroup>();
                if (!cg) cg = coin.gameObject.AddComponent<CanvasGroup>();

                float fadeDelay = delay + Mathf.Clamp01(fadeStartT) * tTime;
                float dur = Mathf.Min(fadeShrinkDuration, Mathf.Max(0.06f, tTime - (fadeDelay - delay)));

                seq.Join(cg.DOFade(0f, dur).SetDelay(fadeDelay - delay).SetEase(Ease.InQuad));
                seq.Join(coin.DOScale(Vector3.zero, dur).SetDelay(fadeDelay - delay).SetEase(Ease.InQuad));
            }

            // 5) Tamamlanınca: SFX, hedef shake, VFX ve destroy
            seq.OnComplete(() =>
            {
                if (sfxCollect) TryPlaySfx(sfxCollect);

                if (enableTargetShake && target != null)
                {
                    target.DOShakeAnchorPos(shakeDuration, shakeStrength, vibrato: 10, randomness: 90f, snapping: false,
                            fadeOut: true)
                        .SetUpdate(true);

                    UIManager.Instance.ScoreAdd(1);
                }

                if (hitVfxPrefab != null && target != null)
                {
                    var parent = target.parent != null ? target.parent : transform;
                    var vfx = Instantiate(hitVfxPrefab, parent);

                    if (vfx.transform is RectTransform vfxRt)
                    {
                        vfxRt.anchorMin = target.anchorMin;
                        vfxRt.anchorMax = target.anchorMax;
                        vfxRt.pivot = target.pivot;
                        vfxRt.anchoredPosition = target.anchoredPosition;
                        vfxRt.localScale = Vector3.one;
                    }
                    else
                    {
                        vfx.transform.position = target.position;
                    }

                    if (hitVfxLifetime > 0f)
                        Destroy(vfx, hitVfxLifetime);
                }

                if (coin) Destroy(coin.gameObject);

                completed++;
                if (completed >= _pool.Count)
                {
                    _pool.Clear();
                    onComplete?.Invoke();
                }
            });
        }

        if (moneyRemaining > 0)
            UIManager.Instance.ScoreAdd(moneyRemaining);
    }

    private void TryPlaySfx(AudioClip clip)
    {
        if (clip == null) return;

        var src = GetComponent<AudioSource>();
        if (!src) src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.pitch = UnityEngine.Random.Range(0.98f, 1.02f);
        src.spatialBlend = 0f; // UI için 2D ses
        src.PlayOneShot(clip);
    }
}