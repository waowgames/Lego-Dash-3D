using UnityEditor;
using UnityEngine;

public class InspectorKeyboardInput
{
    [InitializeOnLoadMethod]
    static void EditorInit()
    {
        System.Reflection.FieldInfo info = typeof(EditorApplication).GetField("globalEventHandler",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        EditorApplication.CallbackFunction value = (EditorApplication.CallbackFunction)info.GetValue(null);

        value += EditorGlobalKeyPress;

        info.SetValue(null, value);
    }


    static void EditorGlobalKeyPress()
    {
        if (Event.current == null || Event.current.type != EventType.KeyDown)
        {
            return;
        }

        switch (Event.current.keyCode)
        {
            case KeyCode.F7:
                Time.timeScale = 1;
                
                break;
            case KeyCode.F8:
                Time.timeScale = 3;
                break;
            case KeyCode.Alpha8:
                UIManager.Instance.ScoreAdd(500);
                break;
            case KeyCode.Alpha7:
                UIManager.Instance.ScoreAdd(-500);
                break;
            case KeyCode.K:
                if (!EditorApplication.isPlaying)
                {
                    Debug.Log("Inspector shortcut K ignored because the editor is not in play mode.");
                    break;
                }

                if (!TryGetLevelMissionManager(out var levelMissionManager))
                {
                    break;
                }

                Debug.Log("Inspector shortcut K pressed; advancing to the next level.");
                levelMissionManager.AdvanceToNextLevel();
                break;
            case KeyCode.J:
                if (!EditorApplication.isPlaying)
                {
                    Debug.Log("Inspector shortcut J ignored because the editor is not in play mode.");
                    break;
                }

                if (!TryGetLevelMissionManager(out var levelMissionManager))
                {
                    break;
                }

                Debug.Log("Inspector shortcut J pressed; returning to the previous level.");
                levelMissionManager.ReturnToPreviousLevel();
                break;

            case KeyCode.F1:
                PlayerPrefs.DeleteAll();
                break;
            default:
                break;
        }
    }

    private static bool TryGetLevelMissionManager(out LevelMissionManager manager)
    {
        manager = null;

        if (LevelMissionManager.Instance != null)
        {
            manager = LevelMissionManager.Instance;
            return true;
        }

        manager = Object.FindObjectOfType<LevelMissionManager>();
        if (manager != null)
        {
            Debug.LogWarning("Inspector shortcut found a LevelMissionManager via FindObjectOfType because the singleton instance was null.");
            return true;
        }

        Debug.LogWarning("Inspector shortcut pressed but no LevelMissionManager exists in the scene.");
        return false;
    }
}
