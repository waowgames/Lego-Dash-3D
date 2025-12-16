using System;
using UnityEngine;
using UnityEngine.Events;
using Vector4 = System.Numerics.Vector4;

public static class Events
{ 
    // Sample Events
    //public static Action<Vector3, int> SpawnMoneyEvent;
    //public static Action<string, Vector3, float> SpawnVFXForUIEvent; 
    //public static Action SpecialAnimalStarted;
    public static Action OnPlayerLevelUp; 
     
    // LEVEL
    public static event Action<LevelStartPayload> LevelStarted;
    public static event Action<LevelEndPayload>   LevelEnded;
    public static event Action                    LevelTimeout;

    // Genel amaçlı yardımcı "raise" metotları:
    public static void RaiseLevelStarted(LevelStartPayload payload) => LevelStarted?.Invoke(payload);
    public static void RaiseLevelEnded(LevelEndPayload payload)     => LevelEnded?.Invoke(payload);
    public static void RaiseLevelTimeout()                          => LevelTimeout?.Invoke();

}


// Event payloadları: dilediğin bilgiyi büyütebilirsin.
public struct LevelStartPayload
{
    public int LevelIndex;
    public int Attempt;        // aynı level’ı kaçıncı deneme
    public float TimeScale;    // örn. hızlandırılmış modlar için
}

public struct LevelEndPayload
{
    public int LevelIndex;
    public bool Success;
    public float ElapsedSeconds;
    public int Stars;          // istersen kullan
}