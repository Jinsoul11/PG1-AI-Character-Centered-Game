using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 遗留：关键词会话采集库（旧管线）。
/// Pipeline 终库请用 <see cref="RemiFragmentMemory"/>；Ending 主路径已改读印象库。
/// </summary>
[DisallowMultipleComponent]
public class RemiChatFragmentMemory : MonoBehaviour
{
    public static RemiChatFragmentMemory Instance { get; private set; }

    public const string PrefsStoreKey = "RemiChatFragmentStore";

    [SerializeField] private bool persist = true;
    [SerializeField] private int maxEntries = 6;

    private readonly List<RemiChatFragmentEntry> _entries = new List<RemiChatFragmentEntry>();

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiChatFragmentMemory));
        go.AddComponent<RemiChatFragmentMemory>();
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

    /// <summary>登记一条片段；同 id 只保留首次。</summary>
    public bool TryRecord(RemiChatFragmentEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.id) || string.IsNullOrWhiteSpace(entry.summary))
            return false;

        foreach (RemiChatFragmentEntry existing in _entries)
        {
            if (existing != null && existing.id == entry.id)
                return false;
        }

        _entries.Add(entry);
        TrimToCap();
        Save();
        Debug.Log($"[RemiChatFragmentMemory] Recorded fragment: {entry.id}");
        return true;
    }

    /// <summary>按 id 插入或更新（Analyzer 晋升用）。</summary>
    public bool TryUpsert(RemiChatFragmentEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.id) || string.IsNullOrWhiteSpace(entry.summary))
            return false;

        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i] == null || _entries[i].id != entry.id)
                continue;
            _entries[i] = entry;
            Save();
            Debug.Log($"[RemiChatFragmentMemory] Updated fragment: {entry.id} weight={entry.weight:0.00}");
            return true;
        }

        _entries.Add(entry);
        TrimToCap();
        Save();
        Debug.Log($"[RemiChatFragmentMemory] Recorded fragment: {entry.id} weight={entry.weight:0.00}");
        return true;
    }

    public IReadOnlyList<RemiChatFragmentEntry> GetEntriesOrdered() => _entries;

    public void ClearAll()
    {
        _entries.Clear();
        Save();
    }

    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(PrefsStoreKey);
        if (Instance != null)
            Instance.ClearAll();
    }

    private void TrimToCap()
    {
        if (maxEntries <= 0)
            return;
        while (_entries.Count > maxEntries)
            _entries.RemoveAt(0);
    }

    private void Save()
    {
        if (!persist)
            return;
        var store = new RemiChatFragmentStore { entries = new List<RemiChatFragmentEntry>(_entries) };
        PlayerPrefs.SetString(PrefsStoreKey, JsonUtility.ToJson(store));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        if (!persist || !PlayerPrefs.HasKey(PrefsStoreKey))
            return;

        try
        {
            RemiChatFragmentStore store =
                JsonUtility.FromJson<RemiChatFragmentStore>(PlayerPrefs.GetString(PrefsStoreKey, ""));
            _entries.Clear();
            if (store?.entries == null)
                return;
            foreach (RemiChatFragmentEntry entry in store.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                    continue;
                entry.EnsureTagsMigrated();
                _entries.Add(entry);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[RemiChatFragmentMemory] Load failed: {ex.Message}");
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Clear all chat fragments")]
    private void Editor_Clear() => ClearAll();
#endif
}
