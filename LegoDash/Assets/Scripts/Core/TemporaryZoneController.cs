using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Stores non-matching bricks until a later task consumes them.
/// </summary>
public class TemporaryZoneController : MonoBehaviour
{
    [SerializeField]
    private int _maxCapacity = 7;

    private readonly List<Brick> _storedBricks = new();

    public int MaxCapacity => _maxCapacity;
    public int CurrentCount => _storedBricks.Count;

    /// <summary>
    /// Returns false if adding the specified amount would overflow capacity.
    /// </summary>
    public bool CanAccept(int amount)
    {
        return CurrentCount + amount <= _maxCapacity;
    }

    public bool AddBricks(List<Brick> bricks)
    {
        if (!CanAccept(bricks.Count))
        {
            return false;
        }

        _storedBricks.AddRange(bricks);
        // TODO: Animate bricks moving into the temporary zone.
        return true;
    }

    /// <summary>
    /// Extracts all bricks matching the requested color from storage.
    /// </summary>
    public List<Brick> ExtractBricksOfColor(BrickColor color)
    {
        var extracted = _storedBricks.Where(brick => brick.Color == color).ToList();
        _storedBricks.RemoveAll(brick => brick.Color == color);
        return extracted;
    }
}
