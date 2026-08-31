using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>单条 Remi 朋友圈动态（文案与配图由策划给定，按关系阶段解锁发布）。</summary>
[Serializable]
public class RemiMomentsPostDefinition
{
    [Tooltip("唯一 ID，存档用。")]
    public string postId;

    [Tooltip("关系达到该阶段时自动发布（含更高阶段，不会重复发）。")]
    public RemiDialogueDepthStage publishAtStage = RemiDialogueDepthStage.Surface;

    [TextArea(2, 6)]
    public string body;

    [Tooltip("列表里显示的时间文案，如「2小时前」。")]
    public string timeLabel = "刚刚";

    [Tooltip("是否显示配图占位（Inspector 可拖 Sprite）。")]
    public bool hasImage;

    public Sprite imageSprite;

    [Tooltip("无 Sprite 时占位色。")]
    public Color imagePlaceholderColor = new Color(0.28f, 0.28f, 0.3f, 1f);

    [TextArea(1, 3)]
    [Tooltip("玩家评论后 Remi 在聊天里回复时用的系统背景（可选）。")]
    public string commentReplyContext;
}

/// <summary>动态目录：可在 Project 里创建 RemiMomentsCatalog 资源并拖给 RemiMomentsService。</summary>
[CreateAssetMenu(fileName = "RemiMomentsCatalog", menuName = "Remi/Social/Moments Catalog")]
public class RemiMomentsCatalog : ScriptableObject
{
    public List<RemiMomentsPostDefinition> posts = new List<RemiMomentsPostDefinition>();

    public static RemiMomentsCatalog CreateBuiltInDefault()
    {
        var cat = CreateInstance<RemiMomentsCatalog>();
        cat.posts = new List<RemiMomentsPostDefinition>
        {
            new RemiMomentsPostDefinition
            {
                postId = "surface_window",
                publishAtStage = RemiDialogueDepthStage.Surface,
                body = "教室窗外的云。",
                timeLabel = "刚刚",
                hasImage = true,
                imagePlaceholderColor = new Color(0.35f, 0.38f, 0.42f, 1f),
                commentReplyContext = "玩家在 Remi 的 Surface 阶段动态下留言。动态原文：教室窗外的云。",
            },
            new RemiMomentsPostDefinition
            {
                postId = "surface_campus",
                publishAtStage = RemiDialogueDepthStage.Surface,
                body = "下课。",
                timeLabel = "1小时前",
                hasImage = false,
                commentReplyContext = "玩家在 Remi 的 Surface 阶段动态（下课）下留言。",
            },
            new RemiMomentsPostDefinition
            {
                postId = "relational_library",
                publishAtStage = RemiDialogueDepthStage.Relational,
                body = "今天在图书馆一坐就是一下午。好像也没那么难熬。",
                timeLabel = "刚刚",
                hasImage = true,
                imagePlaceholderColor = new Color(0.3f, 0.32f, 0.36f, 1f),
                commentReplyContext =
                    "玩家在 Remi 的 Relational 阶段动态下留言。动态原文提到图书馆一下午。",
            },
            new RemiMomentsPostDefinition
            {
                postId = "relational_thanks",
                publishAtStage = RemiDialogueDepthStage.Relational,
                body = "有些事，想起来还是会想说声谢谢。",
                timeLabel = "昨天",
                hasImage = false,
                commentReplyContext = "玩家在 Remi 带感谢意味的动态下留言。",
            },
            new RemiMomentsPostDefinition
            {
                postId = "influential_restaurant",
                publishAtStage = RemiDialogueDepthStage.Influential,
                body = "今天发现了一家装修超好看的餐厅，下次想抓个小朋友陪我一起去！",
                timeLabel = "刚刚",
                hasImage = true,
                imagePlaceholderColor = new Color(0.42f, 0.32f, 0.28f, 1f),
                commentReplyContext =
                    "玩家在 Remi 的 Influential 阶段动态下留言。动态原文提到餐厅、想找人一起去。",
            },
            new RemiMomentsPostDefinition
            {
                postId = "influential_evening",
                publishAtStage = RemiDialogueDepthStage.Influential,
                body = "晚上风有点凉，但心情还不错。",
                timeLabel = "3小时前",
                hasImage = false,
                commentReplyContext = "玩家在 Remi 的 Influential 阶段晚间心情动态下留言。",
            },
        };
        return cat;
    }
}

[Serializable]
public class RemiMomentsPublishedRecord
{
    public string postId;
    public int storyDay;
    public int phase;
    public int beat;
}

