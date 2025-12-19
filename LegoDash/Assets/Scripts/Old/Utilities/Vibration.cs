using UnityEngine;

public static class Vibration
{
    public static bool isActive = true;

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject _vibrator;

    private static AndroidJavaObject Vibrator
    {
        get
        {
            if (_vibrator != null)
            {
                return _vibrator;
            }

            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                _vibrator = currentActivity?.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }

            return _vibrator;
        }
    }
#endif

    public static void Vibrate()
    {
        //  if (!UIManager.Instance.isVibrationEnabled) return;
        if (!isActive)
        {
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        Vibrator?.Call("vibrate");
#else
        Handheld.Vibrate();
#endif
    }


    public static void Vibrate(long milliseconds)
    { 
        if(!isActive)
        {
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        Vibrator?.Call("vibrate", milliseconds);
#else
        Handheld.Vibrate();
#endif
    }

    public static void Vibrate(long[] pattern, int repeat)
    {
        if(!isActive)
        {
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        Vibrator?.Call("vibrate", pattern, repeat);
#else
        Handheld.Vibrate();
#endif
    }

    public static bool HasVibrator()
    {
        return isAndroid();
    }

    public static void Cancel()
    {
        if (isAndroid())
            Vibrator?.Call("cancel");
    }

    private static bool isAndroid()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Vibrator != null;
#else
        return false;
#endif
    }
}