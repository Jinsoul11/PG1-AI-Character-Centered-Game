using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 继承BasePanel复用显示/隐藏渐变逻辑
public class DialoguePanel : BasePanel
{
    [SerializeField] private DeepSeekDialogueManager dialogManager;
    [SerializeField] private TMP_InputField userInputField;
    [SerializeField] private TMP_Text RemiPromptText;
    [SerializeField] private Button sendButton;
    [SerializeField] private Button historyButton;
    [SerializeField] private ChatHistoryPanel historyPanel;
    [SerializeField] private Button backButton;
    [SerializeField] private Button changeButton;

    private TMP_Text _backButtonLabel;

    [Header("制作组推荐问题（输入框右侧，可拖 3 个 Button）")]
    [Tooltip("每个按钮下需有 TMP_Text 显示文案；点击后把该文案写入输入框。开对话时隐藏，闲置提议后再显示。Day1 找书窗口期隐藏芯片；进对话后保底只看闲聊轮数，不走闲置时间。")]
    [SerializeField] private Button[] suggestedQuestionButtons = new Button[3];

    [Header("闲置话题引导")]
    [Tooltip("Remi 上一句回复结束后，空闲多久再由她提议并显示话题（秒）。Day1 窗口期同阈值改为触发找书委托。")]
    [SerializeField] private float idleTopicSeconds = 60f;
    [Tooltip("开启后闲置提议走 SendSystem（LLM）；失败则回退固定 proposeLine。")]
    [SerializeField] private bool useSendSystemForIdlePropose = true;
    [Tooltip("仅当 RemiSendSystemContentManager 缺失时兜底；正式文案请改 System/RemiSendSystemContentManager。")]
    [FormerlySerializedAs("idleTopicSets")]
    [SerializeField] private RemiFaceIdleTopicSet[] idleTopicSetsFallback = new RemiFaceIdleTopicSet[0];

    [Header("互动节奏 · 推荐闲聊问题（无场景配置时的兜底，已较少使用）")]
    [SerializeField] private string surfaceQuestion0 = "今天课上怎么样？";
    [SerializeField] private string surfaceQuestion1 = "你最近在忙什么？";
    [SerializeField] private string relationalQuestion0 = "找书的事还有什么要叮嘱的吗？";
    [SerializeField] private string influentialQuestion0 = "你之后有什么打算？";
    [SerializeField] private string influentialQuestion1 = "最近有什么想做的事吗？";

    [Tooltip("可选：有则可在适当时机从剧情行生成按钮文案（见 ApplySuggestedQuestionsFromStoryDirector）")]
    [SerializeField] private StoryDirector storyDirectorForSuggestions;

    public Remi remi;
    private PromptedDialogueAgent promptedAgent;
    private RemiResponseTextLayout _remiResponseTextLayout;
    private RemiRoleWorldUI _roleWorldUi;
    private bool _scriptedFlowInputLocked;

    /// <summary>本会话是否已做过闲置提议（每次打开对话最多一次）。</summary>
    private bool _idleTopicOfferedThisSession;
    /// <summary>话题按钮是否已解锁显示。</summary>
    private bool _suggestedTopicsUnlocked;
    /// <summary>正在等 Remi 回复（玩家已发送或提议中）。</summary>
    private bool _awaitingRemiReply;
    /// <summary>正在等 Remi 面对面回复（自由聊或闲置提议）。</summary>
    public bool IsAwaitingRemiReply => _awaitingRemiReply;
    private bool _day3PendingConfirmUxApplied;

    /// <summary>脚本演出锁定输入期间。</summary>
    public bool IsScriptedFlowInputLocked => _scriptedFlowInputLocked;
    private Coroutine _idleTopicWatchCo;
    private Coroutine _idleProposeCo;

    /// <summary>剧情结束后 <see cref="StoryDirector"/> 写入，用于首次打开对话面板时同步世界气泡。</summary>
    private static string _storyPrologueWorldLine;
    private static string _storyPrologueExpression;

    /// <summary>面前问候 SendSystem 仅触发一次时，将其回复固定为之后打开对话面板时的默认气泡（直到交书感谢覆盖）。</summary>
    private static string _pinnedApproachOpenLine;
    private static string _pinnedApproachOpenExpr;

    /// <summary>交书后致谢 SendSystem 的回复，优先于面前问候作为打开面板时的默认气泡。</summary>
    private static string _pinnedThanksOpenLine;
    private static string _pinnedThanksOpenExpr;

    /// <summary>共现 / 终幕 SendSystem 后钉选打开面板时的默认气泡。</summary>
    private static string _pinnedCoPresenceOpenLine;
    private static string _pinnedCoPresenceOpenExpr;

    /// <summary>由 <see cref="StoryDirector"/> 在 AI 问候成功后调用。</summary>
    public static void RegisterStoryPrologueForDialogue(string remiText, string expression = null)
    {
        _storyPrologueWorldLine = string.IsNullOrWhiteSpace(remiText) ? null : remiText.Trim();
        _storyPrologueExpression = string.IsNullOrWhiteSpace(expression) ? null : expression.Trim();
    }

    /// <summary>由 <see cref="RemiFrontApproachTrigger"/> 在面前问候成功后调用：之后每次打开对话面板都显示该句（除非已被交书感谢覆盖）。</summary>
    public static void PinApproachGreetingForDialogueOpen(string text, string expression = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _pinnedApproachOpenLine = text.Trim();
        _pinnedApproachOpenExpr = string.IsNullOrWhiteSpace(expression) ? null : expression.Trim();
    }

    /// <summary>由 <see cref="RemiBookQuestFlow"/> 在交书致谢成功后调用。</summary>
    public static void PinPostBookThanksForDialogueOpen(string text, string expression = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _pinnedThanksOpenLine = text.Trim();
        _pinnedThanksOpenExpr = string.IsNullOrWhiteSpace(expression) ? null : expression.Trim();
    }

    /// <summary>共现 beat 固定剧情结束后 SendSystem 问候（会清除较低优先级的钉选句）。</summary>
    public static void PinCoPresenceGreetingForDialogueOpen(string text, string expression = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _pinnedThanksOpenLine = null;
        _pinnedThanksOpenExpr = null;
        _pinnedApproachOpenLine = null;
        _pinnedApproachOpenExpr = null;
        _pinnedCoPresenceOpenLine = text.Trim();
        _pinnedCoPresenceOpenExpr = string.IsNullOrWhiteSpace(expression) ? null : expression.Trim();
    }

