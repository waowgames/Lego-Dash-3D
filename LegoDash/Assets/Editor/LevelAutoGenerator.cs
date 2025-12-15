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

    public struct DifficultyProfile
    {
        public int AccessibleLayersWindow;
        public float RequiredReachableRatio;
        public float PriorityStrength;
    }

    public struct SolvabilityReport
    {
        public bool Solvable;
        public int FailedTaskIndex;
        public BrickColor FailedColor;
        public int ReachableCount;
        public int RequiredCount;
        public string Message;
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
        totalBricks = Mathf.Max(1, totalBricks);
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
            totalBricks = standCount * MaxBricksPerStand;
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

    public static List<TaskPlan> BuildSolvableTasks(IEnumerable<StandPlan> sourceStands, LevelDifficulty difficulty, System.Random rng)
    {
        var profile = GetDifficultyProfile(difficulty);
        var working = sourceStands
            .Select(s => new StandPlan { Bricks = new List<BrickColor>(s.Bricks) })
            .ToList();

        var tasks = new List<TaskPlan>();
        int totalBricks = working.Sum(s => s.Bricks.Count);
        int guard = 0;

        while (working.Any(s => s.Bricks.Count > 0) && guard < totalBricks * 3)
        {
            guard++;
            var reachable = GetReachableCounts(working, 1);
            if (reachable.Count == 0)
            {
                break;
            }

            var weighted = reachable
                .Select(kvp =>
                {
                    int windowCount = CountWithinWindow(working, kvp.Key, profile.AccessibleLayersWindow);
                    float score = kvp.Value + windowCount * profile.PriorityStrength;
                    return (Color: kvp.Key, Score: score, Top: kvp.Value);
                })
                .OrderByDescending(item => item.Score)
                .ThenBy(item => rng?.NextDouble() ?? UnityEngine.Random.value)
                .ToList();

            var selected = weighted.First();
            int desired = Mathf.Max(1, Mathf.FloorToInt(selected.Top / Mathf.Max(0.01f, profile.RequiredReachableRatio)));
            desired = Mathf.Clamp(desired, 1, selected.Top);

            int collected = CollectColor(working, selected.Color, desired);
            if (collected <= 0)
            {
                // Fallback: remove a single top brick to avoid infinite loops.
                var firstStand = working.First(s => s.Bricks.Count > 0);
                var color = firstStand.Bricks[0];
                firstStand.Bricks.RemoveAt(0);
                tasks.Add(new TaskPlan(color, 1));
                continue;
            }

            tasks.Add(new TaskPlan(selected.Color, collected));
        }

        return tasks;
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

    public static SolvabilityReport SolvabilityCheck(IEnumerable<StandPlan> stands, IEnumerable<TaskPlan> tasks, LevelDifficulty difficulty)
    {
        var profile = GetDifficultyProfile(difficulty);
        var working = stands
            .Select(s => new StandPlan { Bricks = new List<BrickColor>(s.Bricks) })
            .ToList();

        var taskList = tasks.ToList();
        for (int i = 0; i < taskList.Count; i++)
        {
            var task = taskList[i];
            int remaining = Mathf.Max(0, task.RequiredCount);
            int attempts = 0;

            while (remaining > 0 && attempts < working.Sum(s => s.Bricks.Count) + 1)
            {
                attempts++;
                var standIndex = working.FindIndex(s => s.Bricks.Count > 0 && s.Bricks[0] == task.Color);
                if (standIndex < 0)
                {
                    int reachable = CountWithinWindow(working, task.Color, profile.AccessibleLayersWindow);
                    return new SolvabilityReport
                    {
                        Solvable = false,
                        FailedTaskIndex = i,
                        FailedColor = task.Color,
                        ReachableCount = reachable,
                        RequiredCount = remaining,
                        Message = $"Task {i + 1} failed: need {remaining} {task.Color} but only {reachable} reachable."
                    };
                }

                working[standIndex].Bricks.RemoveAt(0);
                remaining--;
            }
        }

        int leftover = working.Sum(s => s.Bricks.Count);
        if (leftover > 0)
        {
            return new SolvabilityReport
            {
                Solvable = false,
                FailedTaskIndex = taskList.Count - 1,
                FailedColor = taskList.LastOrDefault()?.Color ?? BrickColor.Blue,
                ReachableCount = leftover,
                RequiredCount = 0,
                Message = $"Tasks completed but {leftover} bricks remain."
            };
        }

        return new SolvabilityReport { Solvable = true, Message = "Level solvable." };
    }

    public static DifficultyProfile GetDifficultyProfile(LevelDifficulty difficulty)
    {
        return difficulty switch
        {
            LevelDifficulty.Easy => new DifficultyProfile
            {
                AccessibleLayersWindow = 2,
                RequiredReachableRatio = 1.2f,
                PriorityStrength = 0.6f,
            },
            LevelDifficulty.Medium => new DifficultyProfile
            {
                AccessibleLayersWindow = 4,
                RequiredReachableRatio = 1.05f,
                PriorityStrength = 0.35f,
            },
            _ => new DifficultyProfile
            {
                AccessibleLayersWindow = 6,
                RequiredReachableRatio = 0.95f,
                PriorityStrength = 0.2f,
            },
        };
    }

    private static int CollectColor(List<StandPlan> stands, BrickColor color, int desired)
    {
        int collected = 0;
        int guard = stands.Sum(s => s.Bricks.Count) + 1;

        while (collected < desired && guard-- > 0)
        {
            var standIndex = stands.FindIndex(s => s.Bricks.Count > 0 && s.Bricks[0] == color);
            if (standIndex < 0)
            {
                break;
            }

            stands[standIndex].Bricks.RemoveAt(0);
            collected++;
        }

        return collected;
    }

    private static Dictionary<BrickColor, int> GetReachableCounts(IEnumerable<StandPlan> stands, int layers)
    {
        var counts = new Dictionary<BrickColor, int>();
        foreach (var stand in stands)
        {
            int take = Mathf.Min(layers, stand.Bricks.Count);
            for (int i = 0; i < take; i++)
            {
                var color = stand.Bricks[i];
                if (!counts.ContainsKey(color))
                {
                    counts[color] = 0;
                }

                counts[color]++;
            }
        }

        return counts;
    }

    private static int CountWithinWindow(IEnumerable<StandPlan> stands, BrickColor color, int window)
    {
        int count = 0;
        foreach (var stand in stands)
        {
            int take = Mathf.Min(window, stand.Bricks.Count);
            for (int i = 0; i < take; i++)
            {
                if (stand.Bricks[i] == color)
                {
                    count++;
                }
            }
        }

        return count;
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
