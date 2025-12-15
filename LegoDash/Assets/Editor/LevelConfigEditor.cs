using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(LevelConfig))]
public class LevelConfigEditor : Editor
{
    private const int PreferredStandCount = 6;
    private const int MinStandCount = 6;
    private const int MaxStandCount = 10;
    private const int MinBricksPerStand = 7;
    private const int MaxBricksPerStand = 10;

    // Sabitler
    private const int TOTAL_STANDS = 16;
    private const int MAX_BRICKS_PER_STAND = 12;
    private const int MIN_CHUNK_SIZE = 2;
    private const int MAX_CHUNK_SIZE = 5;

    public override void OnInspectorGUI()
    {
        // Mevcut Inspector'ı çiz (Manuel ayarlar için)
        DrawDefaultInspector();

        LevelConfig config = (LevelConfig)target;

        GUILayout.Space(20);
        EditorGUILayout.LabelField("Otomatik Level Oluşturucu", EditorStyles.boldLabel);
        
        // Bilgilendirme Kutusu
        if (config.Tasks.Count > 0)
        {
            int totalBricksNeeded = config.Tasks.Count * 9; // Her task sabit 9 brick
            string info = $"Task Sayısı: {config.Tasks.Count}\nToplam Tuğla: {totalBricksNeeded}\n(Stand Kapasitesi: {TOTAL_STANDS * MAX_BRICKS_PER_STAND})";
            EditorGUILayout.HelpBox(info, MessageType.Info);
        }

        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f); // Yeşil buton
        if (GUILayout.Button("Leveli Otomatik Dağıt (Auto Generate)", GUILayout.Height(40)))
        {
            GenerateLevel(config);
        }
        GUI.backgroundColor = Color.white;