    public static void ClearPinnedRemiOpenLines()
    {
        _pinnedApproachOpenLine = null;
        _pinnedApproachOpenExpr = null;
        _pinnedThanksOpenLine = null;
        _pinnedThanksOpenExpr = null;
        _pinnedCoPresenceOpenLine = null;
        _pinnedCoPresenceOpenExpr = null;
    }

    public static bool IsDialogueOpen()
    {
        DialoguePanel panel = UiManager.Instance?.GetPanel<DialoguePanel>();
        return panel != null && panel.isShow && panel.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// System/观察探针等：把 Remi 一句写入历史 + 世界气泡（对话面板未打开时也会尝试写 Remi 下 NPCPromptText）。
    /// </summary>
    public static void ApplyRemiLineGlobally(string text, string expression = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim();
        expression = string.IsNullOrWhiteSpace(expression) ? null : expression.Trim();

        ChatHistoryPanel hp = UiManager.Instance.GetPanel<ChatHistoryPanel>();
        if (hp == null) hp = UiManager.Instance.ShowPanel<ChatHistoryPanel>();
        hp.gameObject.SetActive(false);
        hp.AddChatItem("Remi", text);
        RemiDialogueArchive.RecordStatic(
            "Remi",
            text,
            RemiDialogueArchiveSource.Scripted,
            RemiInteractionChannel.FaceToFace);

        RegisterStoryPrologueForDialogue(text, expression);

        DialoguePanel dp = UiManager.Instance.GetPanel<DialoguePanel>();
        if (dp != null)
            dp.ApplyInitialWorldResponseBubble();
        else if (ShouldShowFaceResponseText())
            ApplyWorldBubbleWithoutDialoguePanel(text, expression);
    }

    static void ApplyWorldBubbleWithoutDialoguePanel(string text, string expression)
    {
        GameObject remiObj = GameObject.Find("Remi");
        if (remiObj == null) return;

        Remi remiComp = remiObj.GetComponent<Remi>();
        foreach (TMP_Text tmp in remiObj.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!tmp.CompareTag("NPCPromptText")) continue;
            tmp.text = text;
            RemiResponseTextLayout layout = tmp.GetComponentInParent<RemiResponseTextLayout>();
            layout?.RefreshLayout();
            break;
        }

        if (remiComp != null && expression != null)
            remiComp.PlayExpression(MapExpressionFromString(expression));
    }

    /// <summary>
    /// 剧本/SendSystem 台词播完：写入历史，保持当前 Response 最终句（勿走 ApplyInitialWorldResponseBubble 覆盖）。
    /// </summary>
    public static void OnScriptedUtteranceComplete(string text, string expression = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim();
        expression = string.IsNullOrWhiteSpace(expression) ? null : expression.Trim();

        ChatHistoryPanel hp = UiManager.Instance.GetPanel<ChatHistoryPanel>();
        if (hp == null) hp = UiManager.Instance.ShowPanel<ChatHistoryPanel>();
        hp.gameObject.SetActive(false);
        hp.AddChatItem("Remi", text);
        RemiDialogueArchive.RecordStatic(
            "Remi",
            text,
            RemiDialogueArchiveSource.Scripted,
            RemiInteractionChannel.FaceToFace);

        RegisterStoryPrologueForDialogue(text, expression);

        DialoguePanel dp = UiManager.Instance.GetPanel<DialoguePanel>();
        if (dp != null && IsDialogueOpen())
        {
            if (ShouldShowFaceResponseText())
                dp.SetRemiWorldResponseText(text);
            if (dp.remi != null && !string.IsNullOrEmpty(expression))
                dp.remi.PlayExpression(MapExpressionFromString(expression));
            return;
        }

        if (ShouldShowFaceResponseText())
            ApplyWorldBubbleWithoutDialoguePanel(text, expression);
    }

    /// <summary>面对面 SendSystem 打字机逐字刷新 Response；无文字呈现时返回 null。</summary>
    public static System.Action<string> CreateWorldRevealCallbackIfOpen()
    {
        if (!IsDialogueOpen() || !ShouldShowFaceResponseText())
            return null;

        DialoguePanel dp = UiManager.Instance.GetPanel<DialoguePanel>();
        return dp != null ? dp.RevealWorldResponsePartial : null;
    }

    /// <summary>告别/致谢演出期间禁用发送与关闭，避免提前退出面板。</summary>
    public static void SetScriptedFlowInputLocked(bool locked)
    {
        DialoguePanel dp = UiManager.Instance?.GetPanel<DialoguePanel>();
        dp?._SetScriptedFlowInputLocked(locked);
    }

    /// <summary>托付台词播完：仅启用「确认」关闭按钮。</summary>
    public static void EnterGoodbyeConfirmUx()
    {
        DialoguePanel dp = UiManager.Instance?.GetPanel<DialoguePanel>();
        dp?.ApplyGoodbyeConfirmUx();
    }

    public static void RefreshExitButtonLabel()
    {
        DialoguePanel dp = UiManager.Instance?.GetPanel<DialoguePanel>();
        dp?.ApplyExitButtonLabel();
    }

    /// <summary>Day3 保底提案后：当面只留确认 Chip，关闭自由输入。</summary>
    public static void NotifyDay3PendingConfirm()
    {
        DialoguePanel dp = UiManager.Instance?.GetPanel<DialoguePanel>();
        if (dp == null || !dp.isShow)
            return;
        dp.ApplyDay3PendingConfirmUx();
    }

    /// <summary>离开 PendingConfirm 后：刷新当面 Chip / 输入。</summary>
    public static void RefreshDay3FaceConfirmUx()
    {
        DialoguePanel dp = UiManager.Instance?.GetPanel<DialoguePanel>();
        if (dp == null || !dp.isShow)
            return;
        dp.RefreshSuggestedQuestionsForRhythm();
    }

    /// <summary>已点「那走吧」、Voice 尚未结束：藏确认 Chip，输入保持关闭。</summary>
    public static void BeginDay3ConfirmAcceptUx()
    {
        DialoguePanel dp = UiManager.Instance?.GetPanel<DialoguePanel>();
        if (dp == null || !dp.isShow)
            return;
        dp.ApplyDay3ConfirmAcceptBusyUx();
    }

