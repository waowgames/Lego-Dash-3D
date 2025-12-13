using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(LevelConfig))]
public class LevelConfigEditor : Editor
{
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
}