[Serializable]
public class RemiMomentsPlayerComment
{
    public string postId;
    public string text;
    public int worldStoryDay;
    public int worldPhase;
    public int worldBeat;
    /// <summary>旧档 UTC 兜底。</summary>
    public long utcTicks;
}

[Serializable]
public class RemiMomentsSaveData
{
    public List<RemiMomentsPublishedRecord> published = new List<RemiMomentsPublishedRecord>();
    /// <summary>旧档仅 ID 列表；加载时迁移到 published。</summary>
    public List<string> publishedPostIds = new List<string>();
    public List<string> likedPostIds = new List<string>();
    public List<RemiMomentsPlayerComment> comments = new List<RemiMomentsPlayerComment>();
}

/// <summary>已发布、用于 UI 渲染的一条动态。</summary>
public class RemiMomentsPublishedPost
{
    public RemiMomentsPostDefinition Definition;
    public RemiWorldTime PublishedAt;
    public bool LikedByPlayer;
    public List<RemiMomentsPlayerComment> Comments = new List<RemiMomentsPlayerComment>();
}

/// <summary>
/// Remi 朋友圈：按 <see cref="RemiDialogueDepthStage"/> 发布预设动态，持久化点赞/评论。
/// 发布时间戳使用 <see cref="RemiWorldTime"/>（与日程同一叙事时钟）。
/// </summary>
[DisallowMultipleComponent]
public class RemiMomentsService : MonoBehaviour
{
    public const string SaveKey = "RemiMoments_State";

    public static RemiMomentsService Instance { get; private set; }

    [SerializeField] private RemiMomentsCatalog catalog;
    [SerializeField] private bool persistState = true;
    [Tooltip("为 true 时须剧情日开始后才会发布动态。")]
    [SerializeField] private bool requireStoryDayStarted = true;

    private RemiMomentsSaveData _save = new RemiMomentsSaveData();
    private readonly List<RemiMomentsPublishedPost> _feed = new List<RemiMomentsPublishedPost>();
    private RemiDialogueDepthStage _lastSyncedStage = (RemiDialogueDepthStage)(-1);

    public event Action FeedChanged;

    public IReadOnlyList<RemiMomentsPublishedPost> Feed => _feed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (catalog == null)
            catalog = Resources.Load<RemiMomentsCatalog>("Remi/RemiMomentsCatalog");
        if (catalog == null)
            catalog = RemiMomentsCatalog.CreateBuiltInDefault();

