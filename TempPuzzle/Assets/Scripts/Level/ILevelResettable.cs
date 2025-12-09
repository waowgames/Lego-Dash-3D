// ILevelResettable.cs

public interface ILevelResettable
{
    // Level başlarken çağrılır.
    void ResetForNewLevel(int levelIndex);
}