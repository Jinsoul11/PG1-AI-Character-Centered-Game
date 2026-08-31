using UnityEngine;

/// <summary>
/// 手机 App 是否已对玩家开放（教室开场结束后解锁）。
/// </summary>
public static class PhoneAppAccess
{
    private const string PrefsKey = "PhoneApp_Unlocked";

    private static bool _unlocked;
    private static bool _loaded;

    public static bool IsUnlocked
    {
        get
        {
            EnsureLoaded();
            return _unlocked;
        }
    }

    public static void EnsureLoaded()
    {
        if (_loaded)
            return;
        _unlocked = PlayerPrefs.GetInt(PrefsKey, 0) != 0;
        _loaded = true;
    }

    /// <summary>读档后强制从 PlayerPrefs 刷新（忽略已缓存状态）。</summary>
    public static void ReloadFromPrefs()
    {
        _unlocked = PlayerPrefs.GetInt(PrefsKey, 0) != 0;
        _loaded = true;
    }

    public static void Unlock(bool persist = true)
    {
        _unlocked = true;
        _loaded = true;
        if (persist)
        {
            PlayerPrefs.SetInt(PrefsKey, 1);
            PlayerPrefs.Save();
        }
    }

    public static void ResetForNewGame()
    {
        _unlocked = false;
        _loaded = true;
        PlayerPrefs.DeleteKey(PrefsKey);
    }

#if UNITY_EDITOR
    public static void EditorReset()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        _unlocked = false;
        _loaded = true;
    }
#endif
}