    /// <summary>Remi 已答应偏离：禁止继续输入，保留返回键以便关面板后切公寓。</summary>
    public static void BeginDay3AcceptedAwaitCloseUx()
    {
        DialoguePanel dp = UiManager.Instance?.GetPanel<DialoguePanel>();
        if (dp == null || !dp.isShow)
            return;
        dp.ApplyDay3AcceptedAwaitCloseUx();
    }

    private void RevealWorldResponsePartial(string text) => SetRemiWorldResponseText(text);

    private void _SetScriptedFlowInputLocked(bool locked)
    {
        _scriptedFlowInputLocked = locked;
        if (backButton != null)
            backButton.interactable = !locked;
        if (sendButton != null)
            sendButton.interactable = !locked;
        if (userInputField != null)
            userInputField.interactable = !locked;
        if (locked)
        {
            _suggestedTopicsUnlocked = false;
            if (suggestedQuestionButtons != null)
            {
                foreach (Button btn in suggestedQuestionButtons)
                {
                    if (btn != null)
                        btn.gameObject.SetActive(false);
                }
            }
        }
        else if (suggestedQuestionButtons != null)
        {
            foreach (Button btn in suggestedQuestionButtons)
            {
                if (btn != null)
                    btn.interactable = true;
            }
        }
    }

    private void ApplyGoodbyeConfirmUx()
    {
        _scriptedFlowInputLocked = false;
        if (backButton != null)
            backButton.interactable = true;
        if (sendButton != null)
            sendButton.interactable = false;
        if (userInputField != null)
            userInputField.interactable = false;
        if (suggestedQuestionButtons != null)
        {
            foreach (Button btn in suggestedQuestionButtons)
            {
                if (btn != null)
                    btn.interactable = false;
            }
        }

        ApplyExitButtonLabel();
    }

    private void ApplyDay3PendingConfirmUx()
    {
        _day3PendingConfirmUxApplied = true;
        _scriptedFlowInputLocked = false;
        if (sendButton != null)
            sendButton.interactable = false;
        if (userInputField != null)
            userInputField.interactable = false;

        string confirm = RemiDemoSpineStoryChips.GetPlayerLineDisplay(RemiSpineStoryChipId.Day3InviteToDorm);
        SetSuggestedQuestionLabels(confirm, string.Empty, string.Empty);
        _suggestedTopicsUnlocked = true;
        ApplySuggestedTopicsVisibility();
        ApplyExitButtonLabel();
    }

    private void ApplyDay3ConfirmAcceptBusyUx()
    {
        _day3PendingConfirmUxApplied = true;
        if (sendButton != null)
            sendButton.interactable = false;
        if (userInputField != null)
            userInputField.interactable = false;
        HideAllSuggestedQuestionButtons();
    }

    private void ApplyDay3AcceptedAwaitCloseUx()
    {
        _day3PendingConfirmUxApplied = false;
        _scriptedFlowInputLocked = false;
        if (backButton != null)
            backButton.interactable = true;
        if (sendButton != null)
            sendButton.interactable = false;
        if (userInputField != null)
            userInputField.interactable = false;
        HideAllSuggestedQuestionButtons();
        ApplyExitButtonLabel();
    }

    // 重写BasePanel的Init方法（替代原Start，统一初始化入口）
    public override void Init()
    {
        GameObject remiObj = GameObject.Find("Remi");
        remi=remiObj.GetComponent<Remi>();
        if (dialogManager == null)
            dialogManager = DeepSeekDialogueManager.Instance != null
                ? DeepSeekDialogueManager.Instance
                : FindObjectOfType<DeepSeekDialogueManager>();
        promptedAgent = FindObjectOfType<PromptedDialogueAgent>();

        backButton.onClick.AddListener(OnBackButtonClicked);
        if (backButton != null)
            _backButtonLabel = backButton.GetComponentInChildren<TMP_Text>(true);

        sendButton.onClick.AddListener(SendUserMessage);

        historyButton.onClick.AddListener(() =>
        {
            if (historyPanel == null)
                historyPanel = UiManager.Instance.GetPanel<ChatHistoryPanel>();
            if (historyPanel == null)
                historyPanel = UiManager.Instance.ShowPanel<ChatHistoryPanel>();
            historyPanel.gameObject.SetActive(true);
            // 仅隐藏对话面板，勿 HidePanel（否则会销毁并在与淡出竞态时把新面板一并删掉）
            gameObject.SetActive(false);
        });

        //加载历史面板
        historyPanel = UiManager.Instance.ShowPanel<ChatHistoryPanel>();
        historyPanel.gameObject.SetActive(false);

        //绑定AI回复
        TMP_Text[] promptTexts = remiObj.GetComponentsInChildren<TMP_Text>(true); // true=包含非激活物体
        foreach (var text in promptTexts)
        {
            if (text.CompareTag("NPCPromptText"))
            {
                RemiPromptText = text;
                break;
            }
        }
        if (RemiPromptText == null)
        {
            Debug.LogError("Remi角色下找不到带NPCPromptText标签的Text组件！");
            return;
        }

        _remiResponseTextLayout = RemiPromptText.GetComponentInParent<RemiResponseTextLayout>();
        if (_remiResponseTextLayout == null)
            _remiResponseTextLayout = remiObj.GetComponentInChildren<RemiResponseTextLayout>(true);

        _roleWorldUi = remiObj.GetComponentInChildren<RemiRoleWorldUI>(true);
        ApplyFacePresentationToWorldUi();

        if (dialogManager == null)
            Debug.LogError("DialoguePanel：场景中找不到 DeepSeekDialogueManager。");

        WireSuggestedQuestionButtons();
        _suggestedTopicsUnlocked = false;
        RefreshSuggestedQuestionsForRhythm();
        SetSuggestedTopicsVisible(false);
        // 再刷一次：Day3 保底确认 Chip「那走吧」在锁定闲置话题时仍要露出
        RefreshSuggestedQuestionsForRhythm();

        // 非 RemiInteraction 入口（如 FirstMeet）也会打开本面板，同样需要进入待机2
        if (remi != null)
            remi.SetDialogueBodyIdle(true);

        ApplyInitialWorldResponseBubble();
        RefreshExitButtonLabel();
    }

