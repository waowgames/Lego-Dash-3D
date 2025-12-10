using UnityEngine;

/// <summary>
/// Available brick colors for the LegoDash core mechanics.
/// </summary>
public enum BrickColor
{
    Blue,
    Red,
    Yellow,
    Purple,
    Green
}

/// <summary>
/// Simple data holder for a spawned brick instance.
/// </summary>
public class Brick
{
    public BrickColor Color { get; }
    public GameObject Instance { get; }

    public Brick(BrickColor color, GameObject instance)
    {
        Color = color;
        Instance = instance;
    }
}
