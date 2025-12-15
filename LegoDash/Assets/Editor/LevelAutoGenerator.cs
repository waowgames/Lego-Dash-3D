#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum LevelDifficulty
{
    Easy,
    Medium,
    Hard,
}

public static class LevelAutoGenerator
{
    public const int StandMin = 6;
    public const int StandMax = 10;
    public const int MaxBricksPerStand = 10;
    public const int TaskChunkSize = 9;
    public const int ColorBlockSize = 9;

    private static readonly BrickColor[] Palette = Enum.GetValues(typeof(BrickColor)).Cast<BrickColor>().ToArray();

    public class TaskPlan
    {
        public BrickColor Color { get; }
        public int RequiredCount { get; }

        public TaskPlan(BrickColor color, int requiredCount)
        {
            Color = color;
            RequiredCount = requiredCount;
        }
    }

    public class StandPlan
    {
        public List<BrickColor> Bricks { get; set; } = new();
    }

    public struct ValidationResult
    {
        public bool Success;
        public string Message;
        public int StandIndex;
    }

    public static System.Random CreateRandom(int? seed)
    {
        return seed.HasValue ? new System.Random(seed.Value) : null;
    }

    public static List<BrickColor> PickGlobalColorPool(LevelDifficulty difficulty, System.Random rng)
    {
        int desired = difficulty switch
        {
            LevelDifficulty.Easy => 3,
            LevelDifficulty.Medium => 4,
            _ => Mathf.Max(6, Palette.Length),
        };

        desired = Mathf.Clamp(desired, 1, Palette.Length);
        var colors = Palette.ToList();
        Shuffle(colors, rng);
        return colors.Take(desired).ToList();
    }

    public static (List<int> Counts, int AdjustedTotal) DistributeBrickCounts(int totalBricks)
    {
        totalBricks = Mathf.Max(ColorBlockSize, RoundDownToColorBlock(totalBricks));
        int standCount = Mathf.CeilToInt(totalBricks / (float)MaxBricksPerStand);

        if (standCount < StandMin && totalBricks >= StandMin)
        {
            standCount = StandMin;
        }

        standCount = Mathf.Clamp(standCount, 1, StandMax);
        if (standCount > totalBricks)
        {
            standCount = totalBricks;
        }

        while (standCount < StandMax && totalBricks > standCount * MaxBricksPerStand)
        {
            standCount++;
        }

        if (totalBricks > standCount * MaxBricksPerStand)
        {
            totalBricks = RoundDownToColorBlock(standCount * MaxBricksPerStand);
        }

        int baseCount = totalBricks / standCount;
        int remainder = totalBricks % standCount;
        baseCount = Mathf.Max(1, baseCount);

        var counts = new List<int>(standCount);
        for (int i = 0; i < standCount; i++)
        {
            int count = baseCount + (i < remainder ? 1 : 0);
            counts.Add(Mathf.Clamp(count, 1, MaxBricksPerStand));
        }

        return (counts, counts.Sum());
    }

    public static int RoundDownToColorBlock(int value)
    {
        return Mathf.Max(ColorBlockSize, (value / ColorBlockSize) * ColorBlockSize);
    }

    public static List<StandPlan> GenerateStands(LevelDifficulty difficulty, List<int> standCounts, List<BrickColor> globalPool, System.Random rng)
    {
        var stands = new List<StandPlan>();

        foreach (int brickCount in standCounts)
        {
            var bricks = GenerateStandColorDistribution(difficulty, brickCount, globalPool, rng);
            stands.Add(new StandPlan { Bricks = bricks });
        }

        return stands;
    }

    public static List<BrickColor> GenerateStandColorDistribution(LevelDifficulty difficulty, int brickCount, IReadOnlyList<BrickColor> availableColors, System.Random rng)
    {
        brickCount = Mathf.Clamp(brickCount, 1, MaxBricksPerStand);
        var colors = new List<BrickColor>(availableColors);
        Shuffle(colors, rng);

        switch (difficulty)
        {
            case LevelDifficulty.Easy:
                return GenerateEasy(brickCount, colors, rng);
            case LevelDifficulty.Medium:
                return GenerateMedium(brickCount, colors, rng);
            default:
                return GenerateHard(brickCount, colors, rng);
        }
    }

