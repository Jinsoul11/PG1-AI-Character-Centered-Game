using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dialogue Archive：对话真源日志（系统层，无 AI 理解）。
/// 与 ChatHistoryPanel / LLM messageHistory / 手机 Social 存档解耦；清 UI 历史不清本库。
/// </summary>
[DisallowMultipleComponent]
public class RemiDialogueArchive : MonoBehaviour
{
    public static RemiDialogueArchive Instance { get; private set; }

    public const string JsonSaveKey = "RemiDialogueArchive";

    [SerializeField] private bool persist = true;

    private readonly List<RemiDialogueArchiveEntry> _entries = new List<RemiDialogueArchiveEntry>();

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiDialogueArchive));
        go.AddComponent<RemiDialogueArchive>();
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

    /// <summary>
    /// 写入一条语料。自动补 storyDay / depthStage；channel 可由调用方指定，否则用当前 Presence 通道。
    /// </summary>
    public void Record(
        string speaker,
        string content,
        RemiDialogueArchiveSource source,
        RemiInteractionChannel? channel = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        string normalizedSpeaker = NormalizeSpeaker(speaker);
        if (string.IsNullOrEmpty(normalizedSpeaker))
            return;

        // Ema 等非 Remi 线不进本 Archive。
        if (IsExcludedSpeaker(normalizedSpeaker))
            return;

        RemiPresenceService presence = RemiPresenceService.Instance;
        RemiWorldTime worldTime = presence != null
            ? presence.WorldTime
            : RemiWorldTime.BeforeStory;
        RemiDialogueDepthStage depth = presence != null
            ? presence.DialogueDepthStage
            : RemiDialogueDepthStage.Surface;
        RemiInteractionChannel resolvedChannel = channel ?? ResolveCurrentChannel(presence);

        var entry = new RemiDialogueArchiveEntry(
            content.Trim(),
            normalizedSpeaker,
            worldTime.storyDay,
            depth,
            resolvedChannel,
            source);

        _entries.Add(entry);
        Save();
    }

    public static void RecordStatic(
        string speaker,
        string content,
        RemiDialogueArchiveSource source,
        RemiInteractionChannel? channel = null)
    {
        EnsureExists();
        Instance?.Record(speaker, content, source, channel);
    }

    /// <summary>
    /// 调试/测试：按指定 storyDay 写入（不依赖 Presence 当前日）。
    /// 生产路径请继续用 <see cref="Record"/>。
    /// </summary>
    public void RecordExplicit(
        string speaker,
        string content,
        int storyDay,
        RemiDialogueArchiveSource source = RemiDialogueArchiveSource.FreeChat,
        RemiInteractionChannel channel = RemiInteractionChannel.FaceToFace,
        RemiDialogueDepthStage depth = RemiDialogueDepthStage.Surface)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        string normalizedSpeaker = NormalizeSpeaker(speaker);
        if (string.IsNullOrEmpty(normalizedSpeaker) || IsExcludedSpeaker(normalizedSpeaker))
            return;

        int day = Mathf.Max(1, storyDay);
        var entry = new RemiDialogueArchiveEntry(
            content.Trim(),
            normalizedSpeaker,
            day,
            depth,
            channel,
            source);

        _entries.Add(entry);
        Save();
    }

    public static void RecordExplicitStatic(
        string speaker,
        string content,
        int storyDay,
        RemiDialogueArchiveSource source = RemiDialogueArchiveSource.FreeChat,
        RemiInteractionChannel channel = RemiInteractionChannel.FaceToFace,
        RemiDialogueDepthStage depth = RemiDialogueDepthStage.Surface)
    {
        EnsureExists();
        Instance?.RecordExplicit(speaker, content, storyDay, source, channel, depth);
    }

    public IReadOnlyList<RemiDialogueArchiveEntry> GetEntriesOrdered() => _entries;

    public List<RemiDialogueArchiveEntry> GetEntriesForStoryDay(int storyDay)
    {
        var result = new List<RemiDialogueArchiveEntry>();
        foreach (RemiDialogueArchiveEntry entry in _entries)
        {
            if (entry != null && entry.storyDay == storyDay)
                result.Add(entry);
        }

        return result;
    }

    public int Count => _entries.Count;

    public void ClearAll()
    {
        _entries.Clear();
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
        var store = new RemiDialogueArchiveStore
        {
            entries = new List<RemiDialogueArchiveEntry>(_entries),
        };
        JsonMgr.Instance.SaveData(store, JsonSaveKey);
    }

    private void Load()
    {
        if (!persist || JsonMgr.Instance == null)
            return;

        try
        {
            RemiDialogueArchiveStore store =
                JsonMgr.Instance.LoadData<RemiDialogueArchiveStore>(JsonSaveKey);
            _entries.Clear();
            if (store?.entries == null)
                return;
            foreach (RemiDialogueArchiveEntry entry in store.entries)
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.content))
                    _entries.Add(entry);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[RemiDialogueArchive] Load failed: {ex.Message}");
        }
    }

    private static RemiInteractionChannel ResolveCurrentChannel(RemiPresenceService presence)
    {
        if (presence == null)
            return RemiInteractionChannel.FaceToFace;
        return presence.CurrentChannel;
    }

    /// <summary>user / player → player；Remi 保持；其它原样（小写角色名除外）。</summary>
    public static string NormalizeSpeaker(string speaker)
    {
        if (string.IsNullOrWhiteSpace(speaker))
            return "";
        string s = speaker.Trim();
        if (string.Equals(s, "user", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s, "player", System.StringComparison.OrdinalIgnoreCase))
            return "player";
        if (string.Equals(s, "Remi", System.StringComparison.OrdinalIgnoreCase))
            return "Remi";
        return s;
    }

    private static bool IsExcludedSpeaker(string normalizedSpeaker) =>
        string.Equals(normalizedSpeaker, "Ema", System.StringComparison.OrdinalIgnoreCase) ||
        string.Equals(normalizedSpeaker, "system", System.StringComparison.OrdinalIgnoreCase);

#if UNITY_EDITOR
    [ContextMenu("Clear dialogue archive")]
    private void Editor_Clear() => ClearAll();
#endif
}
