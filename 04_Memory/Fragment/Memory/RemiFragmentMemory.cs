using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fragment Memory：过程印象终库（Pipeline 第六段）。
/// 不进日常 [MEMORY] 正史；Ending 回忆只读本库。
/// </summary>
[DisallowMultipleComponent]
public class RemiFragmentMemory : MonoBehaviour
{
    public static RemiFragmentMemory Instance { get; private set; }

    public const string JsonSaveKey = "RemiFragmentMemory";

    [SerializeField] private bool persist = true;
    [SerializeField] private int maxImpressions = 24;

    private readonly List<RemiFragmentImpression> _impressions = new List<RemiFragmentImpression>();

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiFragmentMemory));
        go.AddComponent<RemiFragmentMemory>();
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

    public IReadOnlyList<RemiFragmentImpression> GetImpressionsOrdered() => _impressions;

    public int Count => _impressions.Count;

    public List<RemiFragmentImpression> GetImpressionsForStoryDay(int storyDay)
    {
        var list = new List<RemiFragmentImpression>();
        foreach (RemiFragmentImpression impression in _impressions)
        {
            if (impression != null && impression.storyDay == storyDay)
                list.Add(impression);
        }

        return list;
    }

    /// <summary>按 id 插入或更新。</summary>
    public bool TryUpsert(RemiFragmentImpression impression)
    {
        if (impression == null ||
            string.IsNullOrWhiteSpace(impression.id) ||
            string.IsNullOrWhiteSpace(impression.summary))
            return false;

        for (int i = 0; i < _impressions.Count; i++)
        {
            if (_impressions[i] == null || _impressions[i].id != impression.id)
                continue;
            _impressions[i] = impression;
            Save();
            Debug.Log(
                $"[RemiFragmentMemory] Updated {impression.id} weight={impression.weight:0.00}");
            return true;
        }

        _impressions.Add(impression);
        TrimToCap();
        Save();
        Debug.Log(
            $"[RemiFragmentMemory] Recorded {impression.id} weight={impression.weight:0.00}");
        return true;
    }

    public void ClearAll()
    {
        _impressions.Clear();
        Save();
    }

    public static void ResetProgress()
    {
        if (JsonMgr.Instance != null)
            JsonMgr.Instance.DeleteData(JsonSaveKey);
        if (Instance != null)
            Instance.ClearAll();
    }

    private void TrimToCap()
    {
        if (maxImpressions <= 0)
            return;
        // 超限时丢掉 weight 最低的，保留高回忆概率。
        while (_impressions.Count > maxImpressions)
        {
            int worst = 0;
            float worstW = float.MaxValue;
            for (int i = 0; i < _impressions.Count; i++)
            {
                RemiFragmentImpression imp = _impressions[i];
                float w = imp != null ? imp.weight : float.MaxValue;
                if (w < worstW)
                {
                    worstW = w;
                    worst = i;
                }
            }

            _impressions.RemoveAt(worst);
        }
    }

    private void Save()
    {
        if (!persist || JsonMgr.Instance == null)
            return;
        var data = new RemiFragmentMemoryStoreData
        {
            impressions = new List<RemiFragmentImpression>(_impressions),
        };
        JsonMgr.Instance.SaveData(data, JsonSaveKey);
    }

    private void Load()
    {
        if (!persist || JsonMgr.Instance == null)
            return;

        try
        {
            RemiFragmentMemoryStoreData data =
                JsonMgr.Instance.LoadData<RemiFragmentMemoryStoreData>(JsonSaveKey);
            _impressions.Clear();
            if (data?.impressions == null)
                return;

            bool backfilledAliases = false;
            foreach (RemiFragmentImpression impression in data.impressions)
            {
                if (impression == null || string.IsNullOrWhiteSpace(impression.id))
                    continue;

                if (!impression.recallEligible)
                {
                    RemiFragmentTopicAliasBuilder.EnsureRecallEligible(impression);
                    if (impression.recallEligible)
                        backfilledAliases = true;
                }

                _impressions.Add(impression);
            }

            if (backfilledAliases)
                Save();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[RemiFragmentMemory] Load failed: {ex.Message}");
        }
    }

    /// <summary>读档后从磁盘重载。</summary>
    public void ReloadFromDisk()
    {
        _impressions.Clear();
        Load();
    }

#if UNITY_EDITOR
    [ContextMenu("Clear fragment memory")]
    private void Editor_Clear() => ClearAll();
#endif
}