        LoadState();
        MigrateLegacyPublishedIds();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        SyncForCurrentStage(force: true);
    }

    public void SyncForCurrentStage(bool force = false)
    {
        RemiDialogueDepthStage stage = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.DialogueDepthStage
            : RemiDialogueDepthStage.Surface;

        if (!force && stage == _lastSyncedStage)
            return;

        _lastSyncedStage = stage;
        PublishPostsUpToStage(stage);
        RebuildFeed();
        FeedChanged?.Invoke();
    }

    private void PublishPostsUpToStage(RemiDialogueDepthStage stage)
    {
        if (catalog?.posts == null) return;
        if (requireStoryDayStarted && RemiPresenceService.Instance != null &&
            !RemiPresenceService.Instance.StoryDayStarted)
            return;

        RemiWorldTime stamp = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.CaptureWorldTime()
            : RemiWorldTime.BeforeStory;

        foreach (RemiMomentsPostDefinition def in catalog.posts)
        {
            if (def == null || string.IsNullOrEmpty(def.postId)) continue;
            if (def.publishAtStage > stage) continue;
            if (IsPublished(def.postId)) continue;
            PublishRecord(def.postId, stamp);
        }

        SaveState();
    }

    private void PublishRecord(string postId, RemiWorldTime at)
    {
        _save.published.Add(new RemiMomentsPublishedRecord
        {
            postId = postId,
            storyDay = at.storyDay,
            phase = (int)at.phase,
            beat = at.beat,
        });
    }

    private bool IsPublished(string postId)
    {
        if (_save.published != null)
        {
            foreach (RemiMomentsPublishedRecord r in _save.published)
            {
                if (r != null && r.postId == postId)
                    return true;
            }
        }

        return _save.publishedPostIds != null && _save.publishedPostIds.Contains(postId);
    }

    private void MigrateLegacyPublishedIds()
    {
        if (_save.publishedPostIds == null || _save.publishedPostIds.Count == 0)
            return;

        _save.published ??= new List<RemiMomentsPublishedRecord>();
        RemiWorldTime baseTime = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.CaptureWorldTime()
            : new RemiWorldTime { storyDay = 1, phase = RemiDayPhase.Morning, beat = 1 };

        foreach (string id in _save.publishedPostIds)
        {
            if (string.IsNullOrEmpty(id) || IsPublished(id)) continue;
            PublishRecord(id, baseTime);
            baseTime.beat = Mathf.Max(0, baseTime.beat - 1);
        }

        _save.publishedPostIds.Clear();
        SaveState();
    }

    private void RebuildFeed()
    {
        _feed.Clear();
        if (catalog?.posts == null || _save.published == null) return;

        for (int i = catalog.posts.Count - 1; i >= 0; i--)
        {
            RemiMomentsPostDefinition def = catalog.posts[i];
            if (def == null || string.IsNullOrEmpty(def.postId)) continue;

            RemiMomentsPublishedRecord rec = FindPublishedRecord(def.postId);
            if (rec == null) continue;

            var pub = new RemiMomentsPublishedPost
            {
                Definition = def,
                PublishedAt = RecordToWorldTime(rec),
                LikedByPlayer = _save.likedPostIds != null && _save.likedPostIds.Contains(def.postId),
            };

            if (_save.comments != null)
            {
                foreach (RemiMomentsPlayerComment c in _save.comments)
                {
                    if (c != null && c.postId == def.postId)
                        pub.Comments.Add(c);
                }
            }

            _feed.Add(pub);
        }
    }

    private RemiMomentsPublishedRecord FindPublishedRecord(string postId)
    {
        if (_save.published == null) return null;
        foreach (RemiMomentsPublishedRecord r in _save.published)
        {
            if (r != null && r.postId == postId)
                return r;
        }

        return null;
    }

    private static RemiWorldTime RecordToWorldTime(RemiMomentsPublishedRecord rec) =>
        new RemiWorldTime
        {
            storyDay = rec.storyDay,
            phase = (RemiDayPhase)rec.phase,
            beat = rec.beat,
        };

    public bool TryToggleLike(string postId)
    {
        if (string.IsNullOrEmpty(postId) || !IsPublished(postId))
            return false;

        _save.likedPostIds ??= new List<string>();
        if (_save.likedPostIds.Contains(postId))
            _save.likedPostIds.Remove(postId);
        else
            _save.likedPostIds.Add(postId);

        SaveState();
        RebuildFeed();
        FeedChanged?.Invoke();
        return true;
    }

    public bool TryAddComment(string postId, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrEmpty(postId)) return false;
        if (!IsPublished(postId)) return false;

        RemiWorldTime now = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.CaptureWorldTime()
            : RemiWorldTime.BeforeStory;

        _save.comments ??= new List<RemiMomentsPlayerComment>();
        _save.comments.Add(new RemiMomentsPlayerComment
        {
            postId = postId,
            text = text.Trim(),
            worldStoryDay = now.storyDay,
            worldPhase = (int)now.phase,
            worldBeat = now.beat,
        });

        SaveState();
        RebuildFeed();
        FeedChanged?.Invoke();
        return true;
    }

    public bool TryGetPostDefinition(string postId, out RemiMomentsPostDefinition def)
    {
        def = null;
        if (catalog?.posts == null || string.IsNullOrEmpty(postId)) return false;
        foreach (RemiMomentsPostDefinition p in catalog.posts)
        {
            if (p != null && p.postId == postId)
            {
                def = p;
                return true;
            }
        }

        return false;
    }

    public void NotifyStageAdvanced(RemiDialogueDepthStage newStage)
    {
        PublishPostsUpToStage(newStage);
        _lastSyncedStage = newStage;
        RebuildFeed();
        FeedChanged?.Invoke();
    }

    private void LoadState()
    {
        if (!persistState) return;
        RemiMomentsSaveData loaded = JsonMgr.Instance.LoadData<RemiMomentsSaveData>(SaveKey);
        if (loaded == null)
        {
            _save = new RemiMomentsSaveData();
            return;
        }

        _save = loaded;
        _save.published ??= new List<RemiMomentsPublishedRecord>();
        _save.publishedPostIds ??= new List<string>();
        _save.likedPostIds ??= new List<string>();
        _save.comments ??= new List<RemiMomentsPlayerComment>();
    }

    private void SaveState()
    {
        if (!persistState) return;
        JsonMgr.Instance.SaveData(_save, SaveKey);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Clear Moments Save")]
    private void Editor_ClearSave()
    {
        JsonMgr.Instance.DeleteData(SaveKey);
        _save = new RemiMomentsSaveData();
        _feed.Clear();
        _lastSyncedStage = (RemiDialogueDepthStage)(-1);
        FeedChanged?.Invoke();
    }

    [ContextMenu("Debug/Republish For Current Stage")]
    private void Editor_Republish()
    {
        SyncForCurrentStage(force: true);
    }
#endif
}
