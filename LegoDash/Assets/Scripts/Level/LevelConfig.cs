// LevelConfig.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelConfig", menuName = "Levels/Level Config")]
public class LevelConfig : ScriptableObject
{
    [SerializeField] private string levelName = "Level";
    [Tooltip("Renk ve adet bazlı görev listesini tanımlar.")]
    [SerializeField] private List<LevelTaskDefinition> tasks = new();
    [Tooltip("Hangi stantta hangi renk tuğlaların olacağını tanımlar.")]
    [SerializeField] private List<StandLayout> stands = new();
    [SerializeField, Min(0f)] private float timeLimitSeconds = 0f;
    [SerializeField, Min(1)] private int storageCapacity = 7;
    [Tooltip("Bu level için kullanılacak inşaat prefab'ı.")]
    [SerializeField] private Construction constructionPrefab;

    public string LevelName => string.IsNullOrWhiteSpace(levelName) ? name : levelName;
    public IReadOnlyList<LevelTaskDefinition> Tasks => tasks;
    public IReadOnlyList<StandLayout> Stands => stands;
    public float TimeLimitSeconds => Mathf.Max(0f, timeLimitSeconds);
    public int StorageCapacity => Mathf.Max(1, storageCapacity);
    public Construction ConstructionPrefab => constructionPrefab;
}

[System.Serializable]
public class LevelTaskDefinition
{
    [SerializeField] private BrickColor color = BrickColor.Blue;
    public BrickColor Color => color;
    public int RequiredCount => 9;
}

[System.Serializable]
public class StandLayout
{
    [SerializeField] private List<BrickColor> bricks = new();

    public IReadOnlyList<BrickColor> Bricks => bricks;
}