    /// <summary>
    /// 打开面板时同步 Remi 世界气泡：交书感谢 &gt; 面前问候（各只钉一次）&gt; 剧情序章 &gt; 历史中最后一句 Remi。
    /// </summary>
    public void ApplyInitialWorldResponseBubble()
    {
        if (RemiPromptText == null) return;
        if (!ShouldShowFaceResponseText())
        {
            ClearRemiWorldResponseText();
            return;
        }

        if (TryApplyPinnedOpenLines())
            return;

        if (!string.IsNullOrEmpty(_storyPrologueWorldLine))
        {
            SetRemiWorldResponseText(_storyPrologueWorldLine);
            if (remi != null && !string.IsNullOrEmpty(_storyPrologueExpression))
                remi.PlayExpression(MapExpressionFromString(_storyPrologueExpression));
            _storyPrologueWorldLine = null;
            _storyPrologueExpression = null;
            return;
        }

        var hp = UiManager.Instance.GetPanel<ChatHistoryPanel>();
        if (hp != null && hp.TryGetLastRemiLine(out string last) && !string.IsNullOrEmpty(last))
            SetRemiWorldResponseText(last);
    }

    private bool TryApplyPinnedOpenLines()
    {
        if (!string.IsNullOrEmpty(_pinnedCoPresenceOpenLine))
        {
            SetRemiWorldResponseText(_pinnedCoPresenceOpenLine);
            if (remi != null && !string.IsNullOrEmpty(_pinnedCoPresenceOpenExpr))
                remi.PlayExpression(MapExpressionFromString(_pinnedCoPresenceOpenExpr));
            return true;
        }

        if (!string.IsNullOrEmpty(_pinnedThanksOpenLine))
        {
            SetRemiWorldResponseText(_pinnedThanksOpenLine);
            if (remi != null && !string.IsNullOrEmpty(_pinnedThanksOpenExpr))
                remi.PlayExpression(MapExpressionFromString(_pinnedThanksOpenExpr));
            return true;
        }

        if (!string.IsNullOrEmpty(_pinnedApproachOpenLine))
        {
            SetRemiWorldResponseText(_pinnedApproachOpenLine);
            if (remi != null && !string.IsNullOrEmpty(_pinnedApproachOpenExpr))
                remi.PlayExpression(MapExpressionFromString(_pinnedApproachOpenExpr));
            return true;
        }

        return false;
    }

    /// <summary>
    /// 仅关闭对话 UI：走 <see cref="RemiInteraction.EndDialogue"/> 以恢复移动/镜头；若无交互组件则只隐藏面板。
    /// </summary>
    private void OnBackButtonClicked()
    {
        if (_scriptedFlowInputLocked)
            return;

        RemiInteraction interaction = remi != null ? remi.GetComponent<RemiInteraction>() : FindObjectOfType<RemiInteraction>();

        RemiDemoSpineDirector.EnsureExists();
        if (RemiDemoSpineDirector.Instance != null &&
            RemiDemoSpineDirector.Instance.IsPendingDay3ApartmentTravel)
        {
            if (interaction != null && interaction.IsInDialogue)
                interaction.EndDialogue();
            else
            {
                if (remi != null)
                    remi.SetDialogueBodyIdle(false);
                UiManager.Instance.HidePanel<DialoguePanel>();
                RemiDemoSpineDirector.Instance.NotifyPanelClosedForDay3ApartmentTravel();
            }

            return;
        }

        RemiBookQuestFlow bookFlow = RemiBookQuestFlow.Instance;
        if (bookFlow != null && bookFlow.IsAwaitingGoodbyeConfirm)
        {
            if (interaction != null && interaction.IsInDialogue)
                bookFlow.ConfirmGoodbyeAndClose(interaction);
            return;
        }

        RemiLibraryDay2CoPresenceFlow day2Flow = RemiLibraryDay2CoPresenceFlow.Instance;
        if (day2Flow != null && day2Flow.IsAwaitingStayConfirm)
        {
            if (interaction != null && interaction.IsInDialogue)
                day2Flow.ConfirmStayAndClose(interaction);
            return;
        }

        if (bookFlow != null && bookFlow.ShouldShowGoodbyeExitUx())
        {
            if (interaction != null && interaction.IsInDialogue)
                bookFlow.StartFirstGoodbyeSequence(interaction);
            return;
        }

        if (day2Flow != null && day2Flow.ShouldShowStudyExitUx())
        {
            if (interaction != null && interaction.IsInDialogue)
                day2Flow.StartStudySequence(interaction);
            return;
        }

        if (day2Flow != null && day2Flow.ShouldShowStayExitUx())
        {
            if (interaction != null && interaction.IsInDialogue)
                day2Flow.StartStaySequence(interaction);
            return;
        }

        if (interaction != null && interaction.IsInDialogue)
            interaction.EndDialogue();
        else
        {
            if (remi != null)
                remi.SetDialogueBodyIdle(false);
            UiManager.Instance.HidePanel<DialoguePanel>();
        }
    }

    private void WireSuggestedQuestionButtons()
    {
        if (suggestedQuestionButtons == null || userInputField == null) return;
        for (int i = 0; i < suggestedQuestionButtons.Length; i++)
        {
            Button btn = suggestedQuestionButtons[i];
            if (btn == null) continue;
            btn.onClick.RemoveAllListeners();
            int captured = i;
            btn.onClick.AddListener(() => OnSuggestedQuestionButtonClicked(captured));
        }
    }

    private void OnSuggestedQuestionButtonClicked(int index)
    {
        if (suggestedQuestionButtons == null || index < 0 || index >= suggestedQuestionButtons.Length) return;
        Button btn = suggestedQuestionButtons[index];
        if (btn == null) return;

        TMP_Text label = btn.GetComponentInChildren<TMP_Text>(true);
        string text = label != null ? label.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(text)) return;

        // Day3：当面 Chip「今晚方便来宿舍聊聊吗？」（旧「那走吧」也归入同一入口）
        if (RemiDemoSpineStoryChips.IsDay3FaceStoryChipLine(text))
        {
            string plain = RemiDemoSpineStoryChips.GetPlayerLine(RemiSpineStoryChipId.Day3InviteToDorm);
            if (historyPanel != null)
                historyPanel.AddChatItem("user", plain);
            ArchiveFacePlayerUtterance(plain);
            RemiDemoSpineDirector.EnsureExists();
            RemiDemoSpineDirector.Instance?.OnStoryChipSelected(RemiSpineStoryChipId.Day3InviteToDorm);
            return;
        }

