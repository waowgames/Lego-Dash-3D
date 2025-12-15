#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelConfig))]
public class LevelConfigEditor : Editor
{
    private const int StandMin = 6;
    private const int StandMax = 10;
    private const int TaskChunkSize = 9;

    private int _totalBricksInput;
    private LevelDifficulty _difficulty = LevelDifficulty.Easy;
    private string _lastValidationMessage;
    private List<LevelAutoGenerator.TaskPlan> _previewTasks = new();
    private List<int> _previewStandCounts = new();
    private IReadOnlyList<BrickColor> _previewColors = new List<BrickColor>();

    private void OnEnable()
    {
        var config = (LevelConfig)target;

        if (_totalBricksInput <= 0 && TryGetConstructionChildCount(config, out var childCount))
        {
            _totalBricksInput = childCount - (childCount % TaskChunkSize);
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
            "Enter TotalBricks (must be divisible by 9) and a difficulty preset. The generator will synchronize tasks and stand stacks so that every brick is consumed when the level ends.",
            MessageType.Info);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            _totalBricksInput = EditorGUILayout.IntField("Total Bricks", _totalBricksInput);
            _difficulty = (LevelDifficulty)EditorGUILayout.EnumPopup("Difficulty", _difficulty);
            if (EditorGUI.EndChangeCheck())
            {
                UpdatePreview(config);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync TotalBricks from Construction Child Count"))
                {
                    SyncTotalBricksFromConstruction(config);
                }

                EditorGUI.BeginDisabledGroup(!IsInputValid(config, _totalBricksInput, out _lastValidationMessage));
                if (GUILayout.Button("Generate Level (Tasks + Stands)", GUILayout.Height(28)))
                {
                    GenerateLevel(config);
                }
                EditorGUI.EndDisabledGroup();
            }

            if (!string.IsNullOrEmpty(_lastValidationMessage))
            {
                var type = DetermineMessageType(config);
                EditorGUILayout.HelpBox(_lastValidationMessage, type);
            }

            DrawPreview();
        }
    }

    private void DrawPreview()
    {
        if (_previewColors == null || _previewColors.Count == 0 || _previewStandCounts == null || _previewStandCounts.Count == 0)
        {
            return;
        }

        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Stands: {_previewStandCounts.Count}  |  Total: {_previewStandCounts.Sum()}  |  Difficulty: {_difficulty}");
        EditorGUILayout.LabelField("Per Stand Brick Counts:", string.Join(", ", _previewStandCounts));
        EditorGUILayout.LabelField("Colors:", string.Join(", ", _previewColors.Select(c => c.ToString())));

        if (_previewTasks != null && _previewTasks.Count > 0)
        {
            EditorGUILayout.LabelField("Tasks (index : color x bricks):");
            for (int i = 0; i < _previewTasks.Count; i++)
            {
                var task = _previewTasks[i];
                EditorGUILayout.LabelField($"  {i + 1}. {task.Color} x {task.RequiredCount}");
            }
        }
    }

    private void SyncTotalBricksFromConstruction(LevelConfig config)
    {
        if (!TryGetConstructionChildCount(config, out var childCount))
        {
            _lastValidationMessage = "Construction BuildObjectRootOverride is missing.";
            return;
        }

        if (childCount % TaskChunkSize != 0)
        {
            _lastValidationMessage = $"Child count ({childCount}) is not divisible by 9. Please adjust the construction pieces.";
            return;
        }

        _totalBricksInput = childCount;
        _lastValidationMessage = $"TotalBricks synced to {childCount} from construction child count.";
        UpdatePreview(config);
    }

    private bool IsInputValid(LevelConfig config, int totalBricks, out string message)
    {
        if (config == null)
        {
            message = "No LevelConfig selected.";
            return false;
        }

        if (totalBricks <= 0)
        {
            message = "TotalBricks must be greater than zero.";
            return false;
        }

        if (totalBricks % TaskChunkSize != 0)
        {
            message = "TotalBricks must be divisible by 9.";
            return false;
        }

        if (!TryGetConstructionChildCount(config, out var childCount))
        {
            message = "Construction BuildObjectRootOverride is missing.";
            return false;
        }

        if (childCount != totalBricks)
        {
            message = $"TotalBricks ({totalBricks}) does not match construction child count ({childCount}). Use Sync to align.";
            return false;
        }

        message = "";
        return true;
    }

    private void GenerateLevel(LevelConfig config)
    {
        if (!IsInputValid(config, _totalBricksInput, out var validation))
        {
            _lastValidationMessage = validation;
            return;
        }

        UpdatePreview(config);

        Undo.RegisterCompleteObjectUndo(config, "Generate Level (Tasks + Stands)");

        var so = new SerializedObject(config);
        var tasksProp = so.FindProperty("tasks");
        var standsProp = so.FindProperty("stands");

        ApplyTasks(tasksProp, _previewTasks);
        ApplyStands(standsProp, _previewStandCounts);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(config);

        _lastValidationMessage = "Level generated successfully.";
    }

    private void ApplyTasks(SerializedProperty tasksProp, List<LevelAutoGenerator.TaskPlan> tasks)
    {
        int taskCount = tasks.Sum(t => t.RequiredCount) / TaskChunkSize;
        tasksProp.arraySize = taskCount;

        int index = 0;
        foreach (var task in tasks)
        {
            int repetitions = Mathf.Max(1, task.RequiredCount / TaskChunkSize);

            for (int r = 0; r < repetitions; r++)
            {
                var taskProp = tasksProp.GetArrayElementAtIndex(index++);
                var colorProp = taskProp.FindPropertyRelative("color");
                colorProp.enumValueIndex = (int)task.Color;
            }
        }
    }

    private void ApplyStands(SerializedProperty standsProp, List<int> standCounts)
    {
        var colors = _previewColors;
        var tasks = _previewTasks;
        var standPlans = LevelAutoGenerator.BuildStandStacks(_difficulty, standCounts, tasks, colors);

        standsProp.arraySize = standPlans.Count;

        for (int i = 0; i < standPlans.Count; i++)
        {
            var standProp = standsProp.GetArrayElementAtIndex(i);
            var bricksProp = standProp.FindPropertyRelative("bricks");
            bricksProp.arraySize = standPlans[i].Bricks.Count;

            for (int b = 0; b < standPlans[i].Bricks.Count; b++)
            {
                bricksProp.GetArrayElementAtIndex(b).enumValueIndex = (int)standPlans[i].Bricks[b];
            }
        }
    }

    private void UpdatePreview(LevelConfig config)
    {
        if (!IsInputValid(config, _totalBricksInput, out var validation))
        {
            _lastValidationMessage = validation;
            return;
        }

        _previewColors = LevelAutoGenerator.GenerateColorSet(_difficulty, _totalBricksInput);
        int preferredStandCount = Mathf.Clamp(config.Stands != null ? config.Stands.Count : StandMin, StandMin, StandMax);
        _previewStandCounts = LevelAutoGenerator.GenerateStandCounts(_difficulty, _totalBricksInput, preferredStandCount);
        var quotas = LevelAutoGenerator.GenerateColorQuotas(_difficulty, _totalBricksInput, _previewColors);
        _previewTasks = LevelAutoGenerator.GenerateTasks(_difficulty, new Dictionary<BrickColor, int>(quotas));

        int taskSum = _previewTasks.Sum(t => t.RequiredCount);
        int standSum = _previewStandCounts.Sum();

        if (taskSum != _totalBricksInput || standSum != _totalBricksInput)
        {
            _lastValidationMessage = $"Internal mismatch detected (tasks: {taskSum}, stands: {standSum}).";
        }
        else
        {
            _lastValidationMessage = "Preview ready.";
        }
    }

    private MessageType DetermineMessageType(LevelConfig config)
    {
        if (!IsInputValid(config, _totalBricksInput, out _))
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
