using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Demo 角色主动开口文案台账。
/// <list type="bullet">
/// <item>面对面 / 手机 LLM：只填 initiator（director_context），经 <see cref="PromptedDialogueAgent.SendSystem"/>；手机另见 <see cref="RemiPhoneSendSystem"/></item>
/// <item>手机固定 fallback：Day2 邀请 / Day3 开场等，仅在 LLM 失败或禁止重打时写入聊天</item>
/// <item>面对面闲置话题：SendSystem 提议 + 话题按钮文案（按场景）</item>
/// </list>
/// 挂在场景空物体上集中改字。新增事件：加命名字段，或追加 <see cref="extraEntries"/>。
/// SendSystem 不再附带独立 narrative-intent 文案段。
/// </summary>
[DisallowMultipleComponent]
public class RemiSendSystemContentManager : MonoBehaviour
{
    public static RemiSendSystemContentManager Instance { get; private set; }

    [Serializable]
    public struct Entry
    {
        [Tooltip("稳定键：流程代码用这个 id 查找。建议小写蛇形，如 day1_book_request。")]
        public string id;

        [Tooltip("Inspector 备注，不影响运行。")]
        public string label;

        [TextArea(3, 10)]
        [Tooltip("SendSystem：director_context。手机 LLM 条目不要把 fallback 写在这里。")]
        public string initiatorContext;
    }

    [Header("Day1 · 告别托付找书（SendSystem）")]
    [TextArea(3, 10)]
    [SerializeField]
    private string day1BookRequestContext =
        "你刚和玩家聊完，你突然想起来自己有一本名为《AI游戏入门》的书放在教室里，你希望玩家帮忙找一下。";

    [Header("Day1 · 玩家问起后托付找书（SendSystem）")]
    [TextArea(3, 10)]
    [SerializeField]
    private string day1BookRequestPlayerAskedContext =
        "玩家主动问起你在忙什么。你正在筹备学生作品展，但找不到教室里的《AI游戏入门》这本书，请玩家帮忙找一下。";

    [Header("Day1 · Remi 保底主动托付找书（SendSystem）")]
    [TextArea(3, 10)]
    [SerializeField]
    private string day1BookRequestGuaranteeContext =
        "你和玩家在聊天时，突然想起教室里的《AI游戏入门》还没找到，于是你主动开口请玩家帮忙。";

    [Header("Day1 · 交书后感谢（SendSystem）")]
    [TextArea(3, 10)]
    [SerializeField]
    private string day1BookThanksContext =
        "玩家在教室里找到了《AI游戏入门》，并交到你手上。对玩家表示感谢";

    [Header("Day2 · 手机共现邀请（SendSystem · SocialChat）")]
    [TextArea(3, 8)]
    [SerializeField]
    private string day2PhoneInviteContext =
        "玩家在教室没有找到你，你在图书馆查作品展资料。你用手机短信约玩家今天下午来图书馆找你。";

    [TextArea(2, 6)]
    [Tooltip("LLM 失败或读档回放缺失时的固定短信。")]
    [SerializeField]
    private string day2PhoneInviteFallback =
        "昨天《AI游戏入门》帮了大忙！我今天还要在图书馆查一些作品展的资料……如果你有空的话，下午来图书馆找我？";

    [Header("Day3 · 开场短信（固定句；Demo 快通不调 SendSystem）")]
    [TextArea(2, 6)]
    [Tooltip("固定短信。")]
    [SerializeField]
    private string day3PhoneNudgeFallback =
        "今天下午我还在图书馆赶作品展……有点累。有事的话发消息就行。";

    [Header("Day3 · 偏离保底提案（SendSystem；不伪造玩家发言）")]
    [TextArea(3, 8)]
    [SerializeField]
    private string day3PhoneDeviationOfferContext =
        "玩家一直没提换地方。你仍在图书馆，效率很低、想歇一会。用短信口吻主动提出带玩家去你家看看。先表示展览还要整理，再邀请；用问句结尾等对方确认。";

    [TextArea(3, 8)]
    [SerializeField]
    private string day3FaceDeviationOfferContext =
        "玩家在图书馆当面一直没提换地方。你效率很低、想歇一会。当面主动提出带对方去你家看看。先表示展览还要整理，再邀请；用问句结尾等对方确认。";

    [TextArea(2, 6)]
    [Tooltip("LLM 失败时的固定提案（手机短信 / 当面 overlay 共用）。")]
    [FormerlySerializedAs("day3PhoneDeviationOfferMessage")]
    [SerializeField]
    private string day3PhoneDeviationOfferFallback =
        "今天学习效率好低啊，好想歇一会……对了，你还没去我家看过吧，要我带你参观一下吗？";