    public static List<TaskPlan> BuildTasksFromStands(IEnumerable<StandPlan> stands)
    {
        var exposureOrder = BuildAccessibilityOrder(stands);
        var runningCounts = new Dictionary<BrickColor, int>();
        var tasks = new List<TaskPlan>();

        foreach (var color in exposureOrder)
        {
            if (!runningCounts.ContainsKey(color))
            {
                runningCounts[color] = 0;
            }

            runningCounts[color]++;

            if (runningCounts[color] >= TaskChunkSize)
            {
                tasks.Add(new TaskPlan(color, TaskChunkSize));
                runningCounts[color] -= TaskChunkSize;
            }
        }

        return tasks;
    }

    public static List<BrickColor> BuildAccessibilityOrder(IEnumerable<StandPlan> stands)
    {
        var working = stands
            .Select(s => s == null ? new List<BrickColor>() : new List<BrickColor>(s.Bricks))
            .ToList();

        var order = new List<BrickColor>();
        bool progressed;

        do
        {
            progressed = false;

            for (int i = 0; i < working.Count; i++)
            {
                var bricks = working[i];
                if (bricks.Count == 0)
                {
                    continue;
                }

                progressed = true;
                int lastIndex = bricks.Count - 1; // Top-most brick sits at the end of the list.
                order.Add(bricks[lastIndex]);
                bricks.RemoveAt(lastIndex);
            }
        }
        while (progressed);

        return order;
    }

    public static ValidationResult ValidateStand(LevelDifficulty difficulty, IReadOnlyList<BrickColor> bricks, int standIndex)
    {
        if (bricks == null)
        {
            return new ValidationResult { Success = false, StandIndex = standIndex, Message = "Stand is null." };
        }

        if (bricks.Count == 0)
        {
            return new ValidationResult { Success = false, StandIndex = standIndex, Message = "Stand has no bricks." };
        }

        if (bricks.Count > MaxBricksPerStand)
        {
            return new ValidationResult { Success = false, StandIndex = standIndex, Message = "Stand exceeds 10 brick cap." };
        }

        var colorCounts = bricks.GroupBy(b => b).ToDictionary(g => g.Key, g => g.Count());
        int distinct = colorCounts.Count;
        float maxShare = colorCounts.Values.Max() / (float)bricks.Count;

        switch (difficulty)
        {
            case LevelDifficulty.Easy:
                if (distinct < 2 || distinct > 3)
                {
                    return Fail(standIndex, "Easy stands must use 2 (rarely 3) colors.");
                }

                if (maxShare < 0.6f - Mathf.Epsilon)
                {
                    return Fail(standIndex, "Easy stands need a dominant color (>=60%).");
                }

                break;

            case LevelDifficulty.Medium:
                if (distinct != 3)
                {
                    return Fail(standIndex, "Medium stands must use exactly 3 colors.");
                }

                if (maxShare > 0.5f + Mathf.Epsilon)
                {
                    return Fail(standIndex, "Medium stands must keep colors below 50%.");
                }

                if (!IsBalanced(colorCounts.Values, 1))
                {
                    return Fail(standIndex, "Medium stands should be evenly distributed (±1).");
                }

                break;

            default:
                if (distinct < 4 && bricks.Count >= 4)
                {
                    return Fail(standIndex, "Hard stands prefer 4 distinct colors.");
                }

                if (maxShare > 0.4f + Mathf.Epsilon)
                {
                    return Fail(standIndex, "Hard stands must keep colors below 40%.");
                }

                if (!IsBalanced(colorCounts.Values, 1))
                {
                    return Fail(standIndex, "Hard stands should stay balanced (±1).");
                }

                break;
        }

        return new ValidationResult { Success = true, StandIndex = standIndex, Message = "" };
    }

    public static ValidationResult ValidateTotals(IEnumerable<StandPlan> stands, int expectedTotal)
    {
        int total = stands.Sum(s => s.Bricks.Count);
        if (total != expectedTotal)
        {
            return new ValidationResult
            {
                Success = false,
                StandIndex = -1,
                Message = $"Total bricks mismatch. Expected {expectedTotal}, found {total}."
            };
        }

        return new ValidationResult { Success = true, StandIndex = -1, Message = string.Empty };
    }

