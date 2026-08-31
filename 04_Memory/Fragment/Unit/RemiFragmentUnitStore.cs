using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fragment Unit 库（系统固化层）。不进 Fragment Memory / 正史。
/// </summary>
[DisallowMultipleComponent]
public class RemiFragmentUnitStore : MonoBehaviour
{
    public static RemiFragmentUnitStore Instance { get; private set; }

    public const string JsonSaveKey = "RemiFragmentUnitStore";

    [SerializeField] private bool persist = true;

    private readonly List<RemiFragmentUnit> _units = new List<RemiFragmentUnit>();
    private int _nextSerial = 1;

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiFragmentUnitStore));
        go.AddComponent<RemiFragmentUnitStore>();
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

    public IReadOnlyList<RemiFragmentUnit> GetUnitsOrdered() => _units;

    public List<RemiFragmentUnit> GetUnitsForStoryDay(int storyDay)
    {
        var list = new List<RemiFragmentUnit>();
        foreach (RemiFragmentUnit unit in _units)
        {
            if (unit != null && unit.storyDay == storyDay)
                list.Add(unit);
        }

        return list;
    }

    public int Count => _units.Count;

    public string AllocateId(int storyDay)
    {
        int serial = _nextSerial++;
        return $"fu_d{storyDay}_{serial:D3}";
    }

    /// <summary>替换某叙事日的全部 Unit（同日重新固化）。</summary>
    public void ReplaceUnitsForStoryDay(int storyDay, List<RemiFragmentUnit> units)
    {
        _units.RemoveAll(u => u != null && u.storyDay == storyDay);
        if (units != null)
        {
            foreach (RemiFragmentUnit unit in units)
            {
                if (unit != null && !string.IsNullOrWhiteSpace(unit.id))
                    _units.Add(unit);
            }
        }

        Save();
    }

    /// <summary>按 id 更新已有 Unit（Analyzer 回写）。</summary>
    public bool TryUpdateUnit(RemiFragmentUnit updated)
    {
        if (updated == null || string.IsNullOrWhiteSpace(updated.id))
            return false;

        for (int i = 0; i < _units.Count; i++)
        {
            if (_units[i] == null || _units[i].id != updated.id)
                continue;
            _units[i] = updated;
            Save();
            return true;
        }

        return false;
    }

    public void SaveNow() => Save();

    public bool HasUnanalyzedUnitsForStoryDay(int storyDay)
    {
        foreach (RemiFragmentUnit unit in _units)
        {
            if (unit == null || unit.storyDay != storyDay)
                continue;
            if (!unit.meaningReady || !unit.weightReady)
                return true;
        }

        return false;
    }

    public void ClearAll()
    {
        _units.Clear();
        _nextSerial = 1;
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
        var data = new RemiFragmentUnitStoreData
        {
            units = new List<RemiFragmentUnit>(_units),
            nextSerial = _nextSerial,
        };
        JsonMgr.Instance.SaveData(data, JsonSaveKey);
    }

    private void Load()
    {
        if (!persist || JsonMgr.Instance == null)
            return;

        try
        {
            RemiFragmentUnitStoreData data =
                JsonMgr.Instance.LoadData<RemiFragmentUnitStoreData>(JsonSaveKey);
            _units.Clear();
            _nextSerial = 1;
            if (data == null)
                return;
            _nextSerial = Mathf.Max(1, data.nextSerial);
            if (data.units == null)
                return;
            foreach (RemiFragmentUnit unit in data.units)
            {
                if (unit != null && !string.IsNullOrWhiteSpace(unit.id))
                    _units.Add(unit);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[RemiFragmentUnitStore] Load failed: {ex.Message}");
        }
    }

    /// <summary>读档后从磁盘重载。</summary>
    public void ReloadFromDisk()
    {
        _units.Clear();
        _nextSerial = 1;
        Load();
    }
}
