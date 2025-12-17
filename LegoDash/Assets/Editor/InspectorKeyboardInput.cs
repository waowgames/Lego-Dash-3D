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

                if (LevelMissionManager.Instance == null)
                {
                    Debug.LogWarning("Inspector shortcut K pressed but LevelMissionManager instance is missing.");
                    break;
                }

                Debug.Log("Inspector shortcut K pressed; advancing to the next level.");
                if (EditorApplication.isPlaying && LevelMissionManager.Instance != null)
                {
                    LevelMissionManager.Instance.AdvanceToNextLevel();
                }
                break;
            case KeyCode.J:
                if (!EditorApplication.isPlaying)
                {
                    Debug.Log("Inspector shortcut J ignored because the editor is not in play mode.");
                    break;
                }

                if (LevelMissionManager.Instance == null)
                {
                    Debug.LogWarning("Inspector shortcut J pressed but LevelMissionManager instance is missing.");
                    break;
                }

                Debug.Log("Inspector shortcut J pressed; returning to the previous level.");
                LevelMissionManager.Instance.ReturnToPreviousLevel();
                break;

            case KeyCode.F1:
                PlayerPrefs.DeleteAll();
                break;
            default:
                break;
        }
    }
}