        DrawConstructionAutoDistribution(config);
    }

    private void DrawConstructionAutoDistribution(LevelConfig config)
    {
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Construction Bazlı Dağıtım", EditorStyles.boldLabel);

        int existingStandCount = Mathf.Max(0, serializedObject.FindProperty("stands")?.arraySize ?? 0);
        int preferredStandCount = Mathf.Clamp(existingStandCount > 0 ? existingStandCount : PreferredStandCount, MinStandCount, MaxStandCount);

        if (!TryGetTotalBricks(config, out var totalBricks, out var rootDescription))
        {
            EditorGUILayout.HelpBox(rootDescription, MessageType.Warning);
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Button("Auto Distribute Bricks (From Construction)", GUILayout.Height(30));
            EditorGUI.EndDisabledGroup();
            return;
        }

        if (TryGetDistribution(totalBricks, MinBricksPerStand, MaxBricksPerStand, preferredStandCount, MinStandCount, MaxStandCount, out var previewDistribution))
        {
            EditorGUILayout.HelpBox(
                $"Total Bricks: {totalBricks}\nStand Count: {previewDistribution.Count}\nPer Stand (min/max): {MinBricksPerStand}/{MaxBricksPerStand}\nDistribution: {string.Join(", ", previewDistribution)}",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"Total Bricks: {totalBricks}\nStand Count Preference: {preferredStandCount}\nPer Stand (min/max): {MinBricksPerStand}/{MaxBricksPerStand}\nUygun dağıtım bulunamadı. Tuğla sayısını veya stand sayısını ayarlayın.",
                MessageType.Error);
        }

        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
        EditorGUI.BeginDisabledGroup(previewDistribution == null || previewDistribution.Count == 0);
        if (GUILayout.Button("Auto Distribute Bricks (From Construction)", GUILayout.Height(30)))
        {
            AutoDistributeBricks(config, totalBricks, preferredStandCount);
        }
        EditorGUI.EndDisabledGroup();
        GUI.backgroundColor = Color.white;
    }

    private void GenerateLevel(LevelConfig config)
    {
        // İşlemi geri alınabilir yap (Ctrl+Z için)
        Undo.RecordObject(config, "Auto Generate Level");

        // 1. Önce Stand listesini temizle ve 16 boş stand oluştur
        // SerializedObject kullanarak private alanlara güvenli erişim sağlıyoruz
        SerializedObject so = new SerializedObject(config);
        SerializedProperty standsProp = so.FindProperty("stands");
        
        standsProp.ClearArray();
        for (int i = 0; i < TOTAL_STANDS; i++)
        {
            standsProp.InsertArrayElementAtIndex(i);
            // Yeni oluşturulan standın içindeki listeyi de temizlememiz gerekebilir (Unity bazen eski veriyi tutar)
            SerializedProperty standElement = standsProp.GetArrayElementAtIndex(i);
            SerializedProperty bricksProp = standElement.FindPropertyRelative("bricks");
            bricksProp.ClearArray();
        }

        // 2. Tüm Tasklar için tuğla gruplarını (Chunks) hazırla
        List<List<BrickColor>> allChunks = new List<List<BrickColor>>();

        foreach (var task in config.Tasks)
        {
            // Senin kodundaki sabit 9 değerini baz alıyoruz
            int countToDistribute = task.RequiredCount; 
            BrickColor color = task.Color;

            // 9 Adedi 2-5'lik parçalara böl
            List<int> splits = CalculateChunks(countToDistribute);

            foreach (int splitSize in splits)
            {
                List<BrickColor> chunk = new List<BrickColor>();
                for (int k = 0; k < splitSize; k++) chunk.Add(color);
                allChunks.Add(chunk);
            }
        }

        // 3. Grupları karıştır (Rastgelelik için)
        Shuffle(allChunks);

        // 4. Stantlara Dağıtım Algoritması
        DistributeToStands(standsProp, allChunks);

        // Değişiklikleri kaydet
        so.ApplyModifiedProperties();
        Debug.Log($"<color=green>Level Başarıyla Oluşturuldu!</color> {allChunks.Count} parça {TOTAL_STANDS} standa dağıtıldı.");
    }

    // 9 sayısını 2 ile 5 arasında mantıklı parçalara böler (Örn: 5+4, 3+3+3)
    private List<int> CalculateChunks(int total)
    {
        List<int> chunks = new List<int>();
        int remaining = total;

        while (remaining > 0)
        {
            // Rastgele bir boyut seç (2 ile 5 arası)
            // Ancak kalan miktardan büyük olamaz.
            int maxTake = Mathf.Min(MAX_CHUNK_SIZE, remaining);
            
            // Eğer kalan sayı çok küçükse (örneğin 2 kaldıysa) direkt onu almalıyız
            int take = (remaining < MIN_CHUNK_SIZE) ? remaining : Random.Range(MIN_CHUNK_SIZE, maxTake + 1);

            // ÖNEMLİ KONTROL: Eğer bu seçimden sonra geriye 1 kalacaksa, bu yasak (min 2 istiyoruz).
            // O zaman 'take' miktarını 1 azaltarak geriye 2 kalmasını sağlarız.
            if (remaining - take == 1)
            {
                take--;
            }

            // Güvenlik: Eğer take 1'e düştüyse (yukarıdaki azaltmadan dolayı), mecburen son chunk'a ekleme yapacağız.
            // Ama 9 sayısı için (5+4, 4+5, 3+3+3, 3+2+2+2) gibi kombinasyonlarda genelde sorun çıkmaz.
            // Yine de matematiksel garanti için:
            if (take < MIN_CHUNK_SIZE && chunks.Count > 0)
            {
                // Son eklenen parçayı büyüt
                chunks[chunks.Count - 1] += take;
            }
            else
            {
                chunks.Add(take);
            }

            remaining -= take;
        }
        return chunks;
    }

    private void DistributeToStands(SerializedProperty standsProp, List<List<BrickColor>> allChunks)
    {
        // Basit ve etkili bir dağıtım:
        // Önce her standı mümkün olduğunca doldurmaya çalışalım (4-9 arası kuralı için).
        
        foreach (var chunk in allChunks)
        {
            bool placed = false;
            int attempts = 0;
            
            // Rastgele bir stand bulup içine koymayı dene
            while (!placed && attempts < 50)
            {
                int randIndex = Random.Range(0, TOTAL_STANDS);
                SerializedProperty stand = standsProp.GetArrayElementAtIndex(randIndex);
                SerializedProperty bricks = stand.FindPropertyRelative("bricks");

                // Eğer bu standa eklersek 9'u geçiyor mu?
                if (bricks.arraySize + chunk.Count <= MAX_BRICKS_PER_STAND)
                {
                    AddChunkToStand(bricks, chunk);
                    placed = true;
                }
                attempts++;
            }

            // Rastgele bulamadıysa sırayla boş yer ara
            if (!placed)
            {
                for (int i = 0; i < TOTAL_STANDS; i++)
                {
                    SerializedProperty stand = standsProp.GetArrayElementAtIndex(i);
                    SerializedProperty bricks = stand.FindPropertyRelative("bricks");

                    if (bricks.arraySize + chunk.Count <= MAX_BRICKS_PER_STAND)
                    {
                        AddChunkToStand(bricks, chunk);
                        placed = true;
                        break;
                    }
                }
            }

            if (!placed)
            {
                Debug.LogError($"HATA: '{chunk[0]}' rengi için yer bulunamadı! Stantlar çok dolu. Task sayısını azaltmalısın.");
            }
        }
    }

    private void AddChunkToStand(SerializedProperty bricksProp, List<BrickColor> chunk)
    {
        foreach (var color in chunk)
        {
            int index = bricksProp.arraySize;
            bricksProp.InsertArrayElementAtIndex(index);
            bricksProp.GetArrayElementAtIndex(index).enumValueIndex = (int)color;
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    private void AutoDistributeBricks(LevelConfig config, int totalBricks, int preferredStandCount)
    {
        if (!TryGetDistribution(totalBricks, MinBricksPerStand, MaxBricksPerStand, preferredStandCount, MinStandCount, MaxStandCount, out var distribution))
        {
            EditorUtility.DisplayDialog("Auto Distribution Failed", "Uygun dağıtım bulunamadı. Lütfen tuğla veya stand sayılarını kontrol edin.", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(config, "Auto Distribute Bricks");

        SerializedObject configSO = new SerializedObject(config);
        SerializedProperty standsProp = configSO.FindProperty("stands");
        standsProp.arraySize = distribution.Count;

        for (int i = 0; i < distribution.Count; i++)
        {
            SerializedProperty standProp = standsProp.GetArrayElementAtIndex(i);
            SerializedProperty bricksProp = standProp.FindPropertyRelative("bricks");
            ApplyBrickCount(bricksProp, distribution[i]);
        }

        configSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(config);

        Debug.Log($"Auto distribution completed. Total bricks: {totalBricks}, stands: {distribution.Count}. Distribution: {string.Join(", ", distribution)}");
    }

    private bool TryGetTotalBricks(LevelConfig config, out int totalBricks, out string description)
    {
        totalBricks = 0;

        if (config == null)
        {
            description = "LevelConfig referansı bulunamadı.";
            return false;
        }

        if (config.ConstructionPrefab == null)
        {
            description = "Construction Prefab atanmadı. Tuğla sayısı hesaplanamadı.";
            return false;
        }

        SerializedObject constructionSO = new SerializedObject(config.ConstructionPrefab);
        SerializedProperty rootOverrideProp = constructionSO.FindProperty("_builtObjectRootOverride");
        Transform rootOverride = rootOverrideProp?.objectReferenceValue as Transform;

        if (rootOverride == null)
        {
            description = "Construction içinde Build Object Root Override atanmadı. Tuğla dağıtımı yapılmadı.";
            return false;
        }

        totalBricks = rootOverride.childCount;
        description = $"BuildObjectRootOverride: {rootOverride.name} (Child Count: {totalBricks})";
        return true;
    }

    private void ApplyBrickCount(SerializedProperty bricksProp, int desiredCount)
    {
        if (bricksProp == null)
        {
            return;
        }

        int existingCount = bricksProp.arraySize;
        int fillValue = existingCount > 0 ? bricksProp.GetArrayElementAtIndex(0).enumValueIndex : 0;

        // Remove extra bricks
        for (int i = existingCount - 1; i >= desiredCount; i--)
        {
            bricksProp.DeleteArrayElementAtIndex(i);
        }

        // Add missing bricks
        for (int i = bricksProp.arraySize; i < desiredCount; i++)
        {
            bricksProp.InsertArrayElementAtIndex(i);
            bricksProp.GetArrayElementAtIndex(i).enumValueIndex = fillValue;
        }
    }

    private bool TryGetDistribution(
        int total,
        int minPerStand,
        int maxPerStand,
        int preferredStandCount,
        int minStandCount,
        int maxStandCount,
        out List<int> distribution)
    {
        distribution = null;

        if (total <= 0 || minPerStand > maxPerStand || minStandCount > maxStandCount)
        {
            return false;
        }

        List<int> candidates = new List<int>();
        for (int offset = 0; offset <= (maxStandCount - minStandCount); offset++)
        {
            int lower = preferredStandCount - offset;
            int upper = preferredStandCount + offset;

            if (lower >= minStandCount && !candidates.Contains(lower))
            {
                candidates.Add(lower);
            }

            if (upper <= maxStandCount && upper != lower && !candidates.Contains(upper))
            {
                candidates.Add(upper);
            }
        }

        foreach (int standCount in candidates)
        {
            int minPossible = standCount * minPerStand;
            int maxPossible = standCount * maxPerStand;

            if (total < minPossible || total > maxPossible)
            {
                continue;
            }

            var candidateDistribution = new List<int>(standCount);
            for (int i = 0; i < standCount; i++)
            {
                candidateDistribution.Add(minPerStand);
            }

            int remaining = total - minPossible;
            bool progressed = true;

            while (remaining > 0 && progressed)
            {
                progressed = false;

                for (int i = 0; i < standCount && remaining > 0; i++)
                {
                    if (candidateDistribution[i] < maxPerStand)
                    {
                        candidateDistribution[i]++;
                        remaining--;
                        progressed = true;
                    }
                }
            }

            if (remaining == 0)
            {
                distribution = candidateDistribution;
                return true;
            }
        }

        return false;
    }
}