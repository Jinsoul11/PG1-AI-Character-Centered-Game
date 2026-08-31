using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Day1 Touch 委托：教室开场后进入窗口期（<see cref="QuestState.WindowOpen"/>）。
/// 入口可为告别、玩家问起（别名命中后跳过 SendPlayer 直接派送）、或 Remi 保底主动开口（均经 <see cref="PromptedDialogueAgent.SendSystem"/>）。
/// 找书 / 交书 / 致谢推进日切流程不变。SendSystem 文案由 <see cref="RemiSendSystemContentManager"/> 管理。
/// 场景里挂一个实例即可；未挂则一切保持原样。
/// </summary>
[DisallowMultipleComponent]
public class RemiBookQuestFlow : MonoBehaviour
{
    public static RemiBookQuestFlow Instance { get; private set; }

    private const string PrefsKeyState = "RemiBookQuest_State";
    private const string PrefsKeyHasBook = "RemiBookQuest_HasBook";
    private const string PrefsKeyEntry = "RemiBookQuest_Entry";

    public enum QuestState
    {
        /// <summary>窗口期（PlayerPrefs 0；旧名 NeedGoodbyeOffer）。</summary>
        WindowOpen = 0,
        WaitingForBook = 1,
        Finished = 2,
    }

    public enum CommissionEntryKind
    {
        Goodbye = 0,
        PlayerAsked = 1,
        RemiGuarantee = 2,
    }

    [Header("总开关")]
    [SerializeField] private bool enableQuest = true;
    [Tooltip("为 true 时用 PlayerPrefs 记住进度。")]
    [SerializeField] private bool persistProgress = true;
    [Tooltip("等待交书期间是否禁止再次按 F 与 Remi 对话。")]
    [SerializeField] private bool blockRemiDialogueWhileWaitingForBook = true;

    [Header("依赖")]
    [SerializeField] private PromptedDialogueAgent promptedAgent;

    [Header("Day1 Touch 窗口")]
    [Tooltip("玩家尚未打开与 Remi 的面对面对话时：窗口空闲满该秒数（unscaled）触发 RemiGuarantee。一旦进过对话，本窗口不再用时间保底。")]
    [SerializeField] private float guaranteeSeconds = 75f;
    [Tooltip("玩家已进入面对面对话后：自由聊满该「小轮」数触发 RemiGuarantee（每发送一句玩家话 +1，不是 history 大窗）。")]
    [SerializeField] private int guaranteeMinFaceRounds = 3;
    [Tooltip("玩家文本命中任一别名则视为主动问起，走 PlayerAsked 入口。")]
    [SerializeField] private string[] playerAskAliases = new string[]
    {
        "展览", "参考书", "资料", "筹备",
        "忙什么", "在干什么", "找什么", "找书", 
        "AI游戏", "AI游戏入门", "拟人", 
    };

    [Header("UI 文案（与 EmaConPanel 对齐）")]
    [SerializeField] private string goodbyeButtonLabel = "回头见";
    [SerializeField] private string goodbyeConfirmButtonLabel = "确认";

    [Header("演出（可选）")]
    [SerializeField] private UnityEvent onAfterRequestSendSystemBeforeClose;
    [SerializeField] private UnityEvent onAfterThanksSendSystem;

    private QuestState _state = QuestState.WindowOpen;
    private bool _sequenceRunning;
    private bool _hasBookInInventory;
    private int _faceRoundsInWindow;
    /// <summary>本窗口是否已打开过面对面对话；为 true 后保底只看闲聊小轮，不再用 guaranteeSeconds。</summary>
    private bool _faceDialogueEnteredThisWindow;
    private Coroutine _windowWatchRoutine;
    private bool _windowTimingActive;
    private CommissionEntryKind _entryKind = CommissionEntryKind.Goodbye;

    public string GoodbyeButtonLabel => goodbyeButtonLabel;
    public string GoodbyeConfirmButtonLabel => goodbyeConfirmButtonLabel;
    public QuestState State => _state;
    public CommissionEntryKind EntryKind => _entryKind;