    [Header("Day3 · 手机破例答应（固定消息）")]
    [TextArea(2, 6)]
    [SerializeField]
    private string day3PhoneAcceptMessage =
        "好吧。那今天就破例一次。";

    [Header("面对面 · 闲置话题引导（SendSystem + 话题按钮）")]
    [Tooltip("按场景配置：提议兜底句、SendSystem 上下文、三个话题按钮。")]
    [SerializeField]
    private RemiFaceIdleTopicSet[] faceIdleTopicSets = new RemiFaceIdleTopicSet[]
    {
        new RemiFaceIdleTopicSet
        {
            scene = SceneTravelLocation.Classroom,
            proposeLine = "……对了，我正愁《AI游戏入门》找不到呢。",
            topic0 = "你在忙什么？",
            topic1 = "作品展准备得怎么样了？",
            topic2 = "是在找参考书吗？",
        },
        new RemiFaceIdleTopicSet
        {
            scene = SceneTravelLocation.Library,
            proposeLine = "……卡壳的话，不如就从这些里挑一件？",
            topic0 = "找书的事还有什么要叮嘱的吗？",
            topic1 = "今天在馆里查到什么了吗？",
            topic2 = "作品展资料还顺利吗？",
        },
        new RemiFaceIdleTopicSet
        {
            scene = SceneTravelLocation.Apartment,
            proposeLine = "……要不我们就从这些里挑一件慢慢说。",
            topic0 = "你之后有什么打算？",
            topic1 = "最近有什么想做的事吗？",
            topic2 = "今晚就随便聊聊也好。",
        },
    };

    [Header("终幕 · 无模板信时的收束兜底（{0}=共同经历段数）")]
    [TextArea(3, 8)]
    [SerializeField]
    private string endingClosingContextTemplate =
        "Demo 尾声面对面收束。Remi 刚在回忆画面里回顾完与玩家的 {0} 段共同经历；1～2 句总括这几次相处带来的变化；可留白；不要逐条复读标题、不要开新话题、不要新增事件。";

    [Header("扩展条目（新增事件写这里；id 与代码约定一致）")]
    [SerializeField]
    private Entry[] extraEntries = Array.Empty<Entry>();

