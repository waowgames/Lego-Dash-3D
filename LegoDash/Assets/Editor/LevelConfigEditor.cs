#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelConfig))]
public class LevelConfigEditor : Editor
{
    private int _totalBricksInput;
    private LevelDifficulty _difficulty = LevelDifficulty.Easy;
    private bool _useSeed;
    private int _seedValue;
    private string _lastValidationMessage;

    private readonly List<LevelAutoGenerator.StandPlan> _previewStands = new();
    private readonly List<LevelAutoGenerator.TaskPlan> _previewTasks = new();
    private readonly List<BrickColor> _previewColors = new();

    private void OnEnable()
    {
        var config = (LevelConfig)target;

        if (_totalBricksInput <= 0)
        {
            if (config.Stands != null && config.Stands.Count > 0)
            {
                _totalBricksInput = config.Stands.Sum(s => s.Bricks.Count);
            }
            else if (TryGetConstructionChildCount(config, out var childCount))
            {
                _totalBricksInput = childCount;
            }
        }

        UpdatePreview(config);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(20);
        DrawAutoGenerator();
    }

    private void DrawAutoGenerator()
    {
        var config = (LevelConfig)target;

        EditorGUILayout.LabelField("Auto Level Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Total bricks can be any positive value. Bricks are distributed across stands with a strict cap of 10 per stand.",
            MessageType.Info);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            _totalBricksInput = Mathf.Max(1, EditorGUILayout.IntField("Total Bricks", _totalBricksInput));
            _difficulty = (LevelDifficulty)EditorGUILayout.EnumPopup("Difficulty", _difficulty);

            _useSeed = EditorGUILayout.Toggle("Use Seed", _useSeed);
            if (_useSeed)
            {
                _seedValue = EditorGUILayout.IntField("Seed", _seedValue);
            }

            if (EditorGUI.EndChangeCheck())
            {
                UpdatePreview(config);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto Generate (Easy)"))
                {
                    GenerateLevel(config, LevelDifficulty.Easy);
                }

                if (GUILayout.Button("Auto Generate (Medium)"))
                {
                    GenerateLevel(config, LevelDifficulty.Medium);
                }

                if (GUILayout.Button("Auto Generate (Hard)"))
                {
                    GenerateLevel(config, LevelDifficulty.Hard);
                }
            }

            if (GUILayout.Button("Validate", GUILayout.Height(24)))
            {
                ValidateCurrentConfig(config);
            }

            if (!string.IsNullOrEmpty(_lastValidationMessage))
            {
                EditorGUILayout.HelpBox(_lastValidationMessage, DetermineMessageType());
            }

            DrawPreview();
        }
    }

    private void GenerateLevel(LevelConfig config, LevelDifficulty difficulty)
    {
        if (config == null)
        {
            _lastValidationMessage = "No LevelConfig selected.";
            return;
        }

        var rng = LevelAutoGenerator.CreateRandom(_useSeed ? _seedValue : (int?)null);
        var pool = LevelAutoGenerator.PickGlobalColorPool(difficulty, rng);
        var (standCounts, adjustedTotal) = LevelAutoGenerator.DistributeBrickCounts(_totalBricksInput);

        if (adjustedTotal != _totalBricksInput)
        {
            _lastValidationMessage = $"Total bricks clamped to {adjustedTotal} to satisfy stand caps.";
            _totalBricksInput = adjustedTotal;
        }

        var stands = LevelAutoGenerator.GenerateStands(difficulty, standCounts, pool, rng);
        var totalValidation = LevelAutoGenerator.ValidateTotals(stands, adjustedTotal);
        if (!totalValidation.Success)
        {
            _lastValidationMessage = totalValidation.Message;
            return;
        }

        foreach (var result in stands.Select((s, i) => LevelAutoGenerator.ValidateStand(difficulty, s.Bricks, i)))
        {
            if (!result.Success)
            {
                _lastValidationMessage = $"Stand {result.StandIndex + 1}: {result.Message}";
                return;
            }
        }

        List<LevelAutoGenerator.TaskPlan> tasks = null;
        LevelAutoGenerator.SolvabilityReport solvableReport;
        int attempt = 0;

        do
        {
            tasks = LevelAutoGenerator.BuildSolvableTasks(stands, difficulty, rng);
            solvableReport = LevelAutoGenerator.SolvabilityCheck(stands, tasks, difficulty);
            attempt++;
        }
        while (!solvableReport.Solvable && attempt < 100);

        if (!solvableReport.Solvable || tasks.Count == 0)
        {
            _lastValidationMessage = solvableReport.Solvable
                ? "No solvable 9-brick tasks could be generated from the current layout."
                : solvableReport.Message;
            return;
        }

        Undo.RegisterCompleteObjectUndo(config, "Auto Generate Level");
        var so = new SerializedObject(config);
        ApplyTasks(so.FindProperty("tasks"), tasks);
        ApplyStands(so.FindProperty("stands"), stands);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(config);

        UpdatePreview(config, stands, tasks, pool, adjustedTotal, difficulty);
        _lastValidationMessage = "Level generated successfully.";
    }

    private void ValidateCurrentConfig(LevelConfig config)
    {
        if (config == null)
        {
            _lastValidationMessage = "No LevelConfig selected.";
            return;
        }

        if (config.Stands == null || config.Stands.Count == 0)
        {
            _lastValidationMessage = "Config has no stands to validate.";
            return;
        }

        var stands = config.Stands.Select(s => new LevelAutoGenerator.StandPlan { Bricks = s.Bricks.ToList() }).ToList();
        var totalCheck = LevelAutoGenerator.ValidateTotals(stands, _totalBricksInput);
        if (!totalCheck.Success)
        {
            _lastValidationMessage = totalCheck.Message;
            return;
        }

        foreach (var result in stands.Select((s, i) => LevelAutoGenerator.ValidateStand(_difficulty, s.Bricks, i)))
        {
            if (!result.Success)
            {
                _lastValidationMessage = $"Stand {result.StandIndex + 1} failed validation: {result.Message}";
                return;
            }
        }

        var tasks = config.Tasks.Select(t => new LevelAutoGenerator.TaskPlan(t.Color, LevelAutoGenerator.TaskChunkSize)).ToList();
        var solvable = LevelAutoGenerator.SolvabilityCheck(stands, tasks, _difficulty);
        _lastValidationMessage = solvable.Solvable
            ? "All stands validated successfully."
            : solvable.Message;
    }

    private void ApplyTasks(SerializedProperty tasksProp, List<LevelAutoGenerator.TaskPlan> tasks)
    {
        // Tasks in runtime always expect 9 bricks of a single color, so write exactly one entry per plan
        // and trust solvability builder to only emit full-sized tasks.
        tasksProp.arraySize = Mathf.Max(1, tasks.Count);

        for (int i = 0; i < tasksProp.arraySize; i++)
        {
            var task = tasks[Mathf.Min(i, tasks.Count - 1)];
            var taskProp = tasksProp.GetArrayElementAtIndex(i);
            var colorProp = taskProp.FindPropertyRelative("color");
            colorProp.enumValueIndex = (int)task.Color;
        }
    }

    private void ApplyStands(SerializedProperty standsProp, List<LevelAutoGenerator.StandPlan> stands)
    {
        standsProp.arraySize = stands.Count;

        for (int i = 0; i < stands.Count; i++)
        {
            var standProp = standsProp.GetArrayElementAtIndex(i);
            var bricksProp = standProp.FindPropertyRelative("bricks");
            bricksProp.arraySize = stands[i].Bricks.Count;

            for (int b = 0; b < stands[i].Bricks.Count; b++)
            {
                bricksProp.GetArrayElementAtIndex(b).enumValueIndex = (int)stands[i].Bricks[b];
            }
        }
    }

    private void UpdatePreview(LevelConfig config)
    {
        if (config == null)
        {
            return;
        }

        var rng = LevelAutoGenerator.CreateRandom(_useSeed ? _seedValue : (int?)null);
        var pool = LevelAutoGenerator.PickGlobalColorPool(_difficulty, rng);
        var (standCounts, adjustedTotal) = LevelAutoGenerator.DistributeBrickCounts(_totalBricksInput);
        var stands = LevelAutoGenerator.GenerateStands(_difficulty, standCounts, pool, rng);
        var tasks = LevelAutoGenerator.BuildSolvableTasks(stands, _difficulty, rng);

        UpdatePreview(config, stands, tasks, pool, adjustedTotal, _difficulty);
    }

    private void UpdatePreview(
        LevelConfig config,
        List<LevelAutoGenerator.StandPlan> stands,
        List<LevelAutoGenerator.TaskPlan> tasks,
        List<BrickColor> pool,
        int total,
        LevelDifficulty difficulty)
    {
        _previewStands.Clear();
        _previewStands.AddRange(stands);

        _previewTasks.Clear();
        _previewTasks.AddRange(tasks);

        _previewColors.Clear();
        _previewColors.AddRange(pool);

        int standSum = stands.Sum(s => s.Bricks.Count);
        int taskSum = tasks.Sum(t => t.RequiredCount);

        if (standSum != total || taskSum != total)
        {
            _lastValidationMessage = $"Preview mismatch detected (stands {standSum}, tasks {taskSum}).";
        }
        else
        {
            _lastValidationMessage = $"Preview ready for {difficulty}.";
        }
    }

    private void DrawPreview()
    {
        if (_previewStands.Count == 0)
        {
            return;
        }

        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Stands: {_previewStands.Count}  |  Total: {_previewStands.Sum(s => s.Bricks.Count)}  |  Difficulty: {_difficulty}");
        EditorGUILayout.LabelField("Global Colors:", string.Join(", ", _previewColors.Select(c => c.ToString())));

        EditorGUILayout.LabelField("Per Stand Brick Counts:", string.Join(", ", _previewStands.Select(s => s.Bricks.Count)));

        for (int i = 0; i < _previewStands.Count; i++)
        {
            var stand = _previewStands[i];
            var colors = string.Join(", ", stand.Bricks.GroupBy(c => c).Select(g => $"{g.Key}:{g.Count()}"));
            EditorGUILayout.LabelField($"Stand {i + 1}: {colors}");
        }

        if (_previewTasks.Count > 0)
        {
            EditorGUILayout.LabelField("Tasks (color x bricks):");
            foreach (var task in _previewTasks)
            {
                EditorGUILayout.LabelField($"  {task.Color} x {task.RequiredCount}");
            }
        }
    }

    private MessageType DetermineMessageType()
    {
        if (!string.IsNullOrEmpty(_lastValidationMessage) && _lastValidationMessage.ToLower().Contains("fail"))
        {
            return MessageType.Error;
        }

        if (!string.IsNullOrEmpty(_lastValidationMessage) && _lastValidationMessage.ToLower().Contains("mismatch"))
        {
            return MessageType.Error;
        }

        return MessageType.Info;
    }

    private bool TryGetConstructionChildCount(LevelConfig config, out int childCount)
    {
        childCount = 0;

        if (config == null || config.ConstructionPrefab == null)
        {
            return false;
        }

        var constructionSO = new SerializedObject(config.ConstructionPrefab);
        var rootProp = constructionSO.FindProperty("_builtObjectRootOverride");
        var root = rootProp?.objectReferenceValue as Transform;

        if (root == null)
        {
            return false;
        }

        childCount = root.childCount;
        return true;
    }
}

#endif