    private RemiInteraction _goodbyeInteraction;
    private bool _awaitingGoodbyeConfirm;
    private bool _advanceDayAfterConfirmClose;

    /// <summary>等交书阶段且尚未检视领取。</summary>
    public bool AwaitsBookPickup() => IsQuestFeatureEnabled && _state == QuestState.WaitingForBook && !_hasBookInInventory;

    /// <summary>等交书阶段且已在 CheckPanel 确认后拿到书。</summary>
    public bool HasBookForSubmission() => IsQuestFeatureEnabled && _state == QuestState.WaitingForBook && _hasBookInInventory;

    /// <summary>场景中书物体是否应隐藏（已领取或任务已结束）。</summary>
    public bool ShouldHideBookObjectInScene() =>
        IsQuestFeatureEnabled &&
        (_state == QuestState.Finished || (_state == QuestState.WaitingForBook && _hasBookInInventory));

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RemiBookQuestFlow] 场景中存在多个实例，保留先激活的。", this);
            enabled = false;
            return;
        }

        Instance = this;
        RemiSendSystemContentManager.EnsureExists();
        if (!enableQuest)
        {
            _state = QuestState.Finished;
            return;
        }

        if (persistProgress && PlayerPrefs.HasKey(PrefsKeyState))
            _state = (QuestState)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKeyState, 0), 0, 2);
        else
            _state = QuestState.WindowOpen;

        if (persistProgress && PlayerPrefs.HasKey(PrefsKeyEntry))
            _entryKind = (CommissionEntryKind)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKeyEntry, 0), 0, 2);

        if (_state != QuestState.WaitingForBook)
            _hasBookInInventory = false;
        else if (persistProgress && PlayerPrefs.GetInt(PrefsKeyHasBook, 0) != 0)
            _hasBookInInventory = true;

        if (promptedAgent == null)
            promptedAgent = FindObjectOfType<PromptedDialogueAgent>();
    }

    private void OnDestroy()
    {
        StopWindowWatch();
        if (Instance == this)
            Instance = null;
    }

    public bool IsQuestFeatureEnabled => enableQuest && enabled;

    /// <summary>告别/致谢 SendSystem 演出进行中（勿提前关对话面板）。</summary>
    public bool IsSequenceRunning => _sequenceRunning;

    /// <summary>托付台词已播完，等待玩家点「确认」关面板。</summary>
    public bool IsAwaitingGoodbyeConfirm => _awaitingGoodbyeConfirm;

    /// <summary>演出中或待确认期间，勿因走远等方式自动结束对话。</summary>
    public bool IsBlockingDialogueExit => _sequenceRunning || _awaitingGoodbyeConfirm;

    /// <summary>Day1 Touch 窗口期（尚未托付找书）。</summary>
    public bool IsDay1CommissionWindowOpen =>
        IsQuestFeatureEnabled && _state == QuestState.WindowOpen;

    /// <summary>对话面板关闭按钮是否应显示为「回头见」并走告别流程。</summary>
    public bool ShouldShowGoodbyeExitUx() => IsDay1CommissionWindowOpen;

    public bool CanPlayerOpenRemiDialogue() =>
        !IsQuestFeatureEnabled || !blockRemiDialogueWhileWaitingForBook || _state != QuestState.WaitingForBook;

    private void SetState(QuestState s)
    {
        _state = s;
        if (s != QuestState.WindowOpen)
            RemiBookSearchPatrol.Instance?.EndWindowPatrol();

        if (persistProgress && IsQuestFeatureEnabled)
        {
            PlayerPrefs.SetInt(PrefsKeyState, (int)s);
            if (s != QuestState.WaitingForBook)
            {
                _hasBookInInventory = false;
                PlayerPrefs.SetInt(PrefsKeyHasBook, 0);
            }
        }
        else if (s != QuestState.WaitingForBook)
            _hasBookInInventory = false;
    }

    private void StoreEntryKind(CommissionEntryKind kind)
    {
        _entryKind = kind;
        if (persistProgress && IsQuestFeatureEnabled)
            PlayerPrefs.SetInt(PrefsKeyEntry, (int)kind);
    }

    /// <summary>教室故事开场后：启动 Day1 Touch 窗口计时，并（若为 Day1）切入 day block B。</summary>
    public void NotifyStoryDay1WindowStart()
    {
        if (!IsQuestFeatureEnabled)
            return;

        RemiPresenceService presence = RemiPresenceService.Instance;
        if (presence != null && presence.WorldTime.storyDay == 1)
        {
            // Day1 B 在教室找书：勿 sync 到「下午图书馆」默认日程，否则面对面共位失败。
            presence.EnterDayBlock(RemiDayBlockSlot.B, syncPhaseHint: false, enterAnchor: false);
            presence.PinClassroomForDay1Commission();
        }

        if (_state != QuestState.WindowOpen)
            return;

        _faceRoundsInWindow = 0;
        _faceDialogueEnteredThisWindow = false;
        RemiBookSearchPatrol.EnsureOn(this);
        StartWindowWatch();
        RemiBookSearchPatrol.Instance?.BeginWindowPatrol();
    }

    /// <summary>玩家打开面对面对话：本窗口改由闲聊小轮保底，停用 guaranteeSeconds。</summary>
    public void NotifyFaceDialogueOpened()
    {
        if (!IsQuestFeatureEnabled || _state != QuestState.WindowOpen)
            return;
        _faceDialogueEnteredThisWindow = true;
    }

    /// <summary>
    /// 面对面自由聊：每发送一句玩家话计 1 小轮（非 history 大窗）。
    /// </summary>
    public void NotifyFacePlayerChat(string playerText)
    {
        if (!IsQuestFeatureEnabled || _state != QuestState.WindowOpen)
            return;

        _faceDialogueEnteredThisWindow = true;
        _faceRoundsInWindow++;
    }

    /// <summary>
    /// 窗口期内玩家文本命中别名：跳过 SendPlayer，直接打开 PlayerAsked 委托。
    /// 未命中或打开失败返回 false（调用方应走普通自由聊）。
    /// </summary>
    public bool TryBeginPlayerAskCommissionFromChat(string playerText, RemiInteraction interaction)
    {
        if (!IsQuestFeatureEnabled || _state != QuestState.WindowOpen)
            return false;
        if (_sequenceRunning || _awaitingGoodbyeConfirm)
            return false;
        if (!MatchesPlayerAskAlias(playerText))
            return false;

        _faceDialogueEnteredThisWindow = true;
        _faceRoundsInWindow++;
        return TryOpenCommission(CommissionEntryKind.PlayerAsked, interaction);
    }

    /// <summary>公共入口：按入口种类打开找书委托。</summary>
    public bool TryOpenCommission(CommissionEntryKind kind, RemiInteraction interaction)
    {
        if (!IsQuestFeatureEnabled)
            return false;
        if (_state == QuestState.WaitingForBook || _state == QuestState.Finished)
            return false;
        if (_sequenceRunning || _awaitingGoodbyeConfirm)
            return false;

        if (promptedAgent == null)
            promptedAgent = FindObjectOfType<PromptedDialogueAgent>();
        if (promptedAgent == null)
        {
            Debug.LogWarning("[RemiBookQuestFlow] 未找到 PromptedDialogueAgent，无法打开委托。");
            return false;
        }

        StopWindowWatch();
        StartCoroutine(CoOpenCommission(kind, interaction));
        return true;
    }

    /// <summary>在 <see cref="CheckPanel"/> 确认检视后调用：获得书（可持久化），场景中书由调用方隐藏。</summary>
    public void NotifyBookPickedUpFromInspect()
    {
        if (!AwaitsBookPickup()) return;
        _hasBookInInventory = true;
        RemiPresenceService.Instance?.ApplyCommissionEvent(RemiPresenceEventKind.PlayerPickedUpBook);
        if (persistProgress && IsQuestFeatureEnabled)
            PlayerPrefs.SetInt(PrefsKeyHasBook, 1);
    }

    /// <summary>由 <see cref="DialoguePanel"/> 的关闭/告别按钮调用。</summary>
    public void StartFirstGoodbyeSequence(RemiInteraction interaction)
    {
        if (!ShouldShowGoodbyeExitUx() || _sequenceRunning || _awaitingGoodbyeConfirm) return;
        if (interaction == null || !interaction.IsInDialogue) return;

        if (!TryOpenCommission(CommissionEntryKind.Goodbye, interaction))
        {
            if (promptedAgent == null)
                promptedAgent = FindObjectOfType<PromptedDialogueAgent>();
            if (promptedAgent == null)
            {
                Debug.LogWarning("[RemiBookQuestFlow] 未找到 PromptedDialogueAgent，直接结束对话。");
                StopWindowWatch();
                _hasBookInInventory = false;
                if (persistProgress && IsQuestFeatureEnabled)
                    PlayerPrefs.SetInt(PrefsKeyHasBook, 0);
                StoreEntryKind(CommissionEntryKind.Goodbye);
                SetState(QuestState.WaitingForBook);
                interaction.EndDialogue();
            }
        }
    }

    /// <summary>托付台词播完后，由 <see cref="DialoguePanel"/> 在玩家点「确认」时调用。</summary>
    public void ConfirmGoodbyeAndClose(RemiInteraction interaction)
    {
        if (!_awaitingGoodbyeConfirm)
            return;

        _awaitingGoodbyeConfirm = false;
        bool advanceDay = _advanceDayAfterConfirmClose;
        _advanceDayAfterConfirmClose = false;

        RemiInteraction target = interaction != null ? interaction : _goodbyeInteraction;
        _goodbyeInteraction = null;
        target?.EndDialogue();
        DialoguePanel.RefreshExitButtonLabel();

        if (advanceDay)
            CompleteThanksAndAdvanceDay();
    }

    private void CompleteThanksAndAdvanceDay()
    {
        onAfterThanksSendSystem?.Invoke();
        StoryNarrativeHintView.TryPlayAfterBookSubmitThanks();

        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector.Instance?.NotifyDay1BookSubmitted();
    }

    private IEnumerator CoOpenCommission(CommissionEntryKind kind, RemiInteraction interaction)
    {
        _sequenceRunning = true;
        _awaitingGoodbyeConfirm = false;

        if (interaction == null)
            interaction = FindObjectOfType<RemiInteraction>();

        if (interaction == null)
        {
            Debug.LogWarning("[RemiBookQuestFlow] 未找到 RemiInteraction，无法打开委托。");
            _sequenceRunning = false;
            if (_state == QuestState.WindowOpen)
                StartWindowWatch();
            yield break;
        }

        if (!interaction.IsInDialogue)
        {
            interaction.StartDialogue(bypassOpenGates: true);
            if (!interaction.IsInDialogue)
            {
                Debug.LogWarning("[RemiBookQuestFlow] StartDialogue 失败，无法打开委托。");
                _sequenceRunning = false;
                if (_state == QuestState.WindowOpen)
                    StartWindowWatch();
                yield break;
            }
        }

        _goodbyeInteraction = interaction;
        DialoguePanel.SetScriptedFlowInputLocked(true);

        if (PromptContextManager.Instance != null)
            PromptContextManager.Instance.SetTurnEmphasis(
                RemiDialogueEmphasisSpec.WithAnchors("书", "AI游戏入门"));

        System.Action<string> reveal = DialoguePanel.CreateWorldRevealCallbackIfOpen();

        string contentId = ResolveRequestContentId(kind);
        RemiSendSystemContentManager.EnsureExists();
        RemiSendSystemContentManager content = RemiSendSystemContentManager.Instance;
        string requestContext = content != null
            ? content.GetInitiator(contentId)
            : string.Empty;

        yield return promptedAgent.SendSystem(
            requestContext,
            (text, expr) => DialoguePanel.OnScriptedUtteranceComplete(text, expr),
            err => { Debug.LogWarning($"[RemiBookQuestFlow] 托付({kind}): {err}"); },
            reveal);

        onAfterRequestSendSystemBeforeClose?.Invoke();
        RemiPresenceService.Instance?.ApplyCommissionEvent(RemiPresenceEventKind.RemiRequestedBookHelp);
        _hasBookInInventory = false;
        if (persistProgress && IsQuestFeatureEnabled)
            PlayerPrefs.SetInt(PrefsKeyHasBook, 0);
        StoreEntryKind(kind);
        SetState(QuestState.WaitingForBook);

        _sequenceRunning = false;
        _awaitingGoodbyeConfirm = true;
        DialoguePanel.EnterGoodbyeConfirmUx();
    }

    private static string ResolveRequestContentId(CommissionEntryKind kind) =>
        kind switch
        {
            CommissionEntryKind.PlayerAsked => RemiSendSystemContentIds.Day1BookRequestPlayerAsked,
            CommissionEntryKind.RemiGuarantee => RemiSendSystemContentIds.Day1BookRequestGuarantee,
            _ => RemiSendSystemContentIds.Day1BookRequest,
        };

    private void StartWindowWatch()
    {
        StopWindowWatch();
        if (!IsQuestFeatureEnabled || _state != QuestState.WindowOpen)
            return;
        _windowTimingActive = true;
        _windowWatchRoutine = StartCoroutine(CoWatchWindow());
    }

    private void StopWindowWatch()
    {
        _windowTimingActive = false;
        if (_windowWatchRoutine != null)
        {
            StopCoroutine(_windowWatchRoutine);
            _windowWatchRoutine = null;
        }
    }

    private IEnumerator CoWatchWindow()
    {
        float startUnscaled = Time.unscaledTime;
        while (_windowTimingActive && IsQuestFeatureEnabled && _state == QuestState.WindowOpen)
        {
            if (_sequenceRunning || _awaitingGoodbyeConfirm || IsDialogueAwaitingRemiReply())
            {
                yield return null;
                continue;
            }

            // 已进过对话：只靠闲聊小轮（NotifyFacePlayerChat 内尝试打开）。
            if (_faceDialogueEnteredThisWindow)
            {
                if (guaranteeMinFaceRounds > 0 && _faceRoundsInWindow >= guaranteeMinFaceRounds)
                {
                    TryOpenCommission(CommissionEntryKind.RemiGuarantee, null);
                    yield break;
                }

                yield return null;
                continue;
            }

            // 尚未进对话：仅时间保底
            float elapsed = Time.unscaledTime - startUnscaled;
            if (guaranteeSeconds > 0f && elapsed >= guaranteeSeconds)
            {
                TryOpenCommission(CommissionEntryKind.RemiGuarantee, null);
                yield break;
            }

            yield return null;
        }

        _windowWatchRoutine = null;
    }

    private static bool IsDialogueAwaitingRemiReply()
    {
        DialoguePanel panel = UiManager.Instance != null
            ? UiManager.Instance.GetPanel<DialoguePanel>()
            : null;
        return panel != null && panel.IsAwaitingRemiReply;
    }

    private bool MatchesPlayerAskAlias(string playerText)
    {
        if (string.IsNullOrWhiteSpace(playerText) || playerAskAliases == null || playerAskAliases.Length == 0)
            return false;

        string text = playerText.Trim();
        for (int i = 0; i < playerAskAliases.Length; i++)
        {
            string alias = playerAskAliases[i];
            if (string.IsNullOrWhiteSpace(alias))
                continue;
            if (text.IndexOf(alias.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    /// <summary>由 <see cref="RemiInteraction"/> / <see cref="RemiBookSubmitInteractable"/> 在按 E 交书时调用。</summary>
    public bool TrySubmitBookFromWorld(RemiInteraction interaction)
    {
        if (!IsQuestFeatureEnabled || _state != QuestState.WaitingForBook || !_hasBookInInventory
            || _sequenceRunning || _awaitingGoodbyeConfirm)
            return false;

        if (interaction == null)
            interaction = FindObjectOfType<RemiInteraction>();
        if (interaction == null || !interaction.IsPlayerInRange)
            return false;

        if (promptedAgent == null)
            promptedAgent = FindObjectOfType<PromptedDialogueAgent>();
        if (promptedAgent == null)
        {
            Debug.LogWarning("[RemiBookQuestFlow] 未找到 PromptedDialogueAgent，无法致谢。");
            _hasBookInInventory = false;
            if (persistProgress && IsQuestFeatureEnabled)
                PlayerPrefs.SetInt(PrefsKeyHasBook, 0);
            SetState(QuestState.Finished);
            return false;
        }

        interaction.StartDialogue(bypassOpenGates: true);
        if (!interaction.IsInDialogue)
            return false;

        StartCoroutine(CoThanks(interaction));
        return true;
    }

    private IEnumerator CoThanks(RemiInteraction interaction)
    {
        _sequenceRunning = true;
        _awaitingGoodbyeConfirm = false;
        _advanceDayAfterConfirmClose = false;
        _goodbyeInteraction = interaction;
        DialoguePanel.SetScriptedFlowInputLocked(true);

        if (PromptContextManager.Instance != null)
            PromptContextManager.Instance.SetTurnEmphasis(RemiDialogueEmphasisSpec.Whole);

        System.Action<string> reveal = DialoguePanel.CreateWorldRevealCallbackIfOpen();

        RemiSendSystemContentManager.EnsureExists();
        RemiSendSystemContentManager content = RemiSendSystemContentManager.Instance;
        string thanksContext = content != null
            ? content.GetInitiator(RemiSendSystemContentIds.Day1BookThanks)
            : string.Empty;

        yield return promptedAgent.SendSystem(
            thanksContext,
            (text, expr) =>
            {
                DialoguePanel.PinPostBookThanksForDialogueOpen(text, expr);
                DialoguePanel.OnScriptedUtteranceComplete(text, expr);
            },
            err => { Debug.LogWarning($"[RemiBookQuestFlow] 致谢: {err}"); },
            reveal);

        RemiPresenceService.Instance?.ApplyCommissionEvent(RemiPresenceEventKind.PlayerSubmittedBook);

        _hasBookInInventory = false;
        if (persistProgress && IsQuestFeatureEnabled)
            PlayerPrefs.SetInt(PrefsKeyHasBook, 0);
        SetState(QuestState.Finished);

        _sequenceRunning = false;
        _awaitingGoodbyeConfirm = true;
        _advanceDayAfterConfirmClose = true;
        DialoguePanel.EnterGoodbyeConfirmUx();
    }

#if UNITY_EDITOR
    [ContextMenu("Reset quest progress (PlayerPrefs + in-memory)")]
    private void Editor_ResetProgress()
    {
        PlayerPrefs.DeleteKey(PrefsKeyState);
        PlayerPrefs.DeleteKey(PrefsKeyHasBook);
        PlayerPrefs.DeleteKey(PrefsKeyEntry);
        PlayerPrefs.Save();
        StopWindowWatch();
        _state = QuestState.WindowOpen;
        _entryKind = CommissionEntryKind.Goodbye;
        _sequenceRunning = false;
        _awaitingGoodbyeConfirm = false;
        _advanceDayAfterConfirmClose = false;
        _goodbyeInteraction = null;
        _hasBookInInventory = false;
        _faceRoundsInWindow = 0;
        _faceDialogueEnteredThisWindow = false;
        RemiBookSearchPatrol.Instance?.EndWindowPatrol();
        DialoguePanel.ClearPinnedRemiOpenLines();
    }
#endif
}