    public static void EnsureExists()
    {
        if (Instance != null)
            return;

#if UNITY_2023_1_OR_NEWER
        RemiSendSystemContentManager found =
            FindFirstObjectByType<RemiSendSystemContentManager>(FindObjectsInactive.Include);
#else
        RemiSendSystemContentManager found = FindObjectOfType<RemiSendSystemContentManager>();
#endif
        if (found != null)
        {
            // Find 可能早于该组件 Awake：先挂上 Instance，避免投递读到 null。
            Instance = found;
            return;
        }

        var go = new GameObject(nameof(RemiSendSystemContentManager));
        go.AddComponent<RemiSendSystemContentManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 场景重载（如日切回教室）会再实例化 System 下的副本；静默丢掉即可。
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public Entry Day1BookRequest => Make(
        RemiSendSystemContentIds.Day1BookRequest,
        "Day1 托付找书",
        day1BookRequestContext);

    public Entry Day1BookRequestPlayerAsked => Make(
        RemiSendSystemContentIds.Day1BookRequestPlayerAsked,
        "Day1 玩家问起后托付",
        day1BookRequestPlayerAskedContext);

    public Entry Day1BookRequestGuarantee => Make(
        RemiSendSystemContentIds.Day1BookRequestGuarantee,
        "Day1 Remi 保底托付",
        day1BookRequestGuaranteeContext);

    public Entry Day1BookThanks => Make(
        RemiSendSystemContentIds.Day1BookThanks,
        "Day1 交书感谢",
        day1BookThanksContext);

    public Entry Day2PhoneInvite => Make(
        RemiSendSystemContentIds.Day2PhoneInvite,
        "Day2 手机邀请",
        day2PhoneInviteContext);

    public Entry Day3PhoneNudge => Make(
        RemiSendSystemContentIds.Day3PhoneNudge,
        "Day3 开场短信",
        day3PhoneNudgeFallback);

    public Entry Day3PhoneDeviationOffer => Make(
        RemiSendSystemContentIds.Day3PhoneDeviationOffer,
        "Day3 偏离保底提案 · 手机",
        day3PhoneDeviationOfferContext);

    public Entry Day3FaceDeviationOffer => Make(
        RemiSendSystemContentIds.Day3FaceDeviationOffer,
        "Day3 偏离保底提案 · 当面",
        day3FaceDeviationOfferContext);

    public Entry Day3PhoneAccept => Make(
        RemiSendSystemContentIds.Day3PhoneAccept,
        "Day3 手机破例答应",
        day3PhoneAcceptMessage);

    public Entry EndingClosingFallback => Make(
        RemiSendSystemContentIds.EndingClosingFallback,
        "终幕收束兜底",
        endingClosingContextTemplate);

    /// <summary>按场景解析闲置话题组（含提议兜底句与三个话题）。</summary>
    public RemiFaceIdleTopicSet GetFaceIdleTopicSet(SceneTravelLocation scene) =>
        RemiFaceIdleTopicCatalog.Resolve(faceIdleTopicSets, scene);

    /// <summary>当前活动场景的闲置话题组。</summary>
    public RemiFaceIdleTopicSet GetFaceIdleTopicSetForActiveScene() =>
        GetFaceIdleTopicSet(SceneTravelCatalog.ResolveFromActiveScene());

    /// <summary>闲置提议用 SendSystem Entry（仅 director_context）。</summary>
    public Entry GetFaceIdleProposeEntry(SceneTravelLocation scene)
    {
        RemiFaceIdleTopicSet set = GetFaceIdleTopicSet(scene);
        return Make(
            FaceIdleContentId(scene),
            FaceIdleLabel(scene),
            ResolveFaceIdleContext(set));
    }

    public bool TryGet(string id, out Entry entry)
    {
        entry = default;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        string key = id.Trim();
        if (TryGetNamed(key, out entry))
            return true;

        if (extraEntries == null)
            return false;

        for (int i = 0; i < extraEntries.Length; i++)
        {
            Entry e = extraEntries[i];
            if (string.IsNullOrWhiteSpace(e.id))
                continue;
            if (string.Equals(e.id.Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                entry = e;
                return true;
            }
        }

        return false;
    }

    public string GetInitiator(string id, string fallback = "")
    {
        if (TryGet(id, out Entry e) && !string.IsNullOrWhiteSpace(e.initiatorContext))
            return e.initiatorContext;
        return fallback ?? string.Empty;
    }

    /// <summary>手机短信正文：SendSystem 条目用 fallback 句；其余取 initiator。</summary>
    public string GetPhoneLine(string id, string fallback = "")
    {
        if (string.Equals(id, RemiSendSystemContentIds.Day2PhoneInvite, System.StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(day2PhoneInviteFallback)
                ? day2PhoneInviteFallback
                : (fallback ?? string.Empty);
        }

        if (string.Equals(id, RemiSendSystemContentIds.Day3PhoneNudge, System.StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(day3PhoneNudgeFallback)
                ? day3PhoneNudgeFallback
                : (fallback ?? string.Empty);
        }

        if (string.Equals(id, RemiSendSystemContentIds.Day3PhoneDeviationOffer, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(id, RemiSendSystemContentIds.Day3FaceDeviationOffer, System.StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(day3PhoneDeviationOfferFallback)
                ? day3PhoneDeviationOfferFallback
                : (fallback ?? string.Empty);
        }

        return GetInitiator(id, fallback);
    }

    /// <summary>解析收束兜底上下文；<paramref name="memoryCountOrLabel"/> 填入 {0}（可用数字或「几」）。</summary>
    public string FormatEndingClosingContext(object memoryCountOrLabel) =>
        string.Format(
            string.IsNullOrWhiteSpace(endingClosingContextTemplate)
                ? "Demo 尾声面对面收束。不要逐条复读标题。"
                : endingClosingContextTemplate,
            memoryCountOrLabel ?? "几");

    private bool TryGetNamed(string key, out Entry entry)
    {
        if (string.Equals(key, RemiSendSystemContentIds.Day1BookRequest, StringComparison.OrdinalIgnoreCase))
        {
            entry = Day1BookRequest;
            return true;
        }

        if (string.Equals(key, RemiSendSystemContentIds.Day1BookRequestPlayerAsked, StringComparison.OrdinalIgnoreCase))
        {
            entry = Day1BookRequestPlayerAsked;
            return true;
        }

        if (string.Equals(key, RemiSendSystemContentIds.Day1BookRequestGuarantee, StringComparison.OrdinalIgnoreCase))
        {
            entry = Day1BookRequestGuarantee;
            return true;
        }

        if (string.Equals(key, RemiSendSystemContentIds.Day1BookThanks, StringComparison.OrdinalIgnoreCase))
        {
            entry = Day1BookThanks;
            return true;
        }

        if (string.Equals(key, RemiSendSystemContentIds.Day2PhoneInvite, StringComparison.OrdinalIgnoreCase))
        {
            entry = Day2PhoneInvite;
            return true;
        }

        if (string.Equals(key, RemiSendSystemContentIds.Day3PhoneNudge, StringComparison.OrdinalIgnoreCase))
        {
            entry = Day3PhoneNudge;
            return true;
        }

        if (string.Equals(key, RemiSendSystemContentIds.Day3PhoneDeviationOffer, StringComparison.OrdinalIgnoreCase))
        {
            entry = Day3PhoneDeviationOffer;
            return true;
        }

        if (string.Equals(key, RemiSendSystemContentIds.Day3FaceDeviationOffer, StringComparison.OrdinalIgnoreCase))
        {
            entry = Day3FaceDeviationOffer;
            return true;
        }

        if (string.Equals(key, RemiSendSystemContentIds.Day3PhoneAccept, StringComparison.OrdinalIgnoreCase))
        {
            entry = Day3PhoneAccept;
            return true;
        }

        if (string.Equals(key, RemiSendSystemContentIds.EndingClosingFallback, StringComparison.OrdinalIgnoreCase))
        {
            entry = EndingClosingFallback;
            return true;
        }

        if (TryGetFaceIdleNamed(key, out entry))
            return true;

        entry = default;
        return false;
    }

    private bool TryGetFaceIdleNamed(string key, out Entry entry)
    {
        if (string.Equals(key, RemiSendSystemContentIds.FaceIdleProposeClassroom, StringComparison.OrdinalIgnoreCase))
        {
            entry = GetFaceIdleProposeEntry(SceneTravelLocation.Classroom);
            return true;
        }

        if (string.Equals(key, RemiSendSystemContentIds.FaceIdleProposeLibrary, StringComparison.OrdinalIgnoreCase))
        {
            entry = GetFaceIdleProposeEntry(SceneTravelLocation.Library);
            return true;
        }

        if (string.Equals(key, RemiSendSystemContentIds.FaceIdleProposeApartment, StringComparison.OrdinalIgnoreCase))
        {
            entry = GetFaceIdleProposeEntry(SceneTravelLocation.Apartment);
            return true;
        }

        entry = default;
        return false;
    }

    private string ResolveFaceIdleContext(RemiFaceIdleTopicSet set)
    {
        if (set == null)
            return RemiFaceIdleTopicSet.BuildDefaultSendSystemContext(null, null, null);
        return set.ResolveSendSystemContext();
    }

    private static string FaceIdleContentId(SceneTravelLocation scene) =>
        scene switch
        {
            SceneTravelLocation.Library => RemiSendSystemContentIds.FaceIdleProposeLibrary,
            SceneTravelLocation.Apartment => RemiSendSystemContentIds.FaceIdleProposeApartment,
            _ => RemiSendSystemContentIds.FaceIdleProposeClassroom,
        };

    private static string FaceIdleLabel(SceneTravelLocation scene) =>
        scene switch
        {
            SceneTravelLocation.Library => "闲置提议 · 图书馆",
            SceneTravelLocation.Apartment => "闲置提议 · 公寓",
            _ => "闲置提议 · 教室",
        };

    private static Entry Make(string id, string label, string context) =>
        new Entry
        {
            id = id,
            label = label,
            initiatorContext = context ?? string.Empty,
        };
}

/// <summary>与 <see cref="RemiSendSystemContentManager"/> 命名槽 / Extra 条目对齐的稳定 id。</summary>
public static class RemiSendSystemContentIds
{
    public const string Day1BookRequest = "day1_book_request";
    public const string Day1BookRequestPlayerAsked = "day1_book_request_player_asked";
    public const string Day1BookRequestGuarantee = "day1_book_request_guarantee";
    public const string Day1BookThanks = "day1_book_thanks";
    public const string Day2PhoneInvite = "day2_phone_invite";
    public const string Day3PhoneNudge = "day3_phone_nudge";
    public const string Day3PhoneDeviationOffer = "day3_phone_deviation_offer";
    public const string Day3FaceDeviationOffer = "day3_face_deviation_offer";
    public const string Day3PhoneAccept = "day3_phone_accept";
    public const string FaceIdleProposeClassroom = "face_idle_propose_classroom";
    public const string FaceIdleProposeLibrary = "face_idle_propose_library";
    public const string FaceIdleProposeApartment = "face_idle_propose_apartment";
    public const string EndingClosingFallback = "ending_closing_fallback";
}
