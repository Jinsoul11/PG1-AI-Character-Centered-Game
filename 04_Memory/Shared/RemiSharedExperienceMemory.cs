using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 共同经历 Memory（只读 Prompt；仅 <see cref="RemiPresenceService.RecordSharedExperience"/> 写入）。
/// </summary>
[DisallowMultipleComponent]
public class RemiSharedExperienceMemory : MonoBehaviour
{
    public static RemiSharedExperienceMemory Instance { get; private set; }

    private const string PrefsKey = "RemiSharedExperienceStore";
    public const string PrefsStoreKey = PrefsKey;

    [SerializeField] private bool persist = true;

    private readonly List<RemiSharedExperienceEntry> _entries = new List<RemiSharedExperienceEntry>();

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiSharedExperienceMemory));
        go.AddComponent<RemiSharedExperienceMemory>();
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

    /// <summary>是否已登记指定共同经历（只比对 id key）。</summary>
    public bool HasRecorded(RemiSharedExperienceId experienceId)
    {
        string idKey = RemiSharedExperienceCatalog.IdKey(experienceId);
        foreach (RemiSharedExperienceEntry existing in _entries)
        {
            if (existing != null && existing.id == idKey)
                return true;
        }

        return false;
    }

    /// <summary>登记一条共同经历；同 id 只保留首次（封存不覆盖）。</summary>
    public bool TryRecord(RemiSharedExperienceId experienceId, RemiWorldTime worldTime, string frameOverride = null)
    {
        string idKey = RemiSharedExperienceCatalog.IdKey(experienceId);
        foreach (RemiSharedExperienceEntry existing in _entries)
        {
            if (existing != null && existing.id == idKey)
                return false;
        }

        string frame = string.IsNullOrWhiteSpace(frameOverride)
            ? RemiSharedExperienceCatalog.DefaultFrame(experienceId)
            : frameOverride.Trim();

        _entries.Add(new RemiSharedExperienceEntry(
            experienceId,
            RemiSharedExperienceCatalog.KindKey(experienceId),
            frame,
            worldTime.storyDay,
            worldTime.phase));

        Save();
        return true;
    }

    public void ClearAll()
    {
        _entries.Clear();
        Save();
    }

    public IReadOnlyList<RemiSharedExperienceEntry> GetRecordedEntriesOrdered()
    {
        var ordered = new List<RemiSharedExperienceEntry>(_entries);
        ordered.Sort(CompareEntriesForEnding);
        return ordered;
    }

    private static int CompareEntriesForEnding(RemiSharedExperienceEntry a, RemiSharedExperienceEntry b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        int sortA = RemiSharedExperienceCatalog.GetSortOrder(a.id);
        int sortB = RemiSharedExperienceCatalog.GetSortOrder(b.id);
        if (sortA != sortB)
            return sortA.CompareTo(sortB);
        if (a.storyDay != b.storyDay)
            return a.storyDay.CompareTo(b.storyDay);
        return a.phase.CompareTo(b.phase);
    }

    public string BuildExperiencesBlock()
    {
        if (_entries.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append("shared_experiences:\n");

        foreach (RemiSharedExperienceEntry e in _entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.frame))
                continue;
            sb.Append($"  - id: {e.id}\n");
            sb.Append($"    kind: {e.kind}\n");
            sb.Append($"    frame: {e.frame.Trim()}\n");
            if (e.storyDay > 0)
                sb.Append($"    when: day{e.storyDay} {PhaseShort((RemiDayPhase)e.phase)}\n");
        }

        return sb.ToString().TrimEnd();
    }

    private static string PhaseShort(RemiDayPhase phase) =>
        phase switch
        {
            RemiDayPhase.Afternoon => "afternoon",
            RemiDayPhase.Evening => "evening",
            RemiDayPhase.Night => "night",
            _ => "morning",
        };

    private void Save()
    {
        if (!persist)
            return;
        var store = new RemiSharedExperienceStore { entries = new List<RemiSharedExperienceEntry>(_entries) };
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(store));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        if (!persist || !PlayerPrefs.HasKey(PrefsKey))
            return;

        try
        {
            RemiSharedExperienceStore store =
                JsonUtility.FromJson<RemiSharedExperienceStore>(PlayerPrefs.GetString(PrefsKey, ""));
            _entries.Clear();
            if (store?.entries == null)
                return;
            foreach (RemiSharedExperienceEntry e in store.entries)
            {
                if (e != null && !string.IsNullOrWhiteSpace(e.frame))
                    _entries.Add(e);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[RemiSharedExperienceMemory] Load failed: {ex.Message}");
        }
    }

    /// <summary>读档后从 PlayerPrefs 重载（可清空）。</summary>
    public void ReloadFromDisk()
    {
        _entries.Clear();
        Load();
    }

#if UNITY_EDITOR
    [ContextMenu("Clear all shared experiences")]
    private void Editor_Clear() => ClearAll();
#endif
}
