using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PG1 三日 Demo 故事脊柱：固定 beat 串联 Commission → CoPresence → Deviation。
/// 要求：关闭 AI 时固定演出仍可完成三次认知转变。
/// </summary>
[DisallowMultipleComponent]
public class RemiDemoSpineDirector : MonoBehaviour
{
    public static RemiDemoSpineDirector Instance { get; private set; }

    private const string PrefsBeatKey = "RemiDemoSpine_Beat";
    private const string SpeakerRemi = "Remi";
    private const string SpeakerPlayer = "你";
    private const string SpeakerNarrator = "旁白";

    [Header("依赖")]
    [SerializeField] private StoryDirector overlayStoryDirector;
    [SerializeField] private RemiDemoMemoryRecapEndingFlow memoryRecapEndingFlow;
    [SerializeField] private bool persistBeat = true;

    [Header("作品展背景（写入 Prompt）")]
    [TextArea(1, 3)]
    [SerializeField] private string exhibitionBackgroundFact =
        "约两周后举办学生作品展；Remi 是筹备组成员之一，最近常查资料、改方案。";

    [Header("Day1 结束 · 图书馆一瞥")]
    [Tooltip("黑屏后展示 Remi 在馆内的时长（秒）。")]
    [SerializeField] private float day1LibraryGlimpseHoldSeconds = 3.5f;
    [Tooltip("优先启用该物体（或其子物体）上的 Camera 作为过场机位；找不到再回退为把玩家放到同名点。")]
    [SerializeField] private string day1LibraryGlimpseCamMarker =
        SceneTravelCatalog.LibraryDay1GlimpseCamMarkerName;
    [SerializeField] private string day1LibraryGlimpsePlayerFallbackMarker =
        SceneTravelCatalog.LibraryPlayerFreeDialogueMarkerName;
    [Tooltip("过场期间 Remi 的世界 Y 轴朝向；结束后归位。")]
    [SerializeField] private float day1LibraryGlimpseRemiYawDegrees =
        RemiWorldPlacement.Day1LibraryGlimpseRemiYawDegrees;
    [SerializeField] private float day1LibraryGlimpseRemiIdleYawDegrees =
        RemiWorldPlacement.RemiDefaultIdleYawDegrees;

    [Header("Day2 结束 · 公寓一瞥")]
    [Tooltip("黑屏后展示 Remi 回公寓的时长（秒）。")]
    [SerializeField] private float day2ApartmentGlimpseHoldSeconds = 3.5f;
    [Tooltip("Apartment 场景内机位根（可含子 Camera）；默认 Day2LibraryGlimpseCam。")]
    [SerializeField] private string day2ApartmentGlimpseCamMarker =
        SceneTravelCatalog.ApartmentDay2GlimpseCamMarkerName;
    [SerializeField] private string day2ApartmentGlimpsePlayerFallbackMarker =
        SceneTravelCatalog.ApartmentDefaultRemiMarkerName;
    [SerializeField] private float day2ApartmentGlimpseRemiYawDegrees =
        RemiWorldPlacement.RemiDefaultIdleYawDegrees;
    [SerializeField] private float day2ApartmentGlimpseRemiIdleYawDegrees =
        RemiWorldPlacement.RemiDefaultIdleYawDegrees;

    [Header("Day3 偏离窗口")]
    [Tooltip("邀约送达后开启偏离窗口；满该秒数（unscaled）且未采纳时，由 Remi 主动提出偏离（不伪造玩家发言）。")]
    [SerializeField] private float day3InviteGuaranteeSeconds = 75f;

    private RemiDemoSpineBeat _beat;
    private bool _pendingDay2Invite;
    private bool _pendingDay3Nudge;
    private bool _pendingDay3ApartmentTravelOnPhoneClose;
    private Action _storyCompleteCallback;
    private bool _sequenceRunning;
    private Coroutine _day3InviteWatchRoutine;
    private bool _day3InviteWindowActive;
    private Coroutine _day2InviteFlushRoutine;
    private Coroutine _day3NudgeFlushRoutine;
    private Coroutine _day3OfferFlushRoutine;

    private Camera _glimpseOverrideCam;
    private bool _glimpseOverrideCamWasInactive;
    private GameObject _glimpseOverrideRoot;
    private AudioListener _glimpseOverrideListener;
    private bool _glimpseOverrideListenerAdded;
    private bool _glimpseOverrideListenerWasEnabled;
    private Camera _glimpseCachedMainCam;
    private bool _glimpseCachedMainCamEnabled;
    private AudioListener _glimpseCachedMainListener;
    private bool _glimpseCachedMainListenerEnabled;

    public RemiDemoSpineBeat CurrentBeat => _beat;
    public bool IsSpineSequenceRunning => _sequenceRunning;
    public string ExhibitionBackgroundFact => exhibitionBackgroundFact;

    /// <summary>Day2 邀请已送达，玩家尚未完成图书馆共现 intro。</summary>
    public bool IsAwaitingDay2LibraryVisit() =>
        _beat >= RemiDemoSpineBeat.Day2InviteDelivered &&
        _beat < RemiDemoSpineBeat.Day2LibraryIntroDone;

    /// <summary>Day2 仍在教室内（含等手机邀请、等出发去图书馆）。</summary>
    public bool IsDay2ClassroomPhase() =>
        _beat >= RemiDemoSpineBeat.Day1Complete &&
        _beat < RemiDemoSpineBeat.Day2LibraryIntroDone;

    /// <summary>Day3 偏离已接受，玩家尚未完成公寓共现 intro。</summary>
    public bool IsAwaitingDay3ApartmentVisit() =>
        _beat >= RemiDemoSpineBeat.Day3DeviationAccepted &&
        _beat < RemiDemoSpineBeat.Day3ApartmentIntroDone;

    /// <summary>
    /// Day3 偏离窗口 Open：邀约已送达、尚未采纳。
    /// 一次打开持续到前往公寓前（含教室手机 / 图书馆当面）。
    /// </summary>
    public bool IsDay3DeviationWindowOpen =>
        _beat >= RemiDemoSpineBeat.Day3InviteReady &&
        _beat < RemiDemoSpineBeat.Day3DeviationAccepted &&
        !RemiDemoSpineStoryChips.IsDay3ChipUsed;

    /// <summary>兼容旧名。</summary>
    public bool IsDay3InviteWindowOpen => IsDay3DeviationWindowOpen;