    public static ValidationResult ValidateColorBlocks(IEnumerable<StandPlan> stands, int expectedTotal)
    {
        if (expectedTotal % ColorBlockSize != 0)
        {
            return new ValidationResult
            {
                Success = false,
                StandIndex = -1,
                Message = $"Total bricks must be a multiple of {ColorBlockSize}."
            };
        }

        var totals = BuildColorTotals(stands);
        int total = totals.Values.Sum();
        if (total != expectedTotal)
        {
            return new ValidationResult
            {
                Success = false,
                StandIndex = -1,
                Message = $"Color totals mismatch. Expected {expectedTotal}, found {total}."
            };
        }

        foreach (var kvp in totals)
        {
            if (kvp.Value % ColorBlockSize != 0)
            {
                return new ValidationResult
                {
                    Success = false,
                    StandIndex = -1,
                    Message = $"{kvp.Key} count {kvp.Value} must be a multiple of {ColorBlockSize}."
                };
            }
        }

        return new ValidationResult { Success = true, StandIndex = -1, Message = string.Empty };
    }

    public static void NormalizeStandsToColorBlocks(List<StandPlan> stands, List<BrickColor> palette, int targetTotal)
    {
        if (stands == null || stands.Count == 0)
        {
            return;
        }

        targetTotal = Mathf.Max(ColorBlockSize, targetTotal);
        targetTotal = RoundDownToColorBlock(targetTotal);

        int currentTotal = stands.Sum(s => s.Bricks.Count);
        if (currentTotal > targetTotal)
        {
            TrimStands(stands, currentTotal - targetTotal);
        }

        var currentTotals = BuildColorTotals(stands);
        var desiredTotals = CalculateDesiredColorTotals(currentTotals, palette, targetTotal);
        BalanceColorCounts(stands, currentTotals, desiredTotals);
    }

    public static Dictionary<BrickColor, int> BuildColorTotals(IEnumerable<StandPlan> stands)
    {
        var totals = new Dictionary<BrickColor, int>();

        foreach (var stand in stands)
        {
            foreach (var color in stand.Bricks)
            {
                if (!totals.ContainsKey(color))
                {
                    totals[color] = 0;
                }

                totals[color]++;
            }
        }

        return totals;
    }

    private static void TrimStands(List<StandPlan> stands, int bricksToRemove)
    {
        for (int i = 0; i < stands.Count && bricksToRemove > 0; i++)
        {
            var stand = stands[i];
            while (stand.Bricks.Count > 1 && bricksToRemove > 0)
            {
                stand.Bricks.RemoveAt(stand.Bricks.Count - 1);
                bricksToRemove--;
            }
        }
    }

    private static Dictionary<BrickColor, int> CalculateDesiredColorTotals(
        IReadOnlyDictionary<BrickColor, int> currentTotals,
        IReadOnlyList<BrickColor> palette,
        int targetTotal)
    {
        var desired = new Dictionary<BrickColor, int>();
        int remaining = targetTotal;

        foreach (var color in palette)
        {
            int current = currentTotals.TryGetValue(color, out var count) ? count : 0;
            int floor = (current / ColorBlockSize) * ColorBlockSize;
            desired[color] = floor;
            remaining -= floor;
        }

        var ordered = palette
            .OrderByDescending(c => currentTotals.TryGetValue(c, out var count) ? count % ColorBlockSize : 0)
            .ToList();

        int index = 0;
        while (remaining > 0 && ordered.Count > 0)
        {
            var color = ordered[index % ordered.Count];
            desired[color] += ColorBlockSize;
            remaining -= ColorBlockSize;
            index++;
        }

        return desired;
    }