        if (userInputField == null) return;
        userInputField.text = text;
        userInputField.caretPosition = text.Length;
        userInputField.ActivateInputField();
    }

    /// <summary>按当前场景刷新话题文案；是否显示由闲置解锁状态决定。</summary>
    public void RefreshSuggestedQuestionsForRhythm()
    {
        RemiDemoSpineDirector.EnsureExists();
        if (RemiDemoSpineDirector.Instance != null &&
            RemiDemoSpineDirector.Instance.CanOfferDay3InviteToDorm())
        {
            ApplyDay3PendingConfirmUx();
            return;
        }

        if (_day3PendingConfirmUxApplied)
        {
            _day3PendingConfirmUxApplied = false;
            if (!_scriptedFlowInputLocked && !_awaitingRemiReply)
            {
                if (sendButton != null)
                    sendButton.interactable = true;
                if (userInputField != null)
                    userInputField.interactable = true;
            }
        }

        // Day1 / Day2 Window：隐藏推荐芯片（入口靠别名 / 告别 / 保底）
        if (IsDay1CommissionWindowOpen() || IsDay2CoPresenceWindowOpen())
        {
            _suggestedTopicsUnlocked = false;
            HideAllSuggestedQuestionButtons();
            return;
        }

        RemiFaceIdleTopicSet set = ResolveIdleTopicSet();

        string l0 = set != null ? set.topic0 : surfaceQuestion0;
        string l1 = set != null ? set.topic1 : surfaceQuestion1;
        string l2 = set != null ? set.topic2 : string.Empty;

        // ContentManager / 场景条目都没有时，仍可按关系档兜底（兼容旧 Inspector 字段）
        if (set == null || IsIdleTopicSetEmpty(set))
        {
            RemiDialogueDepthStage stage = RemiPresenceService.Instance != null
                ? RemiPresenceService.Instance.DialogueDepthStage
                : RemiDialogueDepthStage.Surface;
            switch (stage)
            {
                case RemiDialogueDepthStage.Influential:
                    l0 = influentialQuestion0;
                    l1 = influentialQuestion1;
                    l2 = relationalQuestion0;
                    break;
                case RemiDialogueDepthStage.Relational:
                    l0 = relationalQuestion0;
                    l1 = surfaceQuestion0;
                    l2 = surfaceQuestion1;
                    break;
                default:
                    l0 = surfaceQuestion0;
                    l1 = surfaceQuestion1;
                    l2 = string.Empty;
                    break;
            }
        }

        SetSuggestedQuestionLabels(l0, l1, l2);

        ApplySuggestedTopicsVisibility();
    }

    private RemiFaceIdleTopicSet ResolveIdleTopicSet()
    {
        SceneTravelLocation scene = SceneTravelCatalog.ResolveFromActiveScene();
        RemiSendSystemContentManager.EnsureExists();
        if (RemiSendSystemContentManager.Instance != null)
            return RemiSendSystemContentManager.Instance.GetFaceIdleTopicSet(scene);

        return RemiFaceIdleTopicCatalog.Resolve(idleTopicSetsFallback, scene);
    }

    private static bool IsIdleTopicSetEmpty(RemiFaceIdleTopicSet set)
    {
        if (set == null)
            return true;
        return string.IsNullOrWhiteSpace(set.topic0) &&
               string.IsNullOrWhiteSpace(set.topic1) &&
               string.IsNullOrWhiteSpace(set.topic2);
    }

    private void ApplySuggestedTopicsVisibility()
    {
        if (suggestedQuestionButtons == null)
            return;

        for (int i = 0; i < suggestedQuestionButtons.Length; i++)
        {
            if (suggestedQuestionButtons[i] == null)
                continue;

            string label = string.Empty;
            TMP_Text tmp = suggestedQuestionButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
                label = tmp.text != null ? tmp.text.Trim() : string.Empty;

            bool isDay3StoryChip = RemiDemoSpineStoryChips.IsDay3FaceStoryChipLine(label);
            // Day3 邀约 Chip：不必等闲置话题解锁
            // Day1 找书窗口：始终隐藏芯片
            if (IsDay1CommissionWindowOpen())
            {
                suggestedQuestionButtons[i].gameObject.SetActive(false);
                continue;
            }

            bool show = !string.IsNullOrEmpty(label) && (_suggestedTopicsUnlocked || isDay3StoryChip);
            suggestedQuestionButtons[i].gameObject.SetActive(show);
        }
    }

    private void HideAllSuggestedQuestionButtons()
    {
        if (suggestedQuestionButtons == null)
            return;
        for (int i = 0; i < suggestedQuestionButtons.Length; i++)
        {
            if (suggestedQuestionButtons[i] != null)
                suggestedQuestionButtons[i].gameObject.SetActive(false);
        }
    }

    private void SetSuggestedTopicsVisible(bool visible)
    {
        _suggestedTopicsUnlocked = visible;
        ApplySuggestedTopicsVisibility();
    }

    /// <summary>
    /// 运行时设置三个推荐按钮上显示的文案（会写到各 Button 子级第一个 TMP_Text）。
    /// </summary>
    public void SetSuggestedQuestionLabels(string line0, string line1, string line2)
    {
        string[] lines = { line0 ?? string.Empty, line1 ?? string.Empty, line2 ?? string.Empty };
        if (suggestedQuestionButtons == null) return;
        for (int i = 0; i < suggestedQuestionButtons.Length && i < lines.Length; i++)
        {
            if (suggestedQuestionButtons[i] == null) continue;
            TMP_Text label = suggestedQuestionButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = lines[i];
        }
    }

    /// <summary>
    /// 从 <see cref="StoryDirector"/> 的前若干行 <see cref="StoryDirector.StoryLine.text"/> 填到按钮（仅作占位逻辑：取前 3 句全文，过长不截断；你可改为挑特定行或摘要）。
    /// 需要先在 Inspector 绑定 <see cref="storyDirectorForSuggestions"/>。
    /// </summary>
    public void ApplySuggestedQuestionsFromStoryDirector()
    {
        if (storyDirectorForSuggestions == null || suggestedQuestionButtons == null) return;
        IReadOnlyList<StoryDirector.StoryLine> storyLines = storyDirectorForSuggestions.Lines;
        if (storyLines == null || storyLines.Count == 0) return;

        for (int i = 0; i < suggestedQuestionButtons.Length && i < storyLines.Count; i++)
        {
            if (suggestedQuestionButtons[i] == null) continue;
            TMP_Text label = suggestedQuestionButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (label == null) continue;
            string t = storyLines[i].text;
            if (!string.IsNullOrEmpty(t))
                label.text = t.Trim();
        }
    }

    private void SendUserMessage()
    {
        if (_scriptedFlowInputLocked)
            return;

        RemiDemoSpineDirector.EnsureExists();
        if (RemiDemoSpineDirector.Instance != null &&
            RemiDemoSpineDirector.Instance.IsDay3DeviationPendingConfirm)
            return;

        if (RemiDemoSpineDirector.Instance != null &&
            RemiDemoSpineDirector.Instance.IsPendingDay3ApartmentTravel)
            return;

        string userMessage = userInputField.text.Trim();
        if (string.IsNullOrEmpty(userMessage)) return;

        RemiInteraction faceInteraction = remi != null
            ? remi.GetComponent<RemiInteraction>()
            : FindObjectOfType<RemiInteraction>();

        // Day1 Window：别名问起 → 跳过 SendPlayer，直接派送找书委托
        RemiBookQuestFlow bookFlow = RemiBookQuestFlow.Instance;
        if (bookFlow != null &&
            bookFlow.TryBeginPlayerAskCommissionFromChat(userMessage, faceInteraction))
        {
            if (historyPanel != null)
                historyPanel.AddChatItem("user", userMessage);
            ArchiveFacePlayerUtterance(userMessage);
            userInputField.text = "";
            return;
        }

        // Day2 Window：别名问起 → 跳过 SendPlayer，直接进共现 Anchor Story
        RemiLibraryDay2CoPresenceFlow day2Flow = RemiLibraryDay2CoPresenceFlow.Instance;
        if (day2Flow != null &&
            day2Flow.TryBeginPlayerAskAnchorFromChat(userMessage, faceInteraction))
        {
            if (historyPanel != null)
                historyPanel.AddChatItem("user", userMessage);
            ArchiveFacePlayerUtterance(userMessage);
            userInputField.text = "";
            return;
        }

        // 让“玩家发起”成为默认上下文（避免忘记写 PromptContext）
        if (PromptContextManager.Instance != null)
        {
            PromptContextManager.Instance.SetTurnKind(RemiPromptTurnKind.PlayerChat);
            PromptContextManager.Instance.SetInitiator(PromptContextManager.InitiatorRole.Player, "");
        }

        _awaitingRemiReply = true;
        bookFlow?.NotifyFacePlayerChat(userMessage);
        day2Flow?.NotifyFacePlayerChat(userMessage);

        // 显示加载状态
        remi.animator.SetBool("Thinking",true);
        if (ShouldShowFaceResponseText())
            SetRemiWorldResponseText("嗯...");

        userInputField.text = "";
        StartCoroutine(CoSendFaceUserMessage(userMessage));
    }

    private IEnumerator CoSendFaceUserMessage(string userMessage)
    {
        System.Action<string> revealCallback = ShouldShowFaceResponseText()
            ? (System.Action<string>)SetRemiWorldResponseText
            : null;

        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector spine = RemiDemoSpineDirector.Instance;
        if (spine != null &&
            spine.IsDay3DeviationWindowOpen &&
            !spine.IsDay3DeviationPendingConfirm &&
            !spine.IsSpineSequenceRunning)
        {
            if (promptedAgent == null)
                promptedAgent = FindObjectOfType<PromptedDialogueAgent>();

            if (promptedAgent != null)
            {
                RemiDeviationDetectIntent.Result detect = default;
                yield return promptedAgent.CoDetectDay3DeviationPropose(
                    userMessage,
                    r => detect = r);

                if (spine.CanAcceptDay3DeviationFromDetect(detect))
                {
                    yield return spine.CoPerformDay3DeviationAcceptWithVoice(
                        userMessage,
                        detect,
                        promptedAgent,
                        (remiLine, expression) =>
                        {
                            remi.animator.SetBool("Thinking", false);
                            RemiExpression mappedExpr = MapExpression(expression);
                            remi.PlayExpression(mappedExpr);

                            if (ShouldShowFaceResponseText())
                                SetRemiWorldResponseText(remiLine);
                            if (historyPanel != null)
                            {
                                historyPanel.AddChatItem("user", userMessage);
                                historyPanel.AddChatItem("Remi", remiLine);
                            }

                            ArchiveFaceFreeChat(userMessage, remiLine);
                            OnRemiFaceReplyFinished();
                        });
                    yield break;
                }
            }
        }

        if (promptedAgent != null)
        {
            yield return promptedAgent.SendPlayer(
                userMessage,
                (responseText, expression) =>
                {
                    remi.animator.SetBool("Thinking", false);
                    RemiExpression mappedExpr = MapExpression(expression);
                    remi.PlayExpression(mappedExpr);

                    if (ShouldShowFaceResponseText())
                        SetRemiWorldResponseText(responseText);
                    historyPanel.AddChatItem("user", userMessage);
                    historyPanel.AddChatItem("Remi", responseText);
                    ArchiveFaceFreeChat(userMessage, responseText);
                    OnRemiFaceReplyFinished();
                },
                (error) =>
                {
                    remi.animator.SetBool("Thinking", false);
                    if (ShouldShowFaceResponseText())
                        SetRemiWorldResponseText($"请求失败：{error}");
                    OnRemiFaceReplyFinished();
                },
                revealCallback);
            yield break;
        }

        RemiPresenceService.Instance?.PushToPromptContext(RemiStageExpressionContext.FaceToFaceChat);

        yield return dialogManager.SendMessageWithEmotion(
            userMessage,
            (responseText, expression) =>
            {
                remi.animator.SetBool("Thinking", false);
                RemiExpression mappedExpr = MapExpression(expression);
                remi.PlayExpression(mappedExpr);

                if (ShouldShowFaceResponseText())
                    SetRemiWorldResponseText(responseText);
                historyPanel.AddChatItem("user", userMessage);
                historyPanel.AddChatItem("Remi", responseText);
                ArchiveFaceFreeChat(userMessage, responseText);
                OnRemiFaceReplyFinished();
            },
            (error) =>
            {
                remi.animator.SetBool("Thinking", false);
                if (ShouldShowFaceResponseText())
                    SetRemiWorldResponseText($"请求失败：{error}");
                OnRemiFaceReplyFinished();
            },
            revealCallback);
    }

    /// <summary>Remi 一句面对面回复结束：重新开始闲置计时。</summary>
    private void OnRemiFaceReplyFinished()
    {
        _awaitingRemiReply = false;
        RefreshSuggestedQuestionsForRhythm();
    }

    private static void ArchiveFacePlayerUtterance(string userMessage)
    {
        RemiDialogueArchive.RecordStatic(
            "player",
            userMessage,
            RemiDialogueArchiveSource.FreeChat,
            RemiInteractionChannel.FaceToFace);
    }

    private static void ArchiveFaceFreeChat(string userMessage, string remiResponse)
    {
        ArchiveFacePlayerUtterance(userMessage);
        RemiDialogueArchive.RecordStatic(
            "Remi",
            remiResponse,
            RemiDialogueArchiveSource.FreeChat,
            RemiInteractionChannel.FaceToFace);
    }

    private void SetRemiWorldResponseText(string text)
    {
        if (!ShouldShowFaceResponseText())
            return;
        if (RemiPromptText == null) return;
        RemiPromptText.richText = true;
        RemiPromptText.text = text;
        _remiResponseTextLayout?.RefreshLayout();
        _roleWorldUi?.SetResponseText(text);
    }

    private void ClearRemiWorldResponseText()
    {
        if (RemiPromptText != null)
            RemiPromptText.text = string.Empty;
        _remiResponseTextLayout?.RefreshLayout();
        _roleWorldUi?.SetResponseText(string.Empty);
    }

    private static bool ShouldShowFaceResponseText()
    {
        if (StoryMemoryRecapView.IsActive)
            return false;

        DialogueSequenceDirector director = DialogueSequenceDirector.Instance;
        if (director == null)
            return true;
        return DialogueSequenceDirector.ShowsResponseText(
            director.ResolveMode(RemiInteractionChannel.FaceToFace));
    }

    private void ApplyFacePresentationToWorldUi()
    {
        if (_roleWorldUi == null)
            return;
        _roleWorldUi.SetSuppressResponseVisual(!ShouldShowFaceResponseText());
    }

    private static RemiExpression MapExpressionFromString(string expression)
    {
        if (string.IsNullOrEmpty(expression)) return RemiExpression.Neutral;
        switch (expression.Trim().ToLowerInvariant())
        {
            case "happy":
            case "excited":
            case "cheerful":
                return RemiExpression.Happy;
            case "angry":
            case "mad":
                return RemiExpression.Angry;
            case "sad":
            case "upset":
            case "unhappy":
                return RemiExpression.Sad;
            case "surprise":
            case "surprised":
                return RemiExpression.Surprised;
            case "shy":
            case "embarrassed":
                return RemiExpression.Shy;
            case "neutral":
            default:
                return RemiExpression.Neutral;
        }
    }

    /// <summary>根据 AI 回复里的 expression 字段选表情。</summary>
    private RemiExpression MapExpression(string expression) => MapExpressionFromString(expression);

    // 重写ShowMe/HideMe，扩展显示/隐藏逻辑
    public override void ShowMe()
    {
        base.ShowMe();
        gameObject.SetActive(true); // 确保面板激活
        ApplyFacePresentationToWorldUi();
        _SetScriptedFlowInputLocked(false);
        BeginIdleTopicSession();
        ApplyExitButtonLabel();
    }

    public override void HideMe(UnityEngine.Events.UnityAction callBack)
    {
        StopIdleTopicSession();
        _suggestedTopicsUnlocked = false;
        ApplySuggestedTopicsVisibility();
        base.HideMe(callBack);
    }

    private void OnEnable()
    {
        // 从历史面板切回时只 SetActive，需续上闲置监听
        if (!isShow || _idleTopicOfferedThisSession)
            return;
        if (_idleTopicWatchCo == null && _idleProposeCo == null)
            _idleTopicWatchCo = StartCoroutine(CoWatchIdleTopic());
    }

    private void OnDisable()
    {
        if (_idleTopicWatchCo != null)
        {
            StopCoroutine(_idleTopicWatchCo);
            _idleTopicWatchCo = null;
        }
    }

    private void BeginIdleTopicSession()
    {
        StopIdleTopicSession();
        _idleTopicOfferedThisSession = false;
        _awaitingRemiReply = false;
        _suggestedTopicsUnlocked = false;
        RefreshSuggestedQuestionsForRhythm();
        // Day1 窗口不解锁芯片；其它场景等闲置后再显示
        SetSuggestedTopicsVisible(false);
        _idleTopicWatchCo = StartCoroutine(CoWatchIdleTopic());
    }

    private void StopIdleTopicSession()
    {
        if (_idleTopicWatchCo != null)
        {
            StopCoroutine(_idleTopicWatchCo);
            _idleTopicWatchCo = null;
        }

        if (_idleProposeCo != null)
        {
            StopCoroutine(_idleProposeCo);
            _idleProposeCo = null;
        }

        _awaitingRemiReply = false;
    }

    private static bool IsDay1CommissionWindowOpen()
    {
        RemiBookQuestFlow flow = RemiBookQuestFlow.Instance;
        return flow != null && flow.IsDay1CommissionWindowOpen;
    }

    private static bool IsDay2CoPresenceWindowOpen()
    {
        RemiLibraryDay2CoPresenceFlow flow = RemiLibraryDay2CoPresenceFlow.Instance;
        return flow != null && flow.IsDay2CommissionWindowOpen;
    }

    private IEnumerator CoWatchIdleTopic()
    {
        float elapsed = 0f;
        float threshold = Mathf.Max(1f, idleTopicSeconds);

        while (isShow && !_idleTopicOfferedThisSession)
        {
            if (_awaitingRemiReply || _scriptedFlowInputLocked)
            {
                elapsed = 0f;
                yield return null;
                continue;
            }

            if (RemiDemoSpineDirector.Instance != null &&
                (RemiDemoSpineDirector.Instance.IsDay3DeviationPendingConfirm ||
                 RemiDemoSpineDirector.Instance.IsPendingDay3ApartmentTravel))
            {
                elapsed = 0f;
                yield return null;
                continue;
            }

            RemiBookQuestFlow quest = RemiBookQuestFlow.Instance;
            if (quest != null && quest.IsQuestFeatureEnabled &&
                quest.State != RemiBookQuestFlow.QuestState.WindowOpen)
            {
                yield break;
            }

            RemiLibraryDay2CoPresenceFlow day2 = RemiLibraryDay2CoPresenceFlow.Instance;
            if (day2 != null && day2.IsQuestFeatureEnabled &&
                day2.State != RemiLibraryDay2CoPresenceFlow.FlowState.WindowOpen)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= threshold)
            {
                _idleProposeCo = StartCoroutine(CoProposeThenUnlockTopics());
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator CoProposeThenUnlockTopics()
    {
        if (_idleTopicOfferedThisSession)
            yield break;

        // Day1 Window：对话内闲置不走时间保底（进对话后只看闲聊小轮）
        // Day2 Window：共现闲聊在触发之后，Window 本身无闲聊轮数保底
        if (IsDay1CommissionWindowOpen() || IsDay2CoPresenceWindowOpen())
        {
            _idleTopicOfferedThisSession = true;
            SetSuggestedTopicsVisible(false);
            _idleProposeCo = null;
            _idleTopicWatchCo = null;
            yield break;
        }

        _idleTopicOfferedThisSession = true;
        _awaitingRemiReply = true;

        RemiFaceIdleTopicSet set = ResolveIdleTopicSet();
        string fallbackPropose = set != null && !string.IsNullOrWhiteSpace(set.proposeLine)
            ? set.proposeLine.Trim()
            : "……你要是一时不知道说什么，我们可以从这些里挑一件聊。";

        bool usedSendSystem = false;
        if (useSendSystemForIdlePropose)
        {
            if (promptedAgent == null)
                promptedAgent = FindObjectOfType<PromptedDialogueAgent>();

            if (promptedAgent != null)
            {
                usedSendSystem = true;
                if (remi != null)
                    remi.animator.SetBool("Thinking", true);
                if (ShouldShowFaceResponseText())
                    SetRemiWorldResponseText("嗯...");

                SceneTravelLocation scene = SceneTravelCatalog.ResolveFromActiveScene();
                RemiSendSystemContentManager.EnsureExists();
                RemiSendSystemContentManager content = RemiSendSystemContentManager.Instance;
                RemiSendSystemContentManager.Entry propose = content != null
                    ? content.GetFaceIdleProposeEntry(scene)
                    : default;

                string context = content != null && !string.IsNullOrWhiteSpace(propose.initiatorContext)
                    ? propose.initiatorContext
                    : (set != null
                        ? set.ResolveSendSystemContext()
                        : RemiFaceIdleTopicSet.BuildDefaultSendSystemContext(
                            fallbackPropose, string.Empty, string.Empty));

                System.Action<string> reveal = ShouldShowFaceResponseText()
                    ? (System.Action<string>)SetRemiWorldResponseText
                    : null;

                bool done = false;
                bool ok = false;
                yield return promptedAgent.SendSystem(
                    context,
                    (text, expr) =>
                    {
                        ok = true;
                        if (remi != null)
                        {
                            remi.animator.SetBool("Thinking", false);
                            if (!string.IsNullOrWhiteSpace(expr))
                                remi.PlayExpression(MapExpressionFromString(expr));
                        }

                        OnScriptedUtteranceComplete(text, expr);
                        done = true;
                    },
                    err =>
                    {
                        Debug.LogWarning($"[DialoguePanel] 闲置提议 SendSystem 失败，回退固定句: {err}");
                        if (remi != null)
                            remi.animator.SetBool("Thinking", false);
                        done = true;
                    },
                    reveal);

                while (!done)
                    yield return null;

                if (!ok)
                    ApplyRemiLineGlobally(fallbackPropose);
            }
        }

        if (!usedSendSystem)
            ApplyRemiLineGlobally(fallbackPropose);

        // 稍顿再出话题，让提议先被看见
        yield return new WaitForSecondsRealtime(0.35f);

        RefreshSuggestedQuestionsForRhythm();
        SetSuggestedTopicsVisible(true);
        _awaitingRemiReply = false;
        _idleProposeCo = null;
        _idleTopicWatchCo = null;
    }

    private void ApplyExitButtonLabel()
    {
        if (_backButtonLabel == null) return;

        RemiBookQuestFlow flow = RemiBookQuestFlow.Instance;
        if (flow != null && flow.IsAwaitingGoodbyeConfirm)
        {
            string confirm = flow.GoodbyeConfirmButtonLabel;
            _backButtonLabel.text = string.IsNullOrWhiteSpace(confirm) ? "确认" : confirm.Trim();
            return;
        }

        RemiLibraryDay2CoPresenceFlow day2 = RemiLibraryDay2CoPresenceFlow.Instance;
        if (day2 != null && day2.IsAwaitingStayConfirm)
        {
            string confirm = day2.StayConfirmButtonLabel;
            _backButtonLabel.text = string.IsNullOrWhiteSpace(confirm) ? "确认" : confirm.Trim();
            return;
        }

        if (flow != null && flow.ShouldShowGoodbyeExitUx())
        {
            string goodbye = flow.GoodbyeButtonLabel;
            _backButtonLabel.text = string.IsNullOrWhiteSpace(goodbye) ? "回头见" : goodbye.Trim();
            return;
        }

        if (day2 != null && day2.ShouldShowStudyExitUx())
        {
            string study = day2.StudyButtonLabel;
            _backButtonLabel.text = string.IsNullOrWhiteSpace(study) ? "开始自习" : study.Trim();
            return;
        }

        if (day2 != null && day2.ShouldShowStayExitUx())
        {
            string stay = day2.StayButtonLabel;
            _backButtonLabel.text = string.IsNullOrWhiteSpace(stay) ? "留下来" : stay.Trim();
            return;
        }

        _backButtonLabel.text = "回头见";
    }
}