    /// <summary>Remi 已主动提案，等玩家确认「那走吧」；此拍关闭自由输入。</summary>
    public bool IsDay3DeviationPendingConfirm =>
        IsDay3DeviationWindowOpen && RemiDemoSpineStoryChips.IsDay3DeviationPendingConfirm;

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiDemoSpineDirector));
        go.AddComponent<RemiDemoSpineDirector>();
        //if (GetComponent<RemiDemoDay2ClassroomGuide>() == null)
        //    go.AddComponent<RemiDemoDay2ClassroomGuide>();
        if (go.GetComponent<RemiDemoMemoryRecapEndingFlow>() == null)
            go.AddComponent<RemiDemoMemoryRecapEndingFlow>();
        RemiDemoRunTelemetry.EnsureExists();
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

        if (GetComponent<RemiDemoDay2ClassroomGuide>() == null)
            gameObject.AddComponent<RemiDemoDay2ClassroomGuide>();
        if (memoryRecapEndingFlow == null)
            memoryRecapEndingFlow = GetComponent<RemiDemoMemoryRecapEndingFlow>();
        if (memoryRecapEndingFlow == null)
            memoryRecapEndingFlow = gameObject.AddComponent<RemiDemoMemoryRecapEndingFlow>();

        if (overlayStoryDirector == null)
            overlayStoryDirector = GetComponent<StoryDirector>();
        if (overlayStoryDirector == null)
            overlayStoryDirector = gameObject.AddComponent<StoryDirector>();

        overlayStoryDirector.ConfigureAsOverlayBeatDirector();
        LoadBeat();
        EnsureDay3InviteWindowWatch();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (overlayStoryDirector != null)
            overlayStoryDirector.StoryFinished += OnOverlayStoryFinished;
        EnsureDay3InviteWindowWatch();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (overlayStoryDirector != null)
            overlayStoryDirector.StoryFinished -= OnOverlayStoryFinished;
        StopDay3InviteWindowWatch();
    }

    private void OnDestroy()
    {
        StopDay3InviteWindowWatch();
        if (Instance == this)
            Instance = null;
    }

    private void ConfigureOverlayDirector()
    {
        // StoryDirector 序列化字段在运行时 AddComponent 后需代码兜底（Inspector 实例可覆盖）
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RemiWorldPlacement.OnSceneLoaded(scene);
        SyncBeatWithWorld();
        TryFlushPendingDay2Invite();
        TryFlushPendingDay3Nudge();
        EnsureDay3InviteWindowWatch();
    }

    /// <summary>交书致谢完成后（Day1 委托结束）。</summary>
    public void NotifyDay1BookSubmitted()
    {
        if (_beat >= RemiDemoSpineBeat.Day1BookSubmitted || _sequenceRunning)
            return;

        SetBeat(RemiDemoSpineBeat.Day1BookSubmitted);
        StartCoroutine(CoDay1EndingAndAdvance());
    }

    public void NotifyDay2LibraryIntroFinished()
    {
        if (_beat < RemiDemoSpineBeat.Day2InviteDelivered)
            return;
        if (_beat >= RemiDemoSpineBeat.Day2LibraryIntroDone)
            return;
        SetBeat(RemiDemoSpineBeat.Day2LibraryIntroDone);
        RemiPresenceService.Instance?.RecordSharedExperience(RemiSharedExperienceId.Day2LibraryCoPresence);
        RemiPresenceService.Instance?.EnterDayBlockAnchor();
        RemiPresenceService.Instance?.OnAnchorCommitted(RemiStoryAnchorId.Day2LibraryCoPresence);
    }

    /// <summary>Day3 公寓共现 intro 完成后，门口离开可触发终幕。</summary>
    public void NotifyDay3ApartmentIntroFinished()
    {
        if (_beat < RemiDemoSpineBeat.Day3DeviationAccepted)
            return;
        if (_beat >= RemiDemoSpineBeat.Day3ApartmentIntroDone)
            return;
        SetBeat(RemiDemoSpineBeat.Day3ApartmentIntroDone);
        RemiPresenceService.Instance?.OnAnchorCommitted(RemiStoryAnchorId.Day3ApartmentCoPresence);
        // Return waits until apartment leave / Day3Complete.
    }

    /// <summary>Day2 图书馆共现 intro 完成后，离开图书馆（切场景）可触发 Day2 收束。</summary>
    public bool CanPlayDay2Ending() =>
        _beat >= RemiDemoSpineBeat.Day2LibraryIntroDone &&
        _beat < RemiDemoSpineBeat.Day2Complete;

    /// <summary>由 <see cref="SceneTravelService"/> 在离开图书馆时调用；过场结束后玩家在教室。</summary>
    public void TryPlayDay2Ending()
    {
        if (!CanPlayDay2Ending() || _sequenceRunning)
            return;
        StartCoroutine(CoDay2EndingAndAdvance());
    }

    public bool HasPendingStoryReply() => GetPendingStoryChips().Count > 0;

    public IReadOnlyList<RemiSpineStoryChipOption> GetPendingStoryChips()
    {
        var chips = new List<RemiSpineStoryChipOption>(2);
        if (CanOfferDay3InviteToDorm())
            chips.Add(RemiDemoSpineStoryChips.GetChipOption(RemiSpineStoryChipId.Day3InviteToDorm));
        return chips;
    }

    public void OnStoryChipSelected(RemiSpineStoryChipId chipId)
    {
        if (_sequenceRunning)
            return;

        switch (chipId)
        {
            case RemiSpineStoryChipId.Day3InviteToDorm:
                TryAcceptDay3DormInviteFromChip();
                break;
        }
    }

    /// <summary>Day3 Demo 最短路径：固定 Chip 邀宿舍（不依赖 Detect / 保底提案）。</summary>
    public bool CanOfferDay3InviteToDorm()
    {
        if (!IsDay3DeviationWindowOpen || RemiDemoSpineStoryChips.IsDay3ChipUsed || _sequenceRunning)
            return false;
        EnsureStoryDayAtLeast(3, RemiDayPhase.Afternoon);
        return true;
    }

    /// <summary>玩家点 Chip「今晚方便来宿舍聊聊吗？」→ 固定答应 → 进公寓。</summary>
    private void TryAcceptDay3DormInviteFromChip()
    {
        if (!CanOfferDay3InviteToDorm())
            return;

        StopDay3InviteWindowWatch();

        string line = RemiDemoSpineStoryChips.GetPlayerLine(RemiSpineStoryChipId.Day3InviteToDorm);
        bool fromPhone = IsPhoneChatOpen();
        if (fromPhone)
        {
            AppendPhoneMessage(
                "user",
                line,
                true,
                RemiDemoSpineStoryChips.GetPlayerLineDisplay(RemiSpineStoryChipId.Day3InviteToDorm));
        }

        RemiDemoSpineStoryChips.MarkDay3Used();
        RemiDemoSpineStoryChips.ClearDay3PendingConfirm();
        RefreshPhoneStoryChips();
        DialoguePanel.RefreshDay3FaceConfirmUx();
        StartCoroutine(CoDay3DormInviteFixed(fromPhone));
    }

    /// <summary>固定答应句；手机通道等关面板后再进公寓。</summary>
    private IEnumerator CoDay3DormInviteFixed(bool fromPhone)
    {
        _sequenceRunning = true;

        string remiLine = ResolveDay3AcceptFallback();
        string acceptDisplay = RemiDemoSpineStoryChips.FormatDay3RemiAcceptDisplay(remiLine);
        AppendPhoneMessage("Remi", remiLine, true, acceptDisplay);

        if (!fromPhone)
        {
            Remi remi = FindObjectOfType<Remi>();
            remi?.PlayExpression(RemiExpression.Neutral);
            var lines = new List<StoryDirector.StoryLine>();
            AddLine(lines, SpeakerRemi, acceptDisplay);
            bool overlayDone = false;
            PlayOverlayStory(lines, () => overlayDone = true);
            while (!overlayDone)
                yield return null;

            yield return CoCommitDay3DeviationAndEnterApartment();
            _sequenceRunning = false;
            yield break;
        }

        ApplyDay3DeviationAccepted();
        _pendingDay3ApartmentTravelOnPhoneClose = true;
        _sequenceRunning = false;
    }

    /// <summary>已答应 Day3 邀约，等手机关闭后再进公寓。</summary>
    public bool IsPendingDay3ApartmentTravel => _pendingDay3ApartmentTravelOnPhoneClose;

    /// <summary>手机关闭时：若 Day3 已答应，再播黑屏进公寓。</summary>
    public void NotifyPhoneClosed()
    {
        if (!_pendingDay3ApartmentTravelOnPhoneClose || _sequenceRunning)
            return;

        _pendingDay3ApartmentTravelOnPhoneClose = false;
        StartCoroutine(CoTravelToDay3ApartmentAfterAccept());
    }

    /// <summary>当面面板关闭时复用同一触发（与 <see cref="NotifyPhoneClosed"/> 相同）。</summary>
    public void NotifyPanelClosedForDay3ApartmentTravel() => NotifyPhoneClosed();

    private static string ResolveDay3AcceptFallback()
    {
        RemiSendSystemContentManager.EnsureExists();
        return RemiSendSystemContentManager.Instance != null
            ? RemiSendSystemContentManager.Instance.GetPhoneLine(
                RemiSendSystemContentIds.Day3PhoneAccept,
                "好吧。那今天就破例一次。")
            : "好吧。那今天就破例一次。";
    }

    private static RemiExpression ParseRemiExpression(string expression)
    {
        if (Enum.TryParse(expression, true, out RemiExpression parsed))
            return parsed;
        return RemiExpression.Neutral;
    }

    private static bool IsPhoneChatOpen()
    {
        PhoneAppPanel panel = UiManager.Instance != null
            ? UiManager.Instance.GetPanel<PhoneAppPanel>()
            : null;
        return panel != null && panel.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// GameSystem：偏离窗口 Open 时，DeviationDetect 通过且目标合法。
    /// 真正表演与落世界见 <see cref="CoPerformDay3DeviationAcceptWithVoice"/>。
    /// </summary>
    public bool CanAcceptDay3DeviationFromDetect(in RemiDeviationDetectIntent.Result detect)
    {
        if (!IsDay3DeviationWindowOpen || IsDay3DeviationPendingConfirm || _sequenceRunning)
            return false;
        if (!detect.ParseOk || !detect.ProposeDeviation)
            return false;
        if (!RemiDeviationDetectIntent.IsAllowedDay3Target(detect.Target))
        {
            Debug.LogWarning(
                $"[RemiDemoSpineDirector] 偏离目标不合法，忽略：{detect.Target}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Detect 已裁定「玩家在提偏离」且 GameSystem 允许后：Voice 表演接受（可带表情 Intent），再改日程。
    /// </summary>
    public IEnumerator CoPerformDay3DeviationAcceptWithVoice(
        string playerText,
        RemiDeviationDetectIntent.Result detect,
        PromptedDialogueAgent agent,
        System.Action<string, string> onRemiSpoken)
    {
        if (!CanAcceptDay3DeviationFromDetect(detect) || agent == null)
            yield break;

        StopDay3InviteWindowWatch();
        RemiDemoSpineStoryChips.MarkDay3Used();
        RefreshPhoneStoryChips();
        _sequenceRunning = true;

        PromptContextManager ctx = PromptContextManager.Instance;
        ctx?.SetTurnNarrativeIntent(BuildDay3DeviationAcceptVoiceIntent(detect));

        string remiLine = null;
        string expression = "Neutral";
        bool done = false;
        string error = null;

        yield return agent.SendPlayer(
            playerText,
            (text, expr) =>
            {
                remiLine = text;
                expression = string.IsNullOrWhiteSpace(expr) ? "Neutral" : expr;
                done = true;
            },
            err =>
            {
                error = err;
                done = true;
            },
            stageContext: RemiStageExpressionContext.SocialChat,
            preserveInitiatorContext: true,
            voiceOnly: false,
            recordUserInHistory: true);

        while (!done)
            yield return null;

        if (string.IsNullOrWhiteSpace(remiLine))
        {
            remiLine = ResolveDay3AcceptFallback();
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning($"[RemiDemoSpineDirector] 偏离接受 Voice 失败，改用固定句：{error}");
        }

        onRemiSpoken?.Invoke(remiLine.Trim(), expression);
        yield return CoCommitDay3DeviationAndEnterApartment();
        _sequenceRunning = false;
    }

    private static string BuildDay3DeviationAcceptVoiceIntent(RemiDeviationDetectIntent.Result detect)
    {
        string target = string.IsNullOrWhiteSpace(detect.Target)
            ? RemiDeviationDetectIntent.AllowedTargetDorm
            : detect.Target.Trim();
        return
            "本轮裁定：玩家正在请求你改变今天的安排（偏离图书馆轨道）。\n" +
            "GameSystem 已允许接受；目标：" + target + "（宿舍/公寓）。\n" +
            "stance: hesitant_acceptance\n" +
            "表演要求：用短信口吻答应；先露出一点轨道重量（原本还要整理展览/学习），再让步接受；1～3 句；不要否认已接受；不要提系统或 JSON。";
    }

    /// <summary>
    /// Demo 快通：偏离窗口不再跑保底计时 / PendingConfirm。
    /// </summary>
    private void EnsureDay3InviteWindowWatch()
    {
        StopDay3InviteWindowWatch();
    }

    private void StartDay3InviteWindowWatch()
    {
        StopDay3InviteWindowWatch();
        if (!IsDay3DeviationWindowOpen || IsDay3DeviationPendingConfirm)
            return;

        _day3InviteWindowActive = true;
        _day3InviteWatchRoutine = StartCoroutine(CoWatchDay3InviteWindow());
    }

    private void StopDay3InviteWindowWatch()
    {
        _day3InviteWindowActive = false;
        if (_day3InviteWatchRoutine == null)
            return;

        StopCoroutine(_day3InviteWatchRoutine);
        _day3InviteWatchRoutine = null;
    }

    private IEnumerator CoWatchDay3InviteWindow()
    {
        // 只累计窗口 Open 且未 PendingConfirm 的空闲时间；过场中暂停计时。
        float idleElapsed = 0f;
        while (_day3InviteWindowActive &&
               IsDay3DeviationWindowOpen &&
               !IsDay3DeviationPendingConfirm)
        {
            if (_sequenceRunning)
            {
                yield return null;
                continue;
            }

            idleElapsed += Time.unscaledDeltaTime;
            if (day3InviteGuaranteeSeconds > 0f && idleElapsed >= day3InviteGuaranteeSeconds)
            {
                TriggerDay3DeviationGuarantee();
                yield break;
            }

            yield return null;
        }

        _day3InviteWatchRoutine = null;
    }

    /// <summary>保底：不伪造玩家发言；图书馆当面固定对话，否则手机 Remi 主动提案。</summary>
    private void TriggerDay3DeviationGuarantee()
    {
        if (!IsDay3DeviationWindowOpen || IsDay3DeviationPendingConfirm || _sequenceRunning)
            return;

        StopDay3InviteWindowWatch();

        if (SceneTravelCatalog.ResolveFromActiveScene() == SceneTravelLocation.Library)
            StartCoroutine(CoDay3LibraryDeviationGuarantee());
        else
            StartCoroutine(CoDay3PhoneDeviationGuarantee());
    }

    /// <summary>手机保底：SendSystem 提案后进入 PendingConfirm（Chip「那走吧」+ 关自由输入）。</summary>
    private IEnumerator CoDay3PhoneDeviationGuarantee()
    {
        _sequenceRunning = true;

        const string FallbackOffer =
            "今天学习效率好低啊，好想歇一会……对了，你还没去我家看过吧，要我带你参观一下吗？";
        yield return RemiPhoneSendSystem.CoDeliverOrRestore(
            RemiSendSystemContentIds.Day3PhoneDeviationOffer,
            FallbackOffer,
            generateIfMissing: true);

        if (!RemiPhoneSendSystem.HasPersistedLine(RemiSendSystemContentIds.Day3PhoneDeviationOffer))
            RemiPhoneSendSystem.PersistDeliveredLine(
                RemiSendSystemContentIds.Day3PhoneDeviationOffer,
                ResolveDay3OfferFallback());

        EnterDay3PendingConfirm(fromPhone: true);
        _sequenceRunning = false;
    }

    /// <summary>图书馆保底：当面 SendSystem 提案，等玩家确认；不代玩家说「那走吧」。</summary>
    private IEnumerator CoDay3LibraryDeviationGuarantee()
    {
        _sequenceRunning = true;

        string fallback = ResolveDay3OfferFallback();
        string offer = RemiPhoneSendSystem.GetPersistedLine(RemiSendSystemContentIds.Day3PhoneDeviationOffer);
        if (string.IsNullOrWhiteSpace(offer))
        {
            string generated = null;
            yield return CoGenerateFaceDeviationOffer(line => generated = line);
            offer = !string.IsNullOrWhiteSpace(generated) ? generated.Trim() : fallback;
            RemiPhoneSendSystem.PersistDeliveredLine(
                RemiSendSystemContentIds.Day3PhoneDeviationOffer,
                offer);
        }

        var lines = new List<StoryDirector.StoryLine>();
        if (string.Equals(offer, fallback, StringComparison.Ordinal))
        {
            AddLine(lines, SpeakerRemi, "今天学习效率好低啊……好想歇一会。");
            AddLine(lines, SpeakerRemi,
                RemiDialogueEmphasis.Apply(
                    "可是我本来还准备把展览的东西再整理一会儿……",
                    RemiDialogueEmphasisSpec.WithAnchors("展览")));
            AddLine(lines, SpeakerRemi, "……算了。对了，你还没去我家看过吧，要我带你参观一下吗？");
        }
        else
        {
            AddLine(lines, SpeakerRemi, RemiDemoSpineStoryChips.FormatDay3OfferMessageDisplay(offer));
        }

        bool done = false;
        PlayOverlayStory(lines, () => done = true);
        while (!done)
            yield return null;

        EnterDay3PendingConfirm(fromPhone: false);
        _sequenceRunning = false;
    }

    private IEnumerator CoGenerateFaceDeviationOffer(Action<string> onLine)
    {
        PromptedDialogueAgent agent = PromptedDialogueAgent.Instance != null
            ? PromptedDialogueAgent.Instance
            : FindObjectOfType<PromptedDialogueAgent>();
        if (agent == null)
        {
            onLine?.Invoke(null);
            yield break;
        }

        RemiSendSystemContentManager.EnsureExists();
        string context = RemiSendSystemContentManager.Instance != null
            ? RemiSendSystemContentManager.Instance.GetInitiator(
                RemiSendSystemContentIds.Day3FaceDeviationOffer)
            : string.Empty;
        if (string.IsNullOrWhiteSpace(context))
        {
            onLine?.Invoke(null);
            yield break;
        }

        Remi remi = FindObjectOfType<Remi>();
        string remiLine = null;
        bool done = false;
        yield return agent.SendSystem(
            context,
            (text, expr) =>
            {
                remiLine = text;
                if (remi != null && !string.IsNullOrWhiteSpace(expr))
                    remi.PlayExpression(ParseRemiExpression(expr));
                done = true;
            },
            err =>
            {
                if (!string.IsNullOrWhiteSpace(err))
                    Debug.LogWarning($"[RemiDemoSpineDirector] 馆内保底提案 SendSystem 失败，改用 fallback：{err}");
                done = true;
            });

        while (!done)
            yield return null;

        onLine?.Invoke(remiLine);
    }

    private void EnterDay3PendingConfirm(bool fromPhone)
    {
        RemiDemoSpineStoryChips.MarkDay3PendingConfirm();
        RefreshPhoneStoryChips();
        DialoguePanel.NotifyDay3PendingConfirm();
        // 仅手机保底出叙事条；图书馆当面提案不再出 Hint。
        if (fromPhone)
            StoryNarrativeHintView.TryPlayDay3DeviationOfferPhone();
    }

    private static string ResolveDay3OfferFallback()
    {
        RemiSendSystemContentManager.EnsureExists();
        return RemiSendSystemContentManager.Instance != null
            ? RemiSendSystemContentManager.Instance.GetPhoneLine(
                RemiSendSystemContentIds.Day3PhoneDeviationOffer,
                "今天学习效率好低啊，好想歇一会……对了，你还没去我家看过吧，要我带你参观一下吗？")
            : "今天学习效率好低啊，好想歇一会……对了，你还没去我家看过吧，要我带你参观一下吗？";
    }

    public void TryPlayDay3ApartmentEnding()
    {
        if (!CanPlayDay3Ending() || _sequenceRunning)
            return;
        StartCoroutine(CoDay3EndingAndFinale());
    }

    public bool CanPlayDay3Ending() =>
        _beat >= RemiDemoSpineBeat.Day3ApartmentIntroDone &&
        _beat < RemiDemoSpineBeat.Day3Complete;

    /// <summary>Day2 邀约已送达、尚未进馆：玩家可一次性短信回复（不触发 Remi 回信）。</summary>
    public bool CanSendDay2InviteOneShotReply() =>
        IsAwaitingDay2LibraryVisit() && !RemiDemoSpineStoryChips.IsDay2ChipAcknowledged;

    /// <summary>Day2 邀约窗口内已发过一次回复：锁住手机输入，催去图书馆。</summary>
    public bool IsDay2InvitePhoneInputLocked() =>
        IsAwaitingDay2LibraryVisit() && RemiDemoSpineStoryChips.IsDay2ChipAcknowledged;

    public void MarkDay2InviteOneShotReplySent()
    {
        if (!IsAwaitingDay2LibraryVisit())
            return;
        RemiDemoSpineStoryChips.MarkDay2Acknowledged();
        RefreshPhoneStoryChips();
    }

    /// <summary>公寓门口只提供「离开」，并拦截传送以进入 Ending。</summary>
    public bool IsApartmentLeaveEndingReady() => CanPlayDay3Ending() && !_sequenceRunning;

    public void TryFlushPendingDay2Invite()
    {
        if (_day2InviteFlushRoutine != null)
            return;
        _day2InviteFlushRoutine = StartCoroutine(CoFlushPendingDay2InviteThenClear());
    }

    private IEnumerator CoFlushPendingDay2InviteThenClear()
    {
        yield return CoFlushPendingDay2Invite();
        _day2InviteFlushRoutine = null;
    }

    private IEnumerator CoFlushPendingDay2Invite()
    {
        const string FallbackInvite =
            "昨天《AI游戏入门》帮了大忙！我今天还要在图书馆查一些作品展的资料……如果你有空的话，下午来图书馆找我？";

        if (_beat >= RemiDemoSpineBeat.Day2InviteDelivered)
        {
            _pendingDay2Invite = false;
            yield return RemiPhoneSendSystem.CoDeliverOrRestore(
                RemiSendSystemContentIds.Day2PhoneInvite,
                FallbackInvite,
                generateIfMissing: false);
            yield break;
        }

        if (!_pendingDay2Invite || _beat < RemiDemoSpineBeat.Day1Complete)
            yield break;

        yield return RemiPhoneSendSystem.CoDeliverOrRestore(
            RemiSendSystemContentIds.Day2PhoneInvite,
            FallbackInvite,
            generateIfMissing: true);

        if (!RemiPhoneSendSystem.HasPersistedLine(RemiSendSystemContentIds.Day2PhoneInvite))
            yield break;

        _pendingDay2Invite = false;
        SetBeat(RemiDemoSpineBeat.Day2InviteDelivered);
        OnDay2InviteDelivered();
    }

    /// <summary>打开手机时补投 Day3 开场短信。仅首次 pending 才打 LLM；已落盘只回放。</summary>
    public void TryFlushPendingDay3Nudge()
    {
        if (_day3NudgeFlushRoutine != null)
            return;
        _day3NudgeFlushRoutine = StartCoroutine(CoFlushPendingDay3NudgeThenClear());
    }

    private IEnumerator CoFlushPendingDay3NudgeThenClear()
    {
        // Demo 快通：Day3 开场短信只用固定句，不调 SendSystem。
        yield return CoFlushPendingDay3Nudge(generateIfMissing: false);
        _day3NudgeFlushRoutine = null;
    }

    private IEnumerator CoFlushPendingDay3Nudge(bool generateIfMissing)
    {
        if (_beat < RemiDemoSpineBeat.Day3InviteReady ||
            _beat >= RemiDemoSpineBeat.Day3DeviationAccepted)
            yield break;

        EnsureStoryDayAtLeast(3, RemiDayPhase.Afternoon);
        const string FallbackNudge =
            "今天下午我还在图书馆赶作品展……有点累。有事的话发消息就行。";
        bool alreadyHad = RemiPhoneSendSystem.HasPersistedLine(RemiSendSystemContentIds.Day3PhoneNudge);
        yield return RemiPhoneSendSystem.CoDeliverOrRestore(
            RemiSendSystemContentIds.Day3PhoneNudge,
            FallbackNudge,
            generateIfMissing: false);

        if (!RemiPhoneSendSystem.HasPersistedLine(RemiSendSystemContentIds.Day3PhoneNudge))
            yield break;

        bool firstDelivery = _pendingDay3Nudge && !alreadyHad;
        _pendingDay3Nudge = false;
        if (firstDelivery)
            OnDay3NudgeDelivered();
    }

    private void OnDay3NudgeDelivered()
    {
        RefreshPhoneStoryChips();
        StoryNarrativeHintView.TryPlayDay3PhoneNudge(7f);
    }

    /// <summary>读档时 beat 停在 Day1Complete：恢复待发邀请标记。</summary>
    public void RestorePendingDay2InviteIfNeeded()
    {
        if (_beat == RemiDemoSpineBeat.Day1Complete)
            _pendingDay2Invite = true;
    }

    private IEnumerator CoDay1EndingAndAdvance()
    {
        _sequenceRunning = true;

        RemiInteraction openDialogue = FindObjectOfType<RemiInteraction>();
        if (openDialogue != null && openDialogue.IsInDialogue)
            openDialogue.EndDialogue();

        RemiPresenceService presence = RemiPresenceService.Instance;
        if (presence != null)
        {
            // 交书后可能已进 Evening；一瞥前先回到「第一天下午 · 图书馆」叙事
            if (presence.WorldTime.storyDay < 1)
                presence.AdvanceWorldTime(RemiTimeAdvanceReason.StoryDayBegan);
            presence.SetDayPhase(RemiDayPhase.Afternoon);
            presence.PinLocationSoft(RemiLocation.Library, RemiActivity.Free);
        }

        // —— 黑屏：放学路过图书馆 ——
        yield return StoryBlackScreenInterlude.Play(
            "放学后，你路过图书馆。",
            "玻璃窗里还亮着灯。",
            "出于好奇，你望向窗边——");

        // —— 短镜头：馆内 Remi ——
        GameObject glimpseCover = CreateTransientFullScreenBlackCover(sortingOrder: 560);
        yield return CoLoadSceneQuiet(SceneTravelLocation.Library, pendingSpawnName: null);
        RemiWorldPlacement.PlaceRemiForLibraryGlimpse(day1LibraryGlimpseRemiYawDegrees);
        if (!TryApplyGlimpseCamera(ResolveDay1LibraryGlimpseCamMarker()))
            PlacePlayerForDay1LibraryGlimpse();
        LockPlayerForCinematic(true);
        SetRemiInteractionEnabled(false);
        if (glimpseCover != null)
            Destroy(glimpseCover);

        float hold = Mathf.Max(1.2f, day1LibraryGlimpseHoldSeconds);
        yield return new WaitForSecondsRealtime(hold);

        RemiWorldPlacement.SetRemiWorldYaw(day1LibraryGlimpseRemiIdleYawDegrees);
        RestoreGlimpseCamera();

        // —— 黑屏收束 + 切到第二天早上教室 ——
        yield return StoryBlackScreenInterlude.Play(
            "你没有进去打扰。",
            "第二天……");

        GameObject day2Cover = CreateTransientFullScreenBlackCover(sortingOrder: 560);
        SetRemiInteractionEnabled(true);
        if (presence != null)
        {
            presence.AdvanceWorldTime(RemiTimeAdvanceReason.NextDay);
            presence.SetDayPhase(RemiDayPhase.Morning);
            presence.ResetDayBlockForStoryDay(2);
        }

        yield return CoReturnToClassroomQuietForDayTransition();
        TryPlacePlayerAtSpawn(SceneTravelCatalog.GetSpawnPointName(SceneTravelLocation.Classroom));
        SceneTravelService.SetPendingSpawnPointName(null);
        RemiWorldPlacement.PrepareRemiAbsentFromClassroomForDay2();
        LockPlayerForCinematic(false);
        if (day2Cover != null)
            Destroy(day2Cover);

        presence = RemiPresenceService.Instance;

        yield return StoryBlackScreenInterlude.Play(
            "第二天，你来到教室。",
            "你习惯性看向窗边——那个座位今天却是空的。",
            "Remi 并不在这里。",
            "还没等你坐下，手机忽然震了一下。");

        SetBeat(RemiDemoSpineBeat.Day1Complete);

        if (presence != null)
            presence.SetDayPhase(RemiDayPhase.Afternoon);

        _pendingDay2Invite = true;
        yield return CoFlushPendingDay2Invite();

        if (presence != null)
            presence.EnterDayBlock(RemiDayBlockSlot.B, syncPhaseHint: false, enterAnchor: false);

        StoryNarrativeHintView.TryPlayDay2RemiLibraryInvite(6f);

        yield return RemiMemoryDaySettlement.CoWaitUntilIdle();
        RemiDemoDaySaveService.SaveDayStart(2);

        _sequenceRunning = false;
    }

    private void OnDay2InviteDelivered()
    {
        RefreshPhoneStoryChips();
        RemiDemoDay2ClassroomGuide.NotifyDay2InviteDelivered();
    }

    private IEnumerator CoDay2EndingAndAdvance()
    {
        _sequenceRunning = true;

        // 中途离馆：与自习终点共用 FinalSpecial 告别机位（已播过则跳过）
        RemiLibraryDay2CoPresenceFlow day2Flow = RemiLibraryDay2CoPresenceFlow.Instance;
        if (day2Flow != null && !day2Flow.HasCompletedStudyFarewell)
        {
            yield return day2Flow.CoPlayFarewellWithCameraIfNeeded();
        }
        else if (day2Flow == null)
        {
            var lines = new List<StoryDirector.StoryLine>();
            AddLine(lines, SpeakerRemi, "今天谢谢你陪我待这么久。");
            AddLine(lines, SpeakerRemi, "我明天应该还会来这里。");
            AddLine(lines, SpeakerPlayer, "好，那我不打扰你整理了。");
            AddLine(lines, SpeakerRemi, "嗯。路上小心。");

            bool done = false;
            PlayOverlayStory(lines, () => done = true);
            while (!done)
                yield return null;
        }

        RemiInteraction interaction = FindObjectOfType<RemiInteraction>();
        if (interaction != null && interaction.IsInDialogue)
            interaction.EndDialogue();

        RemiPresenceService presence = RemiPresenceService.Instance;
        if (presence != null)
        {
            // 一瞥前：叙事落到「第二天傍晚 · 公寓」
            if (presence.WorldTime.storyDay < 2)
                presence.AdvanceWorldTime(RemiTimeAdvanceReason.StoryDayBegan);
            presence.SetDayPhase(RemiDayPhase.Evening);
            presence.PinLocationSoft(RemiLocation.Dorm, RemiActivity.Free);
        }

        // —— 黑屏：Remi 从图书馆回家 ——
        yield return StoryBlackScreenInterlude.Play(
            "傍晚，图书馆渐渐安静下来。",
            "Remi 收拾好资料，从馆里走回公寓。",
            "夜里，窗边还亮着一盏灯——");

        // —— 短镜头：公寓内 Remi（Day2LibraryGlimpseCam）——
        GameObject glimpseCover = CreateTransientFullScreenBlackCover(sortingOrder: 560);
        yield return CoLoadSceneQuiet(SceneTravelLocation.Apartment, pendingSpawnName: null);
        RemiWorldPlacement.PlaceRemiForApartmentGlimpse(day2ApartmentGlimpseRemiYawDegrees);
        if (!TryApplyGlimpseCamera(ResolveDay2ApartmentGlimpseCamMarker()))
            PlacePlayerForDay2ApartmentGlimpse();
        LockPlayerForCinematic(true);
        SetRemiInteractionEnabled(false);
        if (glimpseCover != null)
            Destroy(glimpseCover);

        float hold = Mathf.Max(1.2f, day2ApartmentGlimpseHoldSeconds);
        yield return new WaitForSecondsRealtime(hold);

        RemiWorldPlacement.SetRemiWorldYaw(day2ApartmentGlimpseRemiIdleYawDegrees);
        RestoreGlimpseCamera();

        // —— 黑屏收束 + 切到第三天教室 ——
        yield return StoryBlackScreenInterlude.Play(
            "这一天就这样过去了。",
            "第三天……");

        GameObject day3Cover = CreateTransientFullScreenBlackCover(sortingOrder: 560);
        SetRemiInteractionEnabled(true);
        if (presence != null)
        {
            presence.EnterDayBlockClosing();
            presence.AdvanceWorldTime(RemiTimeAdvanceReason.NextDay);
            presence.SetDayPhase(RemiDayPhase.Morning);
        }

        yield return CoReturnToClassroomQuietForDayTransition();
        TryPlacePlayerAtSpawn(SceneTravelCatalog.GetSpawnPointName(SceneTravelLocation.Classroom));
        SceneTravelService.SetPendingSpawnPointName(null);
        RemiWorldPlacement.PrepareRemiAbsentFromClassroomForDay2();
        LockPlayerForCinematic(false);
        if (day3Cover != null)
            Destroy(day3Cover);

        // 场景重载后重新取 Presence，并强制日历到第 3 天下午（Chip / 短信时间戳依赖此）
        EnsureStoryDayAtLeast(3, RemiDayPhase.Afternoon);

        yield return StoryBlackScreenInterlude.Play(
            "第三天，你又来到教室。",
            "窗边的座位依旧空着——Remi 大概还在图书馆忙作品展。",
            "你想起她昨天说的话：明天应该还会去那里。",
            "这一次，或许该由你主动约她。");

        SetBeat(RemiDemoSpineBeat.Day2Complete);
        SetBeat(RemiDemoSpineBeat.Day3InviteReady);

        EnsureStoryDayAtLeast(3, RemiDayPhase.Afternoon);
        presence = RemiPresenceService.Instance;
        if (presence != null)
            presence.ResetDayBlockForStoryDay(3);

        _pendingDay3Nudge = true;
        yield return CoFlushPendingDay3Nudge(generateIfMissing: false);
        RefreshPhoneStoryChips();
        if (presence != null)
            presence.EnterDayBlock(RemiDayBlockSlot.B, syncPhaseHint: false, enterAnchor: false);

        yield return RemiMemoryDaySettlement.CoWaitUntilIdle();
        RemiDemoDaySaveService.SaveDayStart(3);

        _sequenceRunning = false;
        EnsureDay3InviteWindowWatch();
    }

    /// <summary>保证叙事日不少于 minDay，并切到指定时段（补日切丢失 / 场景重载不同步）。</summary>
    private static void EnsureStoryDayAtLeast(int minDay, RemiDayPhase phase)
    {
        RemiPresenceService presence = RemiPresenceService.Instance;
        if (presence == null)
            return;

        int guard = 0;
        while (presence.WorldTime.storyDay < minDay && guard++ < 8)
            presence.AdvanceWorldTime(RemiTimeAdvanceReason.NextDay);

        presence.SetDayPhase(phase);
    }

    /// <summary>日切用静默回教室，不用传送 REMI 进度条（随后由黑屏过场承接）。</summary>
    private IEnumerator CoReturnToClassroomQuietForDayTransition()
    {
        yield return CoLoadSceneQuiet(
            SceneTravelLocation.Classroom,
            SceneTravelCatalog.GetSpawnPointName(SceneTravelLocation.Classroom));
    }

    private IEnumerator CoLoadSceneQuiet(
        SceneTravelLocation location,
        string pendingSpawnName,
        bool unlockPlayerWhenDone = true)
    {
        string sceneName = SceneTravelCatalog.GetSceneName(location);
        if (string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.OrdinalIgnoreCase))
            yield break;

        SceneTravelService.EnsureExists();
        if (!string.IsNullOrWhiteSpace(pendingSpawnName))
            SceneTravelService.SetPendingSpawnPointName(pendingSpawnName);
        else
            SceneTravelService.SetPendingSpawnPointName(null);

        PlayerController player = SceneTravelService.GetPlayerController();
        player?.SetMoveLock(true);
        player?.SetLookLock(true);

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName);
        if (loadOp == null)
        {
            Debug.LogError($"[RemiDemoSpineDirector] 无法加载场景 {sceneName}。");
            if (unlockPlayerWhenDone)
            {
                player?.SetMoveLock(false);
                player?.SetLookLock(false);
            }
            yield break;
        }

        while (!loadOp.isDone)
            yield return null;

        player = SceneTravelService.GetPlayerController();
        if (unlockPlayerWhenDone)
        {
            player?.SetMoveLock(false);
            player?.SetLookLock(false);
        }
        else
        {
            player?.SetMoveLock(true);
            player?.SetLookLock(true);
        }
    }

    /// <summary>
    /// Ending 回顾：切到经历对应场景并启用 Recap_Day* 机位。
    /// </summary>
    public IEnumerator CoApplyEndingRecapGlimpse(SceneTravelCatalog.EndingRecapGlimpseSpec spec)
    {
        RestoreGlimpseCamera();

        GameObject cover = CreateTransientFullScreenBlackCover(sortingOrder: 560);
        yield return CoLoadSceneQuiet(spec.Location, pendingSpawnName: null, unlockPlayerWhenDone: false);
        LockPlayerForCinematic(true);
        SceneTravelService.SetPendingSpawnPointName(null);

        if (!TryApplyGlimpseCamera(spec.CamMarker))
        {
            Debug.LogWarning(
                $"[RemiDemoSpineDirector] 未找到回顾机位 {spec.CamMarker}（场景 {spec.Location}）。");
        }

        if (cover != null)
            Destroy(cover);

        // Day2 迁馆标记会在教室 sceneLoaded 时藏 Remi；回顾需要组件可见。
        RemiWorldPlacement.EnsureRemiActiveForEndingRecap(spec.Location);
    }

    /// <summary>回顾页结束后回到公寓，玩家/Remi 落 InStory，恢复玩家镜头。</summary>
    public IEnumerator CoReturnApartmentAfterEndingRecap()
    {
        RestoreGlimpseCamera();

        GameObject cover = CreateTransientFullScreenBlackCover(sortingOrder: 560);
        yield return CoLoadSceneQuiet(
            SceneTravelLocation.Apartment,
            pendingSpawnName: null,
            unlockPlayerWhenDone: false);
        LockPlayerForCinematic(true);

        // Ending 回顾经 Classroom→Library→Apartment 时 CoLoadSceneQuiet 不落点，
        // DDOL 玩家仍停在上一场景世界坐标；恢复 Main Camera 后就会“飞出场景”。
        RemiWorldPlacement.PlaceRemiForDay3Ending();
        if (!TryPlacePlayerAtInStoryOrFallback())
        {
            Debug.LogWarning(
                "[RemiDemoSpineDirector] Ending 回公寓未找到玩家站位 InStory(P)/DuringCon(P)/PlayerDefaultPos3。");
        }

        AimPlayerHorizontalAtRemi();
        // 切公寓会生成新 Remi：再次关掉交互并藏 Tip（Ending 锁也会持续）。
        SetRemiInteractionEnabled(false);
        HideRemiTipForEnding();
        if (cover != null)
            Destroy(cover);
    }

    private static void HideRemiTipForEnding()
    {
        Remi remi = UnityEngine.Object.FindObjectOfType<Remi>(true);
        if (remi == null)
            return;
        RemiRoleWorldUI roleUi = remi.GetComponentInChildren<RemiRoleWorldUI>(true);
        if (roleUi != null)
            roleUi.ApplyStoryPlaying(true);
    }

    /// <summary>优先 InStory(P)，其次闲聊/默认落点。</summary>
    private static bool TryPlacePlayerAtInStoryOrFallback()
    {
        string[] candidates =
        {
            SceneTravelCatalog.ApartmentInStorySpawnName,
            SceneTravelCatalog.ApartmentPlayerFreeDialogueMarkerName,
            SceneTravelCatalog.GetSpawnPointName(SceneTravelLocation.Apartment)
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            string name = candidates[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;
            if (GameObject.Find(name) == null)
                continue;
            TryPlacePlayerAtSpawn(name);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 启用场景内机位根上的 Camera 作为过场画面；
    /// 成功则关闭玩家 Main Camera。找不到专用相机返回 false。
    /// 机位根物体平时应为 inactive，仅在本过场激活。
    /// 连续切换机位时不闪回玩家镜头。
    /// </summary>
    public bool TryApplyGlimpseCamera(string camMarker)
    {
        if (string.IsNullOrWhiteSpace(camMarker))
            return false;

        GameObject root = FindSceneRootOrChildIncludingInactive(camMarker.Trim());
        if (root == null)
            return false;

        Camera cam = root.GetComponent<Camera>();
        if (cam == null)
            cam = root.GetComponentInChildren<Camera>(true);
        if (cam == null)
            return false;

        if (_glimpseOverrideRoot != null && _glimpseOverrideRoot != root)
        {
            ClearGlimpseOverrideListener();
            _glimpseOverrideRoot.SetActive(false);
            _glimpseOverrideCam = null;
            _glimpseOverrideRoot = null;
        }

        if (_glimpseCachedMainCam == null)
        {
            _glimpseCachedMainCam = Camera.main;
            if (_glimpseCachedMainCam != null && _glimpseCachedMainCam != cam)
            {
                _glimpseCachedMainCamEnabled = _glimpseCachedMainCam.enabled;
                _glimpseCachedMainCam.enabled = false;
                _glimpseCachedMainListener = _glimpseCachedMainCam.GetComponent<AudioListener>();
                if (_glimpseCachedMainListener != null)
                {
                    _glimpseCachedMainListenerEnabled = _glimpseCachedMainListener.enabled;
                    _glimpseCachedMainListener.enabled = false;
                }
            }
        }

        _glimpseOverrideCamWasInactive = !root.activeSelf;
        if (!root.activeSelf)
            root.SetActive(true);
        if (!cam.gameObject.activeSelf)
            cam.gameObject.SetActive(true);

        cam.enabled = true;
        EnsureGlimpseOverrideAudioListener(cam);
        _glimpseOverrideCam = cam;
        _glimpseOverrideRoot = root;
        return true;
    }

    private void EnsureGlimpseOverrideAudioListener(Camera cam)
    {
        if (cam == null)
            return;

        // 切到一瞥机位时主相机 Listener 已关；必须保证场景里仍有一个启用的 Listener
        if (_glimpseOverrideListener != null && _glimpseOverrideListener.gameObject != cam.gameObject)
            ClearGlimpseOverrideListener();

        AudioListener listener = cam.GetComponent<AudioListener>();
        bool added = false;
        if (listener == null)
        {
            listener = cam.gameObject.AddComponent<AudioListener>();
            added = true;
        }

        _glimpseOverrideListenerWasEnabled = listener.enabled;
        _glimpseOverrideListenerAdded = added;
        listener.enabled = true;
        _glimpseOverrideListener = listener;
    }

    private void ClearGlimpseOverrideListener()
    {
        if (_glimpseOverrideListener == null)
        {
            _glimpseOverrideListenerAdded = false;
            return;
        }

        if (_glimpseOverrideListenerAdded)
        {
            Destroy(_glimpseOverrideListener);
        }
        else
        {
            _glimpseOverrideListener.enabled = _glimpseOverrideListenerWasEnabled;
        }

        _glimpseOverrideListener = null;
        _glimpseOverrideListenerAdded = false;
    }

    public void RestoreGlimpseCamera()
    {
        ClearGlimpseOverrideListener();

        if (_glimpseOverrideCam != null)
        {
            _glimpseOverrideCam = null;
            _glimpseOverrideCamWasInactive = false;
        }

        if (_glimpseOverrideRoot != null)
        {
            _glimpseOverrideRoot.SetActive(false);
            _glimpseOverrideRoot = null;
        }

        if (_glimpseCachedMainListener != null)
        {
            _glimpseCachedMainListener.enabled = _glimpseCachedMainListenerEnabled;
            _glimpseCachedMainListener = null;
        }

        if (_glimpseCachedMainCam != null)
        {
            _glimpseCachedMainCam.enabled = _glimpseCachedMainCamEnabled;
            _glimpseCachedMainCam = null;
        }
    }

    private string ResolveDay1LibraryGlimpseCamMarker() =>
        string.IsNullOrWhiteSpace(day1LibraryGlimpseCamMarker)
            ? SceneTravelCatalog.LibraryDay1GlimpseCamMarkerName
            : day1LibraryGlimpseCamMarker.Trim();

    private string ResolveDay2ApartmentGlimpseCamMarker() =>
        string.IsNullOrWhiteSpace(day2ApartmentGlimpseCamMarker)
            ? SceneTravelCatalog.ApartmentDay2GlimpseCamMarkerName
            : day2ApartmentGlimpseCamMarker.Trim();

    /// <summary>含 inactive：GameObject.Find 找不到未激活根物体。</summary>
    private static GameObject FindSceneRootOrChildIncludingInactive(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindNamedRecursive(roots[i].transform, name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindNamedRecursive(Transform t, string name)
    {
        if (t == null)
            return null;
        if (t.name == name)
            return t.gameObject;
        for (int i = 0; i < t.childCount; i++)
        {
            GameObject found = FindNamedRecursive(t.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private void PlacePlayerForDay1LibraryGlimpse()
    {
        string camMarker = ResolveDay1LibraryGlimpseCamMarker();

        // 若机位根物体存在但无 Camera，仍可把玩家放到该点作为最后兜底观感
        if (GameObject.Find(camMarker) != null ||
            FindSceneRootOrChildIncludingInactive(camMarker) != null)
        {
            TryPlacePlayerAtSpawn(camMarker);
            return;
        }

        string fallback = string.IsNullOrWhiteSpace(day1LibraryGlimpsePlayerFallbackMarker)
            ? SceneTravelCatalog.LibraryPlayerFreeDialogueMarkerName
            : day1LibraryGlimpsePlayerFallbackMarker.Trim();
        if (GameObject.Find(fallback) != null)
        {
            TryPlacePlayerAtSpawn(fallback);
            AimPlayerHorizontalAtRemi();
            return;
        }

        Remi remiComp = FindObjectOfType<Remi>();
        Transform remi = remiComp != null ? remiComp.transform : null;
        Transform playerRoot = ResolvePlayerRoot();
        if (remi == null || playerRoot == null)
            return;

        Vector3 pos = remi.position + remi.right * 1.15f + remi.forward * -2.0f;
        pos.y = playerRoot.position.y;
        Vector3 flat = Flatten(remi.position - pos);
        if (flat.sqrMagnitude < 0.0001f)
            flat = remi.forward;
        Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);

        CharacterController cc = playerRoot.GetComponentInChildren<CharacterController>();
        if (cc != null)
            cc.enabled = false;
        playerRoot.SetPositionAndRotation(pos, look);
        if (cc != null)
            cc.enabled = true;
    }

    private void PlacePlayerForDay2ApartmentGlimpse()
    {
        string camMarker = ResolveDay2ApartmentGlimpseCamMarker();
        if (FindSceneRootOrChildIncludingInactive(camMarker) != null)
        {
            TryPlacePlayerAtSpawn(camMarker);
            return;
        }

        string fallback = string.IsNullOrWhiteSpace(day2ApartmentGlimpsePlayerFallbackMarker)
            ? SceneTravelCatalog.ApartmentDefaultRemiMarkerName
            : day2ApartmentGlimpsePlayerFallbackMarker.Trim();
        if (GameObject.Find(fallback) != null ||
            FindSceneRootOrChildIncludingInactive(fallback) != null)
        {
            TryPlacePlayerAtSpawn(fallback);
            AimPlayerHorizontalAtRemi();
            return;
        }

        if (FindSceneRootOrChildIncludingInactive("ApartmentDefaultPos(R)") != null)
        {
            TryPlacePlayerAtSpawn("ApartmentDefaultPos(R)");
            AimPlayerHorizontalAtRemi();
            return;
        }

        AimPlayerHorizontalAtRemi();
    }

    private static void AimPlayerHorizontalAtRemi()
    {
        Remi remiComp = FindObjectOfType<Remi>();
        Transform remi = remiComp != null ? remiComp.transform : null;
        Transform playerRoot = ResolvePlayerRoot();
        if (remi == null || playerRoot == null)
            return;

        Vector3 flat = Flatten(remi.position - playerRoot.position);
        if (flat.sqrMagnitude < 0.0001f)
            return;
        playerRoot.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
    }

    /// <summary>玩家水平转向 Remi（门边 Ending 开场第一句期间）。</summary>
    public IEnumerator CoTurnPlayerTowardRemi(float durationSeconds)
    {
        Remi remiComp = FindObjectOfType<Remi>();
        Transform remi = remiComp != null ? remiComp.transform : null;
        Transform playerRoot = ResolvePlayerRoot();
        if (remi == null || playerRoot == null)
            yield break;

        Vector3 flat = Flatten(remi.position - playerRoot.position);
        if (flat.sqrMagnitude < 0.0001f)
            yield break;

        Quaternion from = playerRoot.rotation;
        Quaternion to = Quaternion.LookRotation(flat.normalized, Vector3.up);
        float duration = Mathf.Max(0.15f, durationSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            playerRoot.rotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }

        playerRoot.rotation = to;
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static Transform ResolvePlayerRoot()
    {
        PlayerController controller = FindObjectOfType<PlayerController>();
        if (controller != null)
            return controller.transform;
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged != null ? tagged.transform : null;
    }

    private static readonly List<Behaviour> _cinematicDisabledLooks = new List<Behaviour>();

    private static void LockPlayerForCinematic(bool locked)
    {
        PlayerController player = SceneTravelService.GetPlayerController();
        if (player == null)
            player = FindObjectOfType<PlayerController>();
        player?.SetMoveLock(locked);
        player?.SetLookLock(locked);
        SetCinematicLookScriptsEnabled(!locked);
    }

    private static void SetCinematicLookScriptsEnabled(bool enabled)
    {
        if (!enabled)
        {
            _cinematicDisabledLooks.Clear();
            CollectCinematicLookScripts(Camera.main);
            PlayerController player = SceneTravelService.GetPlayerController();
            if (player != null)
                CollectCinematicLookScripts(player);
            for (int i = 0; i < _cinematicDisabledLooks.Count; i++)
            {
                if (_cinematicDisabledLooks[i] != null)
                    _cinematicDisabledLooks[i].enabled = false;
            }

            return;
        }

        for (int i = 0; i < _cinematicDisabledLooks.Count; i++)
        {
            if (_cinematicDisabledLooks[i] != null)
                _cinematicDisabledLooks[i].enabled = true;
        }

        _cinematicDisabledLooks.Clear();
    }

    private static void CollectCinematicLookScripts(Component root)
    {
        if (root == null)
            return;
        Behaviour[] behaviours = root.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour b = behaviours[i];
            if (b == null || b is PlayerController || !b.enabled)
                continue;
            string typeName = b.GetType().Name;
            if (typeName == "FirstPersonCameraLook" || typeName == "SimpleCameraController")
                _cinematicDisabledLooks.Add(b);
        }
    }

    private static void SetRemiInteractionEnabled(bool enabled)
    {
        RemiInteraction interaction = FindObjectOfType<RemiInteraction>();
        if (interaction != null)
            interaction.enabled = enabled;

        // enabled=false 时 Update 不再跑，须立刻藏 Tip，否则会停在「按 F」状态。
        if (!enabled)
            HideRemiTipForEnding();
    }

    private static GameObject CreateTransientFullScreenBlackCover(int sortingOrder)
    {
        var root = new GameObject("DayTransitionBlackCover");
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var bgGo = new GameObject("Background", typeof(RectTransform));
        bgGo.transform.SetParent(root.transform, false);
        var rt = bgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = bgGo.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        img.raycastTarget = true;
        return root;
    }

    private void ApplyDay3DeviationAccepted()
    {
        RemiPresenceService presence = RemiPresenceService.Instance;
        presence?.ApplyScheduleOverride(RemiLocation.Dorm, RemiActivity.AtDorm);
        RemiWorldPlacement.PrepareRemiAtApartmentForDay3();

        SetBeat(RemiDemoSpineBeat.Day3DeviationAccepted);
        RefreshPhoneStoryChips();
        DialoguePanel.RefreshDay3FaceConfirmUx();
        StopDay3InviteWindowWatch();
        RemiDemoSpineStoryChips.ClearDay3PendingConfirm();
    }

    private IEnumerator CoCommitDay3DeviationAndEnterApartment()
    {
        ApplyDay3DeviationAccepted();
        yield return CoTravelToDay3ApartmentAfterAccept();
    }

    /// <summary>答应后黑屏过场，直接传入公寓并接上固定开场。</summary>
    private IEnumerator CoTravelToDay3ApartmentAfterAccept()
    {
        _sequenceRunning = true;
        PhoneAppPanel.ClosePanel();
        RemiInteraction interaction = FindObjectOfType<RemiInteraction>();
        if (interaction != null && interaction.IsInDialogue)
            interaction.EndDialogue();

        LockPlayerForCinematic(true);

        yield return StoryBlackScreenInterlude.Play(
            "你收到了 Remi 给你发的公寓地址，你似乎还是第一次来到这里……");

        GameObject cover = CreateTransientFullScreenBlackCover(sortingOrder: 560);
        yield return CoLoadSceneQuiet(
            SceneTravelLocation.Apartment,
            SceneTravelCatalog.ApartmentInStorySpawnName);
        LockPlayerForCinematic(true);
        TryPlacePlayerAtSpawn(SceneTravelCatalog.ApartmentInStorySpawnName);
        SceneTravelService.SetPendingSpawnPointName(null);
        yield return null;

        RemiApartmentDay3CoPresenceStory intro = FindObjectOfType<RemiApartmentDay3CoPresenceStory>();
        bool started = intro != null && intro.TryBeginNow();
        if (!started)
            Debug.LogWarning("[RemiDemoSpineDirector] 未能自动开始公寓开场，玩家可走进触发区。");

        if (cover != null)
            Destroy(cover);

        // 开场已开始则由 StoryDirector 持有移动锁，直到剧情 Finish；否则解除过场锁
        if (!started)
            LockPlayerForCinematic(false);
        _sequenceRunning = false;
    }

    private IEnumerator CoDay3EndingAndFinale()
    {
        _sequenceRunning = true;
        try
        {
            RemiInteraction openDialogue = FindObjectOfType<RemiInteraction>();
            if (openDialogue != null && openDialogue.IsInDialogue)
                openDialogue.EndDialogue();

            LockPlayerForCinematic(true);
            SetRemiInteractionEnabled(false);
            RemiWorldPlacement.PlaceRemiForDay3Ending();

            if (memoryRecapEndingFlow == null)
                memoryRecapEndingFlow = RemiDemoMemoryRecapEndingFlow.Instance != null
                    ? RemiDemoMemoryRecapEndingFlow.Instance
                    : GetComponent<RemiDemoMemoryRecapEndingFlow>();

            if (memoryRecapEndingFlow != null)
                yield return memoryRecapEndingFlow.CoPlayMemoryRecapEnding();
            else
                Debug.LogWarning("[RemiDemoSpineDirector] 未找到 RemiDemoMemoryRecapEndingFlow，跳过记忆回顾终幕。");

            RestoreGlimpseCamera();
            SetRemiInteractionEnabled(true);

            RemiPresenceService.Instance?.EnterDayBlockClosing(); // Day3 Return after deviation session ends
            SetBeat(RemiDemoSpineBeat.Day3Complete);

            RemiInteraction interaction = FindObjectOfType<RemiInteraction>();
            if (interaction != null && interaction.IsInDialogue)
                interaction.EndDialogue();

            yield return StoryBlackScreenInterlude.Play(
                "你离开宿舍。",
                "Remi 回到自己的房间，打开电脑，继续作品展的准备工作。",
                "作品展还有 11 天。");

            SetBeat(RemiDemoSpineBeat.DemoFinale);
            RemiDemoRunTelemetry.EnsureExists();
            RemiDemoRunTelemetry.Instance?.FinalizeAndSave();
            StoryNarrativeHintView.TryPlayCustomHint("PG1 Demo 故事线结束。感谢体验。", 8f);
        }
        finally
        {
            RestoreGlimpseCamera();
            SetRemiInteractionEnabled(true);
            LockPlayerForCinematic(false);
            _sequenceRunning = false;
        }
    }

    private void PlayOverlayStory(List<StoryDirector.StoryLine> lines, Action onComplete)
    {
        if (overlayStoryDirector == null)
        {
            onComplete?.Invoke();
            return;
        }

        _storyCompleteCallback = onComplete;
        overlayStoryDirector.ResetStoryPlaybackState();
        overlayStoryDirector.SetLines(lines);
        overlayStoryDirector.BeginStory();
    }

    private void OnOverlayStoryFinished()
    {
        Action cb = _storyCompleteCallback;
        _storyCompleteCallback = null;
        cb?.Invoke();
    }

    private static void AppendPhoneMessage(string role, string content, bool save, string displayOverride = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        content = content.Trim();
        if (save)
        {
            bool isPlayer = string.Equals(role, "user", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "player", StringComparison.OrdinalIgnoreCase);
            if (isPlayer)
                PhoneAppPanel.TryPersistPlayerMessage(content);
            else
                PhoneAppPanel.TryPersistRemiMessage(content);
        }

        PhoneAppPanel panel = UiManager.Instance != null ? UiManager.Instance.GetPanel<PhoneAppPanel>() : null;
        if (panel == null)
            return;

        panel.EnsureInitialized();
        if (panel.gameObject.activeInHierarchy)
            panel.ReloadChatFromStorage();
    }

    private static void RefreshPhoneStoryChips()
    {
        PhoneAppPanel panel = UiManager.Instance != null ? UiManager.Instance.GetPanel<PhoneAppPanel>() : null;
        panel?.RefreshStoryChips();
    }

    private void SyncBeatWithWorld()
    {
        if (_beat >= RemiDemoSpineBeat.Day1Complete && _beat < RemiDemoSpineBeat.Day2InviteDelivered)
            TryFlushPendingDay2Invite();
    }

    private void SetBeat(RemiDemoSpineBeat beat)
    {
        if (beat <= _beat && beat != RemiDemoSpineBeat.NotStarted)
            return;
        _beat = beat;
        if (persistBeat)
            PlayerPrefs.SetInt(PrefsBeatKey, (int)_beat);
    }

    private void LoadBeat()
    {
        _beat = persistBeat
            ? (RemiDemoSpineBeat)Mathf.Max(0, PlayerPrefs.GetInt(PrefsBeatKey, 0))
            : RemiDemoSpineBeat.NotStarted;

        RestorePendingDay2InviteIfNeeded();
    }

    /// <summary>读档：允许 beat 回退到存档值（SetBeat 只前进）。</summary>
    public void ForceApplyBeatFromPrefs()
    {
        _sequenceRunning = false;
        _beat = persistBeat
            ? (RemiDemoSpineBeat)Mathf.Max(0, PlayerPrefs.GetInt(PrefsBeatKey, 0))
            : RemiDemoSpineBeat.NotStarted;
        RestorePendingDay2InviteIfNeeded();
        _pendingDay3Nudge = false;
        RemiWorldPlacement.EnsureDay2AbsentInClassroom();

        // Day2 邀请：读档只回放。Day3 nudge：日起点读档清旧句后重打 SendSystem。
        if (_beat >= RemiDemoSpineBeat.Day2InviteDelivered &&
            _beat < RemiDemoSpineBeat.Day2LibraryIntroDone)
        {
            if (_day2InviteFlushRoutine == null)
                _day2InviteFlushRoutine = StartCoroutine(CoRestoreDay2InviteAfterLoad());
        }
        if (_beat >= RemiDemoSpineBeat.Day3InviteReady &&
            _beat < RemiDemoSpineBeat.Day3DeviationAccepted)
        {
            if (!RemiPhoneSendSystem.HasPersistedLine(RemiSendSystemContentIds.Day3PhoneNudge))
                _pendingDay3Nudge = true;
            if (_day3NudgeFlushRoutine == null)
                _day3NudgeFlushRoutine = StartCoroutine(CoRestoreDay3NudgeAfterLoad());
        }
        RefreshPhoneStoryChips();
        EnsureDay3InviteWindowWatch();
    }

    private IEnumerator CoRestoreDay2InviteAfterLoad()
    {
        yield return CoFlushPendingDay2Invite();
        _day2InviteFlushRoutine = null;
    }

    private IEnumerator CoRestoreDay3NudgeAfterLoad()
    {
        yield return CoFlushPendingDay3Nudge(generateIfMissing: _pendingDay3Nudge);
        _day3NudgeFlushRoutine = null;
    }

    /// <summary>PendingConfirm 时打开手机：只回放已落盘的保底提案，不重打 LLM。</summary>
    public void TryFlushPendingDay3Offer()
    {
        if (!IsDay3DeviationPendingConfirm || _day3OfferFlushRoutine != null)
            return;
        _day3OfferFlushRoutine = StartCoroutine(CoFlushPendingDay3OfferThenClear());
    }

    private IEnumerator CoFlushPendingDay3OfferThenClear()
    {
        yield return RemiPhoneSendSystem.CoDeliverOrRestore(
            RemiSendSystemContentIds.Day3PhoneDeviationOffer,
            ResolveDay3OfferFallback(),
            generateIfMissing: false);
        _day3OfferFlushRoutine = null;
    }

    private static void AddLine(List<StoryDirector.StoryLine> lines, string speaker, string text)
    {
        lines.Add(new StoryDirector.StoryLine { speakerName = speaker, text = text });
    }

    private static void TryPlacePlayerAtSpawn(string spawnName)
    {
        if (string.IsNullOrWhiteSpace(spawnName))
            return;

        GameObject spawn = GameObject.Find(spawnName);
        if (spawn == null)
            return;

        Transform playerRoot = null;
        PlayerController controller = UnityEngine.Object.FindObjectOfType<PlayerController>();
        if (controller != null)
            playerRoot = controller.transform;
        else
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
                playerRoot = tagged.transform;
        }

        if (playerRoot == null)
            return;

        CharacterController cc = playerRoot.GetComponentInChildren<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        playerRoot.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);

        if (cc != null)
            cc.enabled = true;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Reset Spine Beat")]
    private void Editor_ResetBeat()
    {
        PlayerPrefs.DeleteKey(PrefsBeatKey);
        RemiDemoSpineStoryChips.ResetProgress();
        RemiPhoneSendSystem.ClearAll();
        _beat = RemiDemoSpineBeat.NotStarted;
        _pendingDay2Invite = false;
        _pendingDay3Nudge = false;
        _pendingDay3ApartmentTravelOnPhoneClose = false;
        _sequenceRunning = false;
    }
#endif
}
