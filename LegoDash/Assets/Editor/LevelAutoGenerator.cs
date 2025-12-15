#if UNITY_EDITOR
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
    private const int MinStandCount = 6;
    private const int MaxStandCount = 10;
    private const int MinBricksPerStand = 7;
    private const int MaxBricksPerStand = 10;
    private static readonly BrickColor[] Palette = new[]
    {
        BrickColor.Blue,
        BrickColor.Red,
        BrickColor.Yellow,
        BrickColor.Purple,
        BrickColor.Green,
        BrickColor.Pink,
        BrickColor.Orange,
    };

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
        public List<BrickColor> Bricks { get; } = new();
    }

    public static IReadOnlyList<BrickColor> GenerateColorSet(LevelDifficulty difficulty, int totalBricks)
    {
        int desiredCount = difficulty switch
        {
            LevelDifficulty.Easy => 3,
            LevelDifficulty.Medium => Mathf.Clamp(4 + totalBricks / 180, 4, 6),
            _ => Mathf.Clamp(6 + totalBricks / 270, 6, Mathf.Min(Palette.Length, 8)),
        };

        desiredCount = Mathf.Clamp(desiredCount, 1, Palette.Length);
        return Palette.Take(desiredCount).ToArray();
    }

    public static List<int> GenerateStandCounts(LevelDifficulty difficulty, int totalBricks, int preferredStandCount)
    {
        int standCount = Mathf.Clamp(preferredStandCount, MinStandCount, MaxStandCount);

        int minStandCountForCapacity = Mathf.CeilToInt((float)totalBricks / Mathf.Max(1, MaxBricksPerStand));
        standCount = Mathf.Clamp(Mathf.Max(standCount, minStandCountForCapacity), MinStandCount, MaxStandCount);

        while (standCount < MaxStandCount && totalBricks / standCount > MaxBricksPerStand)
        {
            standCount++;
        }

        while (standCount > MinStandCount && totalBricks / standCount < MinBricksPerStand)
        {
            standCount--;
        }

        return BuildStandCounts(difficulty, standCount, totalBricks);
    }

    public static Dictionary<BrickColor, int> GenerateColorQuotas(LevelDifficulty difficulty, int totalBricks, IReadOnlyList<BrickColor> colors)
    {
        var weights = new List<float>();

        switch (difficulty)
        {
            case LevelDifficulty.Easy:
                weights.AddRange(new[] { 0.45f, 0.35f, 0.2f });
                break;
            case LevelDifficulty.Medium:
                for (int i = 0; i < colors.Count; i++)
                {
                    float tilt = 0.15f * (1f - (float)i / Mathf.Max(1, colors.Count - 1));
                    weights.Add(1f - tilt);
                }
                break;
            default:
                for (int i = 0; i < colors.Count; i++)
                {
                    float wobble = 0.05f * (i % 2 == 0 ? 1f : -1f);
                    weights.Add(1f + wobble);
                }
                break;
        }

        AdjustWeightCount(weights, colors.Count);

        float weightSum = Mathf.Max(0.0001f, weights.Sum());
        var quotas = new Dictionary<BrickColor, int>();
        int allocated = 0;

        for (int i = 0; i < colors.Count; i++)
        {
            int count = Mathf.Max(0, Mathf.FloorToInt(totalBricks * (weights[i] / weightSum)));
            quotas[colors[i]] = count;
            allocated += count;
        }

        int remainder = totalBricks - allocated;
        var order = Enumerable.Range(0, colors.Count)
            .OrderByDescending(i => weights[i])
            .ThenBy(i => i);

        foreach (int index in order)
        {
            if (remainder <= 0)
            {
                break;
            }

            quotas[colors[index]]++;
            remainder--;
        }

        while (remainder > 0)
        {
            for (int i = 0; i < colors.Count && remainder > 0; i++)
            {
                quotas[colors[i]]++;
                remainder--;
            }
        }

        SnapQuotasToTaskSize(quotas, totalBricks);
        return quotas;
    }

    public static List<TaskPlan> GenerateTasks(LevelDifficulty difficulty, Dictionary<BrickColor, int> quotas)
    {
        var tasks = new List<TaskPlan>();
        int baseChunk = TaskSize();
        int chunkSize = difficulty switch
        {
            LevelDifficulty.Easy => baseChunk * 2,
            LevelDifficulty.Medium => baseChunk + 3,
            _ => baseChunk,
        };

        chunkSize = Mathf.Max(baseChunk, chunkSize);

        var colorOrder = quotas.Keys.ToList();
        int colorIndex = 0;

        while (quotas.Values.Any(v => v > 0))
        {
            BrickColor color = colorOrder[colorIndex % colorOrder.Count];
            int remaining = quotas[color];

            if (remaining <= 0)
            {
                colorIndex++;
                continue;
            }

            int desired = Mathf.Min(remaining, chunkSize);
            desired -= desired % baseChunk;
            desired = Mathf.Clamp(desired, baseChunk, remaining);
            quotas[color] -= desired;
            tasks.Add(new TaskPlan(color, desired));

            colorIndex++;

            if (difficulty == LevelDifficulty.Easy && colorIndex % colorOrder.Count == 0)
            {
                colorIndex = 0;
            }
        }

        return tasks;
    }

    public static List<StandPlan> BuildStandStacks(LevelDifficulty difficulty, List<int> standCounts, List<TaskPlan> tasks, IReadOnlyList<BrickColor> colors)
    {
        _ = colors;
        var stands = standCounts.Select(_ => new StandPlan()).ToList();
        var remainingPerStand = standCounts.ToArray();
        var weights = BuildWeights(difficulty, standCounts.Count);
        var standOrder = Enumerable.Range(0, standCounts.Count)
            .OrderByDescending(i => weights[i])
            .ToList();

        int rotation = 0;

        foreach (var task in tasks)
        {
            int bricksLeft = task.RequiredCount;

            while (bricksLeft > 0)
            {
                IEnumerable<int> sequence = difficulty switch
                {
                    LevelDifficulty.Easy => standOrder,
                    LevelDifficulty.Medium => Rotate(standOrder, rotation),
                    _ => Alternate(standCounts.Count, rotation),
                };

                foreach (int standIndex in sequence)
                {
                    if (bricksLeft <= 0)
                    {
                        break;
                    }

                    if (remainingPerStand[standIndex] <= 0)
                    {
                        continue;
                    }

                    stands[standIndex].Bricks.Add(task.Color);
                    remainingPerStand[standIndex]--;
                    bricksLeft--;
                }

                rotation++;
            }
        }

        return stands;
    }

    private static List<int> BuildStandCounts(LevelDifficulty difficulty, int standCount, int totalBricks)
    {
        var weights = BuildWeights(difficulty, standCount);
        float weightSum = Mathf.Max(0.0001f, weights.Sum());

        var distribution = new List<int>();
        int allocated = 0;

        for (int i = 0; i < standCount; i++)
        {
            int count = Mathf.FloorToInt(totalBricks * (weights[i] / weightSum));
            count = Mathf.Clamp(count, MinBricksPerStand, MaxBricksPerStand);
            distribution.Add(count);
            allocated += count;
        }

        int remainder = totalBricks - allocated;
        var order = Enumerable.Range(0, standCount)
            .OrderByDescending(i => weights[i])
            .ThenBy(i => i)
            .ToList();

        int cursor = 0;
        int safety = 0;
        while (remainder > 0 && order.Count > 0 && safety < standCount * standCount)
        {
            int index = order[cursor % order.Count];
            int available = MaxBricksPerStand - distribution[index];

            if (available > 0)
            {
                int add = Mathf.Min(available, remainder);
                distribution[index] += add;
                remainder -= add;
            }

            cursor++;
            safety++;
        }

        cursor = 0;
        safety = 0;
        while (remainder > 0 && safety < standCount * standCount)
        {
            int index = cursor % standCount;
            int available = MaxBricksPerStand - distribution[index];

            if (available > 0)
            {
                int add = Mathf.Min(available, remainder);
                distribution[index] += add;
                remainder -= add;
            }

            cursor++;
            safety++;
        }

        return distribution;
    }

    private static List<float> BuildWeights(LevelDifficulty difficulty, int standCount)
    {
        var weights = new List<float>(standCount);

        for (int i = 0; i < standCount; i++)
        {
            float t = standCount <= 1 ? 0f : (float)i / (standCount - 1);
            float edgeBias = 1f - Mathf.Abs(0.5f - t) * 2f;

            switch (difficulty)
            {
                case LevelDifficulty.Easy:
                    weights.Add(1f + edgeBias * 0.6f);
                    break;
                case LevelDifficulty.Medium:
                    weights.Add(1f + edgeBias * 0.35f);
                    break;
                default:
                    weights.Add(1f + edgeBias * 0.1f);
                    break;
            }
        }

        return weights;
    }

    private static void AdjustWeightCount(List<float> weights, int desired)
    {
        if (weights.Count == desired)
        {
            return;
        }

        if (weights.Count > desired)
        {
            weights.RemoveRange(desired, weights.Count - desired);
            return;
        }

        while (weights.Count < desired)
        {
            weights.Add(weights.Last());
        }
    }

    private static IEnumerable<int> Rotate(List<int> source, int offset)
    {
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            yield return source[(i + offset) % count];
        }
    }

    private static IEnumerable<int> Alternate(int count, int offset)
    {
        for (int i = 0; i < count; i++)
        {
            int index = (i % 2 == 0) ? i / 2 : count - 1 - i / 2;
            yield return (index + offset) % count;
        }
    }

    private static void SnapQuotasToTaskSize(Dictionary<BrickColor, int> quotas, int totalBricks)
    {
        var colors = quotas.Keys.ToList();
        var adjusted = new Dictionary<BrickColor, int>();
        int allocated = 0;

        foreach (var color in colors)
        {
            int baseCount = Mathf.Max(0, quotas[color] - quotas[color] % TaskSize());
            adjusted[color] = baseCount;
            allocated += baseCount;
        }

        int remainder = totalBricks - allocated;
        int step = TaskSize();

        var order = colors
            .OrderByDescending(c => quotas[c])
            .ThenBy(c => (int)c)
            .ToList();

        int cursor = 0;
        while (remainder >= step && order.Count > 0)
        {
            var color = order[cursor % order.Count];
            adjusted[color] += step;
            remainder -= step;
            cursor++;
        }

        quotas.Clear();
        foreach (var kvp in adjusted)
        {
            quotas[kvp.Key] = kvp.Value;
        }
    }

    private static int TaskSize()
    {
        return 9;
    }
}

#endif
