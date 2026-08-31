using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Memory Curator 日结果暂存（Pipeline：Curator → 待 Unit/Analyzer）。
/// 不写入 Fragment Memory。
/// </summary>
[DisallowMultipleComponent]
public class RemiMemoryCuratorStore : MonoBehaviour
{
    public static RemiMemoryCuratorStore Instance { get; private set; }

    public const string JsonSaveKey = "RemiMemoryCuratorStore";

    [SerializeField] private bool persist = true;

    private readonly List<RemiMemoryCuratorDayResult> _days = new List<RemiMemoryCuratorDayResult>();

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiMemoryCuratorStore));
        go.AddComponent<RemiMemoryCuratorStore>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public IReadOnlyList<RemiMemoryCuratorDayResult> GetDaysOrdered() => _days;

    public bool TryGetDay(int storyDay, out RemiMemoryCuratorDayResult result)
    {
        foreach (RemiMemoryCuratorDayResult day in _days)
        {
            if (day != null && day.storyDay == storyDay)
            {
                result = day;
                return true;
            }
        }

        result = null;
        return false;
    }

    /// <summary>同日覆盖写入（重新策展）。</summary>
    public void UpsertDay(RemiMemoryCuratorDayResult result)
    {
        if (result == null)
            return;

        for (int i = 0; i < _days.Count; i++)
        {
            if (_days[i] != null && _days[i].storyDay == result.storyDay)
            {
                _days[i] = result;
                Save();
                return;
            }
        }

        _days.Add(result);
        Save();
    }

    public void ClearAll()
    {
        _days.Clear();
        Save();
    }

    public static void ResetProgress()
    {
        if (JsonMgr.Instance != null)
            JsonMgr.Instance.DeleteData(JsonSaveKey);
        if (Instance != null)
            Instance.ClearAll();
    }

    private void Save()
    {
        if (!persist || JsonMgr.Instance == null)
            return;
        var data = new RemiMemoryCuratorStoreData
        {
            days = new List<RemiMemoryCuratorDayResult>(_days),
        };
        JsonMgr.Instance.SaveData(data, JsonSaveKey);
    }

    private void Load()
    {
        if (!persist || JsonMgr.Instance == null)
            return;

        try
        {
            RemiMemoryCuratorStoreData data =
                JsonMgr.Instance.LoadData<RemiMemoryCuratorStoreData>(JsonSaveKey);
            _days.Clear();
            if (data?.days == null)
                return;
            foreach (RemiMemoryCuratorDayResult day in data.days)
            {
                if (day != null)
                    _days.Add(day);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[RemiMemoryCuratorStore] Load failed: {ex.Message}");
        }
    }

    /// <summary>读档后从磁盘重载。</summary>
    public void ReloadFromDisk()
    {
        _days.Clear();
        Load();
    }
}