    private static void BalanceColorCounts(
        List<StandPlan> stands,
        Dictionary<BrickColor, int> currentTotals,
        IReadOnlyDictionary<BrickColor, int> desiredTotals)
    {
        int GetCurrent(BrickColor color)
        {
            return currentTotals.TryGetValue(color, out var count) ? count : 0;
        }

        var deficit = desiredTotals
            .Where(kvp => kvp.Value > GetCurrent(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value - GetCurrent(kvp.Key));

        var excess = desiredTotals
            .Where(kvp => kvp.Value < GetCurrent(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => GetCurrent(kvp.Key) - kvp.Value);

        if (deficit.Count == 0 || excess.Count == 0)
        {
            return;
        }

        foreach (var stand in stands)
        {
            for (int i = 0; i < stand.Bricks.Count; i++)
            {
                var color = stand.Bricks[i];
                if (!excess.TryGetValue(color, out int remaining) || remaining <= 0)
                {
                    continue;
                }

                var targetColor = deficit.FirstOrDefault(kvp => kvp.Value > 0).Key;
                if (!deficit.ContainsKey(targetColor))
                {
                    return;
                }

                stand.Bricks[i] = targetColor;
                excess[color]--;
                deficit[targetColor]--;

                if (excess[color] <= 0)
                {
                    excess.Remove(color);
                }

                if (deficit[targetColor] <= 0)
                {
                    deficit.Remove(targetColor);
                }

                if (deficit.Count == 0 || excess.Count == 0)
                {
                    return;
                }
            }
        }
    }

    private static ValidationResult Fail(int standIndex, string message)
    {
        return new ValidationResult { Success = false, StandIndex = standIndex, Message = message };
    }

    private static List<BrickColor> GenerateEasy(int brickCount, List<BrickColor> colors, System.Random rng)
    {
        int distinct = Mathf.Clamp(colors.Count >= 3 && brickCount >= 3 && Chance(rng, 0.2f) ? 3 : 2, 1, colors.Count);
        var chosen = colors.Take(distinct).ToList();
        int dominant = Mathf.CeilToInt(brickCount * 0.6f);
        int remaining = brickCount - dominant;

        var bricks = new List<BrickColor>();
        bricks.AddRange(Enumerable.Repeat(chosen[0], dominant));

        if (distinct == 1)
        {
            bricks.AddRange(Enumerable.Repeat(chosen[0], remaining));
        }
        else if (distinct == 2)
        {
            bricks.AddRange(Enumerable.Repeat(chosen[1], remaining));
        }
        else
        {
            int split = Chance(rng, 0.5f) ? remaining / 2 : Mathf.CeilToInt(remaining / 2f);
            bricks.AddRange(Enumerable.Repeat(chosen[1], split));
            bricks.AddRange(Enumerable.Repeat(chosen[2], remaining - split));
        }

        Shuffle(bricks, rng);
        return bricks;
    }

    private static List<BrickColor> GenerateMedium(int brickCount, List<BrickColor> colors, System.Random rng)
    {
        int distinct = Math.Min(3, colors.Count);
        var chosen = colors.Take(distinct).ToList();

        var counts = Enumerable.Repeat(brickCount / distinct, distinct).ToArray();
        int remainder = brickCount % distinct;

        for (int i = 0; i < remainder; i++)
        {
            counts[i % distinct]++;
        }

        if (counts.Max() / (float)brickCount > 0.5f)
        {
            counts[0] = Mathf.CeilToInt(brickCount * 0.5f);
        }

        var bricks = new List<BrickColor>();
        for (int i = 0; i < distinct; i++)
        {
            bricks.AddRange(Enumerable.Repeat(chosen[i], counts[i]));
        }

        Shuffle(bricks, rng);
        return bricks;
    }

    private static List<BrickColor> GenerateHard(int brickCount, List<BrickColor> colors, System.Random rng)
    {
        int distinct = Mathf.Min(colors.Count, brickCount >= 4 ? 4 : brickCount);
        distinct = Mathf.Max(1, distinct);
        var chosen = colors.Take(distinct).ToList();

        var counts = Enumerable.Repeat(brickCount / distinct, distinct).ToArray();
        int remainder = brickCount % distinct;

        for (int i = 0; i < remainder; i++)
        {
            counts[i % distinct]++;
        }

        for (int i = 0; i < counts.Length; i++)
        {
            int maxAllowed = Mathf.CeilToInt(brickCount * 0.4f);
            counts[i] = Mathf.Min(counts[i], maxAllowed);
        }

        var bricks = new List<BrickColor>();
        for (int i = 0; i < distinct; i++)
        {
            bricks.AddRange(Enumerable.Repeat(chosen[i], counts[i]));
        }

        while (bricks.Count < brickCount)
        {
            bricks.Add(chosen[bricks.Count % distinct]);
        }

        Shuffle(bricks, rng);
        return bricks;
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        if (list == null || list.Count == 0)
        {
            return;
        }

        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = rng?.Next(0, i + 1) ?? UnityEngine.Random.Range(0, i + 1);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }

    private static bool Chance(System.Random rng, float probability)
    {
        float roll = rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value;
        return roll <= probability;
    }

    private static bool IsBalanced(IEnumerable<int> counts, int tolerance)
    {
        int min = counts.Min();
        int max = counts.Max();
        return max - min <= tolerance;
    }
}

#endif
