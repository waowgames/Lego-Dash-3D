// LevelConfig.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelConfig", menuName = "Levels/Level Config")]
public class LevelConfig : ScriptableObject
{
    [SerializeField] private string levelName = "Level";
    [SerializeField] private List<LevelMissionRequirement> missions = new();
    [SerializeField, Min(0f)] private float timeLimitSeconds = 0f;
    [Header("Fruit Spawning")]
    [SerializeField] private List<FruitSpawnEntry> fruitSpawns = new();
    [SerializeField, Min(0f)] private float fruitMinSpacing = 0.5f;

    public string LevelName => string.IsNullOrWhiteSpace(levelName) ? name : levelName;
    public IReadOnlyList<LevelMissionRequirement> Missions => missions;
    public float TimeLimitSeconds => Mathf.Max(0f, timeLimitSeconds);
    public IReadOnlyList<FruitSpawnEntry> FruitSpawns => fruitSpawns;
    public float FruitMinSpacing => Mathf.Max(0f, fruitMinSpacing);
}

[System.Serializable]
public class FruitSpawnEntry
{
  
}

[System.Serializable]
public class LevelMissionRequirement
{

}
