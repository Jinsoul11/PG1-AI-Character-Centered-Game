using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class SocialChatRecord
{
    public string role;
    public string content;
    /// <summary>叙事时间（权威）。旧档无此字段时 storyDay=0。</summary>
    public int worldStoryDay;
    public int worldPhase;
    public int worldBeat;
    /// <summary>旧档 UTC；仅作迁移兜底。</summary>
    public long utcTicks;
}

/// <summary>
/// 手机 App（ESC）：联系（线上聊天）· 动态（朋友圈）· 设置。面对面仍用 <see cref="DialoguePanel"/>。
/// </summary>
public class PhoneAppPanel : BasePanel
{
    public enum PhoneAppTab
    {
        Contact = 0,
        Moments = 1,
        Settings = 2,
    }

    public const string SaveKey = "SocialConversation";

    public static bool IsOpen =>
        UiManager.Instance.GetPanel<PhoneAppPanel>() is { gameObject: { activeInHierarchy: true } };

    [Header("布局（须在 Resources/UI/PhoneAppPanel 预制体上手动绑定）")]
    [SerializeField] private ScrollRect chatScrollRect;
    [SerializeField] private RectTransform messageContent;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private Button closeButton;
    [Tooltip("可选：不绑则运行时用 SocialChatUiFactory 生成气泡行。")]
    [SerializeField] private RectTransform messageRowPrefab;
    [Tooltip("可选：不绑则运行时生成时间条。")]
    [SerializeField] private RectTransform timeDividerPrefab;

    [Header("头像")]
    [SerializeField] private Sprite remiAvatarSprite;
    [SerializeField] private Sprite playerAvatarSprite;

    [Header("样式")]
    [SerializeField] private Color panelBackground = new Color(0.12f, 0.12f, 0.12f, 0.98f);
    [SerializeField] private Color remiBubbleColor = new Color(0.22f, 0.22f, 0.24f, 1f);
    [SerializeField] private Color playerBubbleColor = new Color(0.18f, 0.45f, 0.85f, 1f);
    [SerializeField] private Color timeTextColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private float bubbleMaxWidth = 420f;
    [SerializeField] private float avatarSize = 40f;

    [Header("时间条")]
    [Tooltip("叙事 beat 间隔达到该值时插入时间条；跨天/跨时段必定插入。")]
    [SerializeField] private int timeDividerMinBeatGap = 3;

    [Header("回复节奏")]
    [SerializeField] private bool usePresenceSocialDelay = true;
    [SerializeField] private TMP_Text typingIndicator;

    [Header("动态（朋友圈）")]
    [SerializeField] private SocialMomentsView momentsView;
    [SerializeField] private PhoneSettingsView settingsView;
    [SerializeField] private RectTransform chatPageRoot;
    [SerializeField] private Button tabContactButton;
    [SerializeField] private Button tabMomentsButton;
    [SerializeField] private Button tabSettingsButton;
    [SerializeField] private TMP_Text socialStatusBanner;
    [SerializeField] private RectTransform storyChipContainer;
    [SerializeField] private RectTransform chatFooter;

    private const float StoryChipBarHeight = 56f;
    private const float ChatFooterBaseHeight = 120f;

    private readonly List<Button> _spawnedChipButtons = new List<Button>();

    private const int MaxHistoryCount = 80;
    private PhoneAppTab _currentTab = PhoneAppTab.Contact;
    private string _pendingCommentPostId;

    private readonly List<SocialChatRecord> _records = new List<SocialChatRecord>();
    private readonly List<RectTransform> _spawnedRows = new List<RectTransform>();
    private RemiWorldTime? _lastMessageWorldTime;
    private bool _showTimeOnNextMessage;

    private Remi _remi;
    private PromptedDialogueAgent _promptedAgent;
    private bool _waitingReply;
    private bool _initialized;

    /// <summary>保证 Init/LoadHistory 已执行（ShowPanel 后 Start 之前也可调用）。</summary>
    public void EnsureInitialized()
    {
        if (_initialized)
            return;
        Init();
    }

    /// <summary>写入手机聊天存档（无需面板实例；Day2 邀请等剧情消息用）。</summary>
    public static bool TryPersistRemiMessage(string content) =>
        TryPersistChatMessage("Remi", content, dedupeSameRoleContent: true);

    /// <summary>写入玩家手机消息（Chip / 代点答应等；面板未打开也能落盘）。</summary>
    public static bool TryPersistPlayerMessage(string content) =>
        TryPersistChatMessage("user", content, dedupeSameRoleContent: false);

    /// <summary>
    /// 以磁盘为真源追加一条社交聊天；避免面板内存列表过期后 Save 覆盖掉已有消息。
    /// </summary>
    public static bool TryPersistChatMessage(string role, string content, bool dedupeSameRoleContent)
    {
        if (string.IsNullOrWhiteSpace(content) || JsonMgr.Instance == null)
            return false;

        content = content.Trim();
        bool isPlayer = IsPlayerRole(role);
        bool isSystem = string.Equals(role, "system", StringComparison.OrdinalIgnoreCase);
        string normalizedRole = isPlayer ? "user" : (isSystem ? "system" : "Remi");

        List<SocialChatRecord> records = JsonMgr.Instance.LoadData<List<SocialChatRecord>>(SaveKey);
        if (records == null)
            records = new List<SocialChatRecord>();

        if (dedupeSameRoleContent)
        {
            for (int i = 0; i < records.Count; i++)
            {
                SocialChatRecord existing = records[i];
                if (existing == null)
                    continue;
                if (!string.Equals(existing.role, normalizedRole, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(existing.content?.Trim(), content, StringComparison.Ordinal))
                    return true;
            }
        }

        RemiWorldTime now = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.CaptureWorldTime()
            : RemiWorldTime.BeforeStory;

        var rec = new SocialChatRecord { role = normalizedRole, content = content };
        WriteWorldTimeToRecord(rec, now);
        records.Add(rec);
        while (records.Count > MaxHistoryCount)
            records.RemoveAt(0);

        JsonMgr.Instance.SaveData(records, SaveKey);

        if (!isSystem)
        {
            RemiDialogueArchiveSource archiveSource = isPlayer
                ? RemiDialogueArchiveSource.FreeChat
                : (dedupeSameRoleContent
                    ? RemiDialogueArchiveSource.Scripted
                    : RemiDialogueArchiveSource.FreeChat);
            RemiDialogueArchive.RecordStatic(
                isPlayer ? "player" : "Remi",
                content,
                archiveSource,
                RemiInteractionChannel.Social);
        }

        return true;
    }

    /// <summary>从存档重建聊天列表 UI。</summary>
    public void ReloadChatFromStorage()
    {
        EnsureInitialized();
        LoadHistory();
        ScrollToBottom();
    }

    public static void Open() => Open(PhoneAppTab.Contact);

    public static void Open(PhoneAppTab tab)
    {
        PhoneAppAccess.EnsureLoaded();
        if (!PhoneAppAccess.IsUnlocked)
            return;

        UiManager.EnsureCanvasActive();
        RemiPresenceService.Instance?.SetInteractionChannel(RemiInteractionChannel.Social);
        PhoneAppPanel panel = UiManager.Instance.GetPanel<PhoneAppPanel>();
        if (panel == null)
            panel = UiManager.Instance.ShowPanel<PhoneAppPanel>();
        else
        {
            panel.gameObject.SetActive(true);
            panel.ShowMe();
        }

        panel?.EnsureInitialized();

        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector.Instance?.TryFlushPendingDay2Invite();
        RemiDemoSpineDirector.Instance?.TryFlushPendingDay3Nudge();
        RemiDemoSpineDirector.Instance?.TryFlushPendingDay3Offer();

        panel?.ReloadChatFromStorage();
        panel?.ShowTab(tab);
        panel?.RefreshStoryChips();
    }

    public static void OpenMoments() => Open(PhoneAppTab.Moments);

    public static void Toggle()
    {
        PhoneAppAccess.EnsureLoaded();
        if (!PhoneAppAccess.IsUnlocked)
            return;

        PhoneAppPanel panel = UiManager.Instance.GetPanel<PhoneAppPanel>();
        if (panel != null && panel.gameObject.activeInHierarchy)
            panel.ClosePhone();
        else
            Open();
    }

    public static void ClosePanel()
    {
        PhoneAppPanel panel = UiManager.Instance.GetPanel<PhoneAppPanel>();
        panel?.ClosePhone();
    }

    public void ClosePhone()
    {
        RemiPresenceService.Instance?.NotifyRemoteBeatInterludeEnded();
        if (DeepSeekDialogueManager.Instance != null)
            DeepSeekDialogueManager.Instance.ClearSocialMessageHistory();
        RestorePlayerInput();
        gameObject.SetActive(false);
        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector.Instance?.NotifyPhoneClosed();
        RemiDemoDay2ClassroomGuide.TryPromptGoToDoorAfterPhone();
    }

    public void CloseForExternalPanel()
    {
        RemiPresenceService.Instance?.NotifyRemoteBeatInterludeEnded();
        if (DeepSeekDialogueManager.Instance != null)
            DeepSeekDialogueManager.Instance.ClearSocialMessageHistory();
        RestorePlayerInput();
        gameObject.SetActive(false);
        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector.Instance?.NotifyPhoneClosed();
    }

    private PlayerController _phonePlayer;
    private CursorLockMode _savedLock;
    private bool _savedCursorVisible;
    private bool _phoneInputLocked;

    private void LockPlayerForPhone()
    {
        if (_phoneInputLocked) return;
        _phonePlayer = FindObjectOfType<PlayerController>();
        _phonePlayer?.SetMoveLock(true);
        _phonePlayer?.SetLookLock(true);
        _savedLock = Cursor.lockState;
        _savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _phoneInputLocked = true;
    }

    private void RestorePlayerInput()
    {
        if (!_phoneInputLocked) return;
        _phonePlayer?.SetMoveLock(false);
        _phonePlayer?.SetLookLock(false);
        Cursor.lockState = _savedLock;
        Cursor.visible = _savedCursorVisible;
        _phoneInputLocked = false;
        _phonePlayer = null;
    }

    protected override void Awake()
    {
        base.Awake();
        WarnIfShellMissing();
    }

    public override void Init()
    {
        if (_initialized)
            return;

        WarnIfShellMissing();

        GameObject remiObj = GameObject.Find("Remi");
        if (remiObj != null)
            _remi = remiObj.GetComponent<Remi>();
        _promptedAgent = FindObjectOfType<PromptedDialogueAgent>();

        if (sendButton != null)
        {
            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener(SendUserMessage);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        WireTabButtons();
        if (inputField != null)
        {
            inputField.onSubmit.RemoveAllListeners();
            inputField.onSubmit.AddListener(_ => SendUserMessage());
        }

        if (momentsView != null)
            momentsView.BindHost(this);

        LoadHistory();
        _showTimeOnNextMessage = true;
        ShowTab(PhoneAppTab.Contact);
        _initialized = true;
    }

    private void WarnIfShellMissing()
    {
        if (chatScrollRect == null || messageContent == null || chatPageRoot == null)
        {
            Debug.LogError(
                "[PhoneAppPanel] 壳层未绑定：请在 Resources/UI/PhoneAppPanel 预制体上手动绑定 Chat Scroll / Message Content / Chat Page Root 等引用。",
                this);
        }
    }

    public override void ShowMe()
    {
        base.ShowMe();
        gameObject.SetActive(true);
        LockPlayerForPhone();
        RemiPresenceService.Instance?.SetInteractionChannel(RemiInteractionChannel.Social);
        EnsureMomentsService();
        RemiMomentsService.Instance?.SyncForCurrentStage();
        _showTimeOnNextMessage = true;
        ShowTab(_currentTab);
        RefreshSocialStatusBanner();
        RefreshStoryChips();
        if (_currentTab == PhoneAppTab.Contact)
        {
            ScrollToBottom();
            RemiDemoSpineDirector.EnsureExists();
            if (RemiInteractionChannelPolicy.CanPlayerTypeInSocialChannel(RemiPresenceService.Instance))
                inputField?.ActivateInputField();
        }
        else if (_currentTab == PhoneAppTab.Moments)
            momentsView?.Refresh();
        else
            settingsView?.Refresh();
    }

    private static void EnsureMomentsService()
    {
        if (RemiMomentsService.Instance != null) return;
        var go = new GameObject("RemiMomentsService");
        go.AddComponent<RemiMomentsService>();
    }

    private void WireTabButtons()
    {
        if (tabContactButton != null)
        {
            tabContactButton.onClick.RemoveAllListeners();
            tabContactButton.onClick.AddListener(() => ShowTab(PhoneAppTab.Contact));
        }

        if (tabMomentsButton != null)
        {
            tabMomentsButton.onClick.RemoveAllListeners();
            tabMomentsButton.onClick.AddListener(() => ShowTab(PhoneAppTab.Moments));
        }

        if (tabSettingsButton != null)
        {
            tabSettingsButton.onClick.RemoveAllListeners();
            tabSettingsButton.onClick.AddListener(() => ShowTab(PhoneAppTab.Settings));
        }
    }

    public void ShowTab(PhoneAppTab tab)
    {
        _currentTab = tab;
        if (chatPageRoot != null)
            chatPageRoot.gameObject.SetActive(tab == PhoneAppTab.Contact);
        momentsView?.SetVisible(tab == PhoneAppTab.Moments);
        settingsView?.SetVisible(tab == PhoneAppTab.Settings);
        RefreshTabButtonStyles();
        RefreshSocialStatusBanner();
        RefreshStoryChips();
        if (tab == PhoneAppTab.Moments)
            momentsView?.Refresh();
        else if (tab == PhoneAppTab.Settings)
            settingsView?.Refresh();
        else if (tab == PhoneAppTab.Contact)
        {
            if (RemiInteractionChannelPolicy.CanPlayerTypeInSocialChannel(RemiPresenceService.Instance))
                inputField?.ActivateInputField();
        }
    }

    private void RefreshSocialStatusBanner()
    {
        if (socialStatusBanner == null) return;
        bool showOnContact = _currentTab == PhoneAppTab.Contact;
        RemiPresenceService p = RemiPresenceService.Instance;
        string banner = showOnContact
            ? RemiPresenceAvailability.GetSocialStatusBanner(p)
            : string.Empty;

        socialStatusBanner.text = banner ?? string.Empty;
        socialStatusBanner.gameObject.SetActive(!string.IsNullOrEmpty(banner));
        RefreshSocialInputGate();
    }

    public void RefreshStoryChips()
    {
        if (storyChipContainer == null)
            return;

        ClearChipButtons();
        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector director = RemiDemoSpineDirector.Instance;

        bool showChips = _currentTab == PhoneAppTab.Contact &&
                         director != null &&
                         director.HasPendingStoryReply();

        if (!showChips)
        {
            storyChipContainer.gameObject.SetActive(false);
            AdjustChatLayoutForChips(false);
            RefreshSocialInputGate();
            return;
        }

        storyChipContainer.gameObject.SetActive(true);
        foreach (RemiSpineStoryChipOption chip in director.GetPendingStoryChips())
            SpawnChipButton(chip);

        AdjustChatLayoutForChips(true);
        RefreshSocialInputGate();
    }

    private void SpawnChipButton(RemiSpineStoryChipOption chip)
    {
        if (storyChipContainer == null || string.IsNullOrWhiteSpace(chip.Label))
            return;

        string display = string.IsNullOrWhiteSpace(chip.DisplayLabel) ? chip.Label : chip.DisplayLabel;
        Button btn = SocialChatUiFactory.CreateStoryChipButton(storyChipContainer, display, playerBubbleColor);
        RemiSpineStoryChipId chipId = chip.Id;
        btn.onClick.AddListener(() => OnStoryChipClicked(chipId));
        _spawnedChipButtons.Add(btn);
    }

    private void OnStoryChipClicked(RemiSpineStoryChipId chipId)
    {
        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector.Instance?.OnStoryChipSelected(chipId);
        ReloadChatFromStorage();
        RefreshStoryChips();
    }

    private void ClearChipButtons()
    {
        foreach (Button btn in _spawnedChipButtons)
        {
            if (btn != null)
                Destroy(btn.gameObject);
        }

        _spawnedChipButtons.Clear();
    }

    private void AdjustChatLayoutForChips(bool chipsVisible)
    {
        float footerHeight = ChatFooterBaseHeight + (chipsVisible ? StoryChipBarHeight : 0f);
        if (chatFooter != null)
            chatFooter.sizeDelta = new Vector2(chatFooter.sizeDelta.x, footerHeight);

        if (chatScrollRect != null)
        {
            RectTransform scrollRt = chatScrollRect.transform as RectTransform;
            if (scrollRt != null)
                SocialChatUiFactory.StretchFill(scrollRt, 0, footerHeight, 0, 0);
        }

        // Chip 与自由输入：PendingConfirm（等「那走吧」）时关闭打字，只留确认 Chip
        RemiDemoSpineDirector.EnsureExists();
        bool pendingConfirm = RemiDemoSpineDirector.Instance != null &&
                              RemiDemoSpineDirector.Instance.IsDay3DeviationPendingConfirm;

        if (inputField != null)
            inputField.gameObject.SetActive(!pendingConfirm);
        if (sendButton != null)
            sendButton.gameObject.SetActive(!pendingConfirm);

        if (storyChipContainer != null)
        {
            if (chipsVisible)
            {
                storyChipContainer.gameObject.SetActive(true);
                RectTransform chipRt = storyChipContainer;
                chipRt.anchorMin = new Vector2(0f, 1f);
                chipRt.anchorMax = new Vector2(1f, 1f);
                chipRt.pivot = new Vector2(0.5f, 1f);
                chipRt.sizeDelta = new Vector2(chipRt.sizeDelta.x, StoryChipBarHeight - 8f);
                chipRt.anchoredPosition = new Vector2(0f, -4f);
                storyChipContainer.SetAsLastSibling();
            }

            if (inputField != null && !pendingConfirm)
            {
                RectTransform inputRt = inputField.transform as RectTransform;
                if (inputRt != null)
                {
                    float topInset = chipsVisible ? StoryChipBarHeight : 0f;
                    inputRt.offsetMax = new Vector2(inputRt.offsetMax.x, -topInset);
                }
            }
        }
    }

    private void RefreshSocialInputGate()
    {
        if (_currentTab != PhoneAppTab.Contact)
            return;

        RemiPresenceService presence = RemiPresenceService.Instance;
        RemiDemoSpineDirector.EnsureExists();
        bool spineBusy = RemiDemoSpineDirector.Instance != null &&
                         RemiDemoSpineDirector.Instance.IsSpineSequenceRunning;
        bool pendingConfirm = RemiDemoSpineDirector.Instance != null &&
                              RemiDemoSpineDirector.Instance.IsDay3DeviationPendingConfirm;
        bool day2InviteLocked = RemiDemoSpineDirector.Instance != null &&
                                RemiDemoSpineDirector.Instance.IsDay2InvitePhoneInputLocked();
        // Open 窗内可与 Chip 并存打字；PendingConfirm 只许确认 Chip；Day2 邀约一次性回复后锁输入
        bool canType = !_waitingReply &&
                       !spineBusy &&
                       !pendingConfirm &&
                       !day2InviteLocked &&
                       RemiInteractionChannelPolicy.CanPlayerTypeInSocialChannel(presence);

        if (inputField != null)
        {
            inputField.interactable = canType;
            if (!canType)
                inputField.DeactivateInputField();
        }

        SetSendInteractable(!_waitingReply && canType);
    }

    private void RefreshTabButtonStyles()
    {
        Color active = new Color(0.18f, 0.45f, 0.85f, 1f);
        Color idle = new Color(0.45f, 0.45f, 0.45f, 1f);
        SetTabLabelColor(tabContactButton, _currentTab == PhoneAppTab.Contact ? active : idle);
        SetTabLabelColor(tabMomentsButton, _currentTab == PhoneAppTab.Moments ? active : idle);
        SetTabLabelColor(tabSettingsButton, _currentTab == PhoneAppTab.Settings ? active : idle);
    }

    private static void SetTabLabelColor(Button btn, Color c)
    {
        if (btn == null) return;
        TMP_Text t = btn.GetComponentInChildren<TMP_Text>(true);
        if (t != null) t.color = c;
    }

    /// <summary>从动态页点「评论」：切到聊天并记住要回复哪条动态。</summary>
    public void BeginCommentOnMoment(string postId)
    {
        _pendingCommentPostId = postId;
        ShowTab(PhoneAppTab.Contact);
        if (inputField != null)
            inputField.ActivateInputField();
    }

    private void OnCloseClicked() => ClosePhone();

    private void SendUserMessage()
    {
        if (inputField == null || _waitingReply) return;
        RemiDemoSpineDirector.EnsureExists();
        if (RemiDemoSpineDirector.Instance != null &&
            RemiDemoSpineDirector.Instance.IsDay3DeviationPendingConfirm)
        {
            RefreshStoryChips();
            return;
        }

        // Day2 邀约：最多一条玩家短信，不触发 Remi 回信，随后锁输入
        if (RemiDemoSpineDirector.Instance != null &&
            RemiDemoSpineDirector.Instance.IsAwaitingDay2LibraryVisit())
        {
            if (!RemiDemoSpineDirector.Instance.CanSendDay2InviteOneShotReply())
            {
                RefreshSocialStatusBanner();
                return;
            }

            string day2Text = inputField.text.Trim();
            if (string.IsNullOrEmpty(day2Text))
                return;

            inputField.text = string.Empty;
            _pendingCommentPostId = null;
            AppendMessage("user", day2Text, save: true);
            RemiDemoSpineDirector.Instance.MarkDay2InviteOneShotReplySent();
            RefreshSocialStatusBanner();
            ScrollToBottom();
            return;
        }

        if (!RemiInteractionChannelPolicy.CanPlayerTypeInSocialChannel(RemiPresenceService.Instance))
        {
            RefreshSocialStatusBanner();
            return;
        }

        string text = inputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        inputField.text = string.Empty;
        AppendMessage("user", text, save: true);

        string momentPostId = _pendingCommentPostId;
        string momentContext = null;
        if (!string.IsNullOrEmpty(momentPostId) && RemiMomentsService.Instance != null)
        {
            RemiMomentsService.Instance.TryAddComment(momentPostId, text);
            if (RemiMomentsService.Instance.TryGetPostDefinition(momentPostId, out RemiMomentsPostDefinition def))
                momentContext = def.commentReplyContext;
            _pendingCommentPostId = null;
        }

        if (PromptContextManager.Instance != null)
        {
            string facts = string.IsNullOrEmpty(momentContext) ? string.Empty : momentContext.Trim();
            PromptContextManager.Instance.SetInitiator(PromptContextManager.InitiatorRole.Player, facts);
        }

        RemiStageExpressionContext stageContext = string.IsNullOrEmpty(momentPostId)
            ? RemiStageExpressionContext.SocialChat
            : RemiStageExpressionContext.SocialMomentComment;

        StartCoroutine(SendToRemiCoroutine(text, stageContext));
    }

    private IEnumerator SendToRemiCoroutine(string userMessage, RemiStageExpressionContext stageContext)
    {
        _waitingReply = true;
        SetSendInteractable(false);
        RefreshSocialStatusBanner();

        RemiPresenceService presence = RemiPresenceService.Instance;
        if (RemiPresenceAvailability.ShouldBlockSocialLlm(presence))
        {
            SetTypingVisible(true);
            if (typingIndicator != null)
                typingIndicator.text = RemiPresenceAvailability.GetSocialStatusBanner(presence);
            int delay = presence != null ? presence.GetSocialReplyDelaySeconds() : 3;
            yield return new WaitForSeconds(Mathf.Min(delay, 5f));
            SetTypingVisible(false);
            AppendMessage("system", RemiPresenceAvailability.GetSocialOfflineSystemLine(presence), save: true);
            _waitingReply = false;
            RefreshSocialInputGate();
            yield break;
        }

        SetTypingVisible(true);

        if (usePresenceSocialDelay && presence != null)
        {
            int delay = presence.GetSocialReplyDelaySeconds();
            if (delay > 0)
                yield return new WaitForSeconds(delay);
        }

        if (_promptedAgent == null)
            _promptedAgent = FindObjectOfType<PromptedDialogueAgent>();

        if (_promptedAgent == null)
        {
            SetTypingVisible(false);
            AppendMessage("Remi", "（未找到对话代理）", save: true);
            _waitingReply = false;
            RefreshSocialInputGate();
            yield break;
        }

        // Day3 偏离窗口：Detect / 保底提案已关；进度靠固定 Chip「今晚方便来宿舍聊聊吗？」
        // 普通闲聊仍可走 Voice，便于 Ending 碎片段。

        bool done = false;
        string errorMsg = null;

        yield return _promptedAgent.SendPlayer(
            userMessage,
            (responseText, expression) =>
            {
                AppendMessage("Remi", responseText, save: true);
                if (_remi != null && !string.IsNullOrEmpty(expression))
                    _remi.PlayExpression(MapExpression(expression));
                done = true;
            },
            err =>
            {
                errorMsg = err;
                done = true;
            },
            stageContext: stageContext,
            preserveInitiatorContext: true,
            voiceOnly: true);

        if (!done && errorMsg != null)
            AppendMessage("Remi", $"请求失败：{errorMsg}", save: true);

        SetTypingVisible(false);
        _waitingReply = false;
        SetSendInteractable(true);
        RefreshSocialInputGate();
        ScrollToBottom();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoadedRefreshSocialGate;

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoadedRefreshSocialGate;

    private void OnSceneLoadedRefreshSocialGate(Scene scene, LoadSceneMode mode)
    {
        if (gameObject.activeInHierarchy)
        {
            RefreshSocialInputGate();
            RefreshStoryChips();
        }
    }

    public void AppendMessage(string role, string content, bool save, string displayOverride = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        content = content.Trim();
        string display = string.IsNullOrWhiteSpace(displayOverride) ? content : displayOverride.Trim();
        bool isPlayer = IsPlayerRole(role);
        bool isSystem = string.Equals(role, "system", StringComparison.OrdinalIgnoreCase);
        RemiWorldTime now = CaptureNowWorldTime();

        if (save)
        {
            // 先落盘（磁盘合并），再同步内存，避免用过期 _records 覆盖历史
            // 自由聊不去重；剧情短信走 TryPersistRemiMessage(dedupe:true)
            TryPersistChatMessage(role, content, dedupeSameRoleContent: false);
            SyncRecordsFromDisk();
        }

        if (ShouldShowTimeDivider(now))
            AddTimeDivider(now);

        if (isSystem)
            AddSystemLine(display);
        else
            SpawnMessageRow(isPlayer, display);
        _lastMessageWorldTime = now;
        _showTimeOnNextMessage = false;

        ScrollToBottom();
    }

    private void SyncRecordsFromDisk()
    {
        _records.Clear();
        List<SocialChatRecord> loaded = JsonMgr.Instance != null
            ? JsonMgr.Instance.LoadData<List<SocialChatRecord>>(SaveKey)
            : null;
        if (loaded == null || loaded.Count == 0)
            return;
        if (loaded.Count > MaxHistoryCount)
            loaded = loaded.GetRange(loaded.Count - MaxHistoryCount, MaxHistoryCount);
        _records.AddRange(loaded);
    }

    private bool ShouldShowTimeDivider(RemiWorldTime now)
    {
        if (_showTimeOnNextMessage)
            return true;
        if (!_lastMessageWorldTime.HasValue)
            return true;
        return RemiWorldTimeFormat.ShouldShowDivider(_lastMessageWorldTime.Value, now, timeDividerMinBeatGap);
    }

    private void AddTimeDivider(RemiWorldTime time)
    {
        RectTransform row = CreateTimeDividerRow();
        if (row == null) return;
        _spawnedRows.Add(row);
        SocialChatTimeDivider divider = row.GetComponent<SocialChatTimeDivider>();
        divider?.SetTimeText(RemiWorldTimeFormat.FormatDivider(time));
    }

    private static RemiWorldTime CaptureNowWorldTime() =>
        RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.CaptureWorldTime()
            : RemiWorldTime.BeforeStory;

    private static RemiWorldTime RecordToWorldTime(SocialChatRecord rec)
    {
        if (rec == null || rec.worldStoryDay <= 0)
            return RemiWorldTime.BeforeStory;
        return new RemiWorldTime
        {
            storyDay = rec.worldStoryDay,
            phase = (RemiDayPhase)rec.worldPhase,
            beat = rec.worldBeat,
        };
    }

    private static void WriteWorldTimeToRecord(SocialChatRecord rec, RemiWorldTime wt)
    {
        rec.worldStoryDay = wt.storyDay;
        rec.worldPhase = (int)wt.phase;
        rec.worldBeat = wt.beat;
    }

    private void AddSystemLine(string content)
    {
        RectTransform row = CreateTimeDividerRow();
        if (row == null) return;
        _spawnedRows.Add(row);
        SocialChatTimeDivider divider = row.GetComponent<SocialChatTimeDivider>();
        divider?.SetTimeText(content);
    }

    private void SpawnMessageRow(bool isPlayer, string content)
    {
        RectTransform rowRt = InstantiateRowPrefab();
        if (rowRt == null) return;
        _spawnedRows.Add(rowRt);

        SocialChatMessageRow row = rowRt.GetComponent<SocialChatMessageRow>();
        if (row == null)
            row = rowRt.gameObject.AddComponent<SocialChatMessageRow>();

        row.Setup(isPlayer, content, remiAvatarSprite, playerAvatarSprite, remiBubbleColor, playerBubbleColor);
    }

    private RectTransform InstantiateRowPrefab()
    {
        if (messageRowPrefab != null && messageContent != null)
            return Instantiate(messageRowPrefab, messageContent);

        if (messageContent == null) return null;
        return SocialChatUiFactory.CreateMessageRow(
            messageContent,
            bubbleMaxWidth,
            avatarSize,
            remiBubbleColor,
            playerBubbleColor,
            remiAvatarSprite,
            playerAvatarSprite);
    }

    private RectTransform CreateTimeDividerRow()
    {
        if (timeDividerPrefab != null && messageContent != null)
            return Instantiate(timeDividerPrefab, messageContent);

        if (messageContent == null) return null;
        return SocialChatUiFactory.CreateTimeDivider(messageContent, timeTextColor);
    }

    public void LoadHistory()
    {
        ClearSpawnedUi();
        _records.Clear();
        _lastMessageWorldTime = null;

        List<SocialChatRecord> loaded = JsonMgr.Instance.LoadData<List<SocialChatRecord>>(SaveKey);
        if (loaded == null || loaded.Count == 0)
            return;

        if (loaded.Count > MaxHistoryCount)
            loaded = loaded.GetRange(loaded.Count - MaxHistoryCount, MaxHistoryCount);

        _records.AddRange(loaded);
        RemiWorldTime? prevTime = null;

        foreach (SocialChatRecord rec in _records)
        {
            if (rec == null || string.IsNullOrEmpty(rec.content)) continue;
            bool isPlayer = IsPlayerRole(rec.role);
            RemiWorldTime msgTime = RecordToWorldTime(rec);

            bool showTime = !prevTime.HasValue
                || RemiWorldTimeFormat.ShouldShowDivider(prevTime.Value, msgTime, timeDividerMinBeatGap);
            if (showTime)
                AddTimeDivider(msgTime);

            string display = RemiDemoSpineStoryChips.TryFormatPersistedPhoneLine(rec.role, rec.content);
            SpawnMessageRow(isPlayer, display);
            prevTime = msgTime;
            _lastMessageWorldTime = msgTime;
        }

        ScrollToBottom();
    }

    public void ClearHistory()
    {
        ClearSpawnedUi();
        _records.Clear();
        _lastMessageWorldTime = null;
        JsonMgr.Instance.DeleteData(SaveKey);
    }

    /// <summary>日起点读档等：清空手机社交会话存档（无需面板实例）。</summary>
    public static void ClearPersistedChatHistory()
    {
        if (JsonMgr.Instance != null)
            JsonMgr.Instance.DeleteData(SaveKey);

        if (UiManager.Instance == null)
            return;

        PhoneAppPanel panel = UiManager.Instance.GetPanel<PhoneAppPanel>();
        if (panel == null)
            return;

        panel.EnsureInitialized();
        panel.ClearHistory();
    }

    /// <summary>日起点读档：只保留早于 <paramref name="exclusiveMinStoryDay"/> 的社交短信（如载入第三天时去掉当日邀约）。</summary>
    public static void KeepPersistedChatBeforeStoryDay(int exclusiveMinStoryDay)
    {
        if (JsonMgr.Instance == null)
            return;

        List<SocialChatRecord> records = JsonMgr.Instance.LoadData<List<SocialChatRecord>>(SaveKey);
        if (records == null)
            records = new List<SocialChatRecord>();

        records.RemoveAll(rec => rec != null && rec.worldStoryDay >= exclusiveMinStoryDay);
        JsonMgr.Instance.SaveData(records, SaveKey);

        if (UiManager.Instance == null)
            return;

        PhoneAppPanel panel = UiManager.Instance.GetPanel<PhoneAppPanel>();
        if (panel == null)
            return;

        panel.EnsureInitialized();
        panel.ReloadChatFromStorage();
    }

    private void ClearSpawnedUi()
    {
        foreach (RectTransform rt in _spawnedRows)
        {
            if (rt != null)
                Destroy(rt.gameObject);
        }
        _spawnedRows.Clear();
    }

    private void TrimRecordsForSave()
    {
        while (_records.Count >= MaxHistoryCount)
            _records.RemoveAt(0);
    }

    private void ScrollToBottom()
    {
        if (chatScrollRect == null) return;
        Canvas.ForceUpdateCanvases();
        chatScrollRect.verticalNormalizedPosition = 0f;
    }

    private void SetSendInteractable(bool on)
    {
        if (sendButton != null)
            sendButton.interactable = on;
    }

    private void SetTypingVisible(bool visible)
    {
        if (typingIndicator != null)
            typingIndicator.gameObject.SetActive(visible);
    }

    private static bool IsPlayerRole(string role) =>
        string.Equals(role, "user", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "player", StringComparison.OrdinalIgnoreCase);

    private static RemiExpression MapExpression(string expression)
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
            default:
                return RemiExpression.Neutral;
        }
    }

}

/// <summary>动态气泡行 / Story Chip / 朋友圈行辅助（不再生成整机壳）。</summary>
internal static class SocialChatUiFactory
{
    public static void StretchFill(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    public static Button CreateStoryChipButton(RectTransform parent, string label, Color buttonColor)
    {
        RectTransform btnRt = CreateChild(parent, "StoryChip");
        LayoutElement le = btnRt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 40f;

        Button btn = btnRt.gameObject.AddComponent<Button>();
        Image img = btnRt.gameObject.AddComponent<Image>();
        img.color = buttonColor;
        img.raycastTarget = true;

        TMP_Text tmp = CreateTmp(btnRt, "Label", 17, TextAlignmentOptions.Center);
        Stretch(tmp.rectTransform, 12, 6, -12, -6);
        tmp.richText = true;
        tmp.text = label;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return btn;
    }

    public static ScrollRect CreateScrollArea(RectTransform parent, out RectTransform content)
    {
        RectTransform scrollRt = CreateChild(parent, "Scroll");
        Stretch(scrollRt);
        ScrollRect scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        RectTransform viewport = CreateChild(scrollRt, "Viewport");
        Stretch(viewport);
        Image vpMask = viewport.gameObject.AddComponent<Image>();
        vpMask.color = Color.clear;
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        scroll.viewport = viewport;

        content = CreateChild(viewport, "Content");
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 0;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        ContentSizeFitter csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;
        return scroll;
    }

    public static RectTransform CreateMomentsCoverHeader(RectTransform content, Sprite avatar)
    {
        RectTransform cover = CreateChild(content, "Cover");
        LayoutElement le = cover.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 120;
        Image bg = cover.gameObject.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.22f, 0.28f, 1f);
        Image av = CreateAvatarImage(cover, 56, avatar);
        RectTransform avRt = av.rectTransform;
        avRt.anchorMin = new Vector2(0, 0);
        avRt.anchorMax = new Vector2(0, 0);
        avRt.pivot = new Vector2(0, 0);
        avRt.anchoredPosition = new Vector2(16, 16);
        avRt.sizeDelta = new Vector2(56, 56);
        return cover;
    }

    public static RectTransform CreateMomentsEmptyHint(RectTransform content, string message)
    {
        RectTransform row = CreateChild(content, "Empty");
        LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 80;
        TMP_Text tmp = CreateTmpText(row, message, 16, TextAlignmentOptions.Center);
        tmp.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        return row;
    }

    public static TMP_Text CreateTmpText(RectTransform parent, string text, int fontSize, TextAlignmentOptions align)
    {
        TMP_Text tmp = CreateTmp(parent, "TMP", fontSize, align);
        tmp.text = text;
        return tmp;
    }

    public static Image CreateAvatarImage(RectTransform parent, float size, Sprite sprite)
    {
        RectTransform av = CreateChild(parent, "Avatar");
        LayoutElement le = av.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;
        Image img = av.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = sprite == null ? new Color(0.35f, 0.35f, 0.38f, 1f) : Color.white;
        img.preserveAspect = true;
        return img;
    }

    public static Button CreateTextButton(RectTransform parent, string label, Color labelColor)
    {
        RectTransform rt = CreateChild(parent, "Btn_" + label);
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 48;
        le.preferredHeight = 28;
        Button btn = rt.gameObject.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.clear;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        btn.colors = cb;
        TMP_Text tmp = CreateTmpText(rt, label, 15, TextAlignmentOptions.Center);
        tmp.color = labelColor;
        return btn;
    }


    public static RectTransform CreateMessageRow(
        RectTransform parent,
        float maxBubbleWidth,
        float avatarSz,
        Color remiBubble,
        Color playerBubble,
        Sprite remiAv,
        Sprite playerAv)
    {
        RectTransform row = CreateChild(parent, "MessageRow");
        // 横向撑满 Content，Spacer 才能把玩家气泡顶到右侧
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(0f, avatarSz + 4f);
        row.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = avatarSz + 4;
        le.preferredHeight = -1;
        le.flexibleWidth = 1f;
        le.minWidth = 0f;

        Image remiAvatarImg = CreateAvatarBlock(row, "RemiAvatar", avatarSz, remiAv);
        Image remiBubbleImg = CreateBubbleBlock(row, "RemiBubble", maxBubbleWidth, remiBubble, out TMP_Text remiTmp);
        CreateFlexSpacer(row);
        Image playerBubbleImg = CreateBubbleBlock(row, "PlayerBubble", maxBubbleWidth, playerBubble, out TMP_Text playerTmp);
        Image playerAvatarImg = CreateAvatarBlock(row, "PlayerAvatar", avatarSz, playerAv);

        SocialChatMessageRow comp = row.gameObject.AddComponent<SocialChatMessageRow>();
        comp.Bind(row, remiAvatarImg, playerAvatarImg, remiBubbleImg, playerBubbleImg, remiTmp, playerTmp);
        return row;
    }

    public static RectTransform CreateTimeDivider(RectTransform parent, Color textColor)
    {
        RectTransform row = CreateChild(parent, "TimeDivider");
        LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 28;
        TMP_Text tmp = CreateTmp(row, "Time", 14, TextAlignmentOptions.Center);
        Stretch(tmp.rectTransform, 0, 4, 0, 4);
        tmp.color = textColor;
        SocialChatTimeDivider divider = row.gameObject.AddComponent<SocialChatTimeDivider>();
        divider.Bind(tmp);
        return row;
    }

    private static LayoutElement CreateFlexSpacer(RectTransform row)
    {
        RectTransform sp = CreateChild(row, "Spacer");
        LayoutElement le = sp.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = 0;
        return le;
    }

    private static Image CreateAvatarBlock(RectTransform row, string name, float size, Sprite sprite)
    {
        RectTransform av = CreateChild(row, name);
        LayoutElement le = av.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;
        Image img = av.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = sprite == null ? new Color(0.35f, 0.35f, 0.38f) : Color.white;
        img.preserveAspect = true;
        return img;
    }

    private static Image CreateBubbleBlock(
        RectTransform row,
        string name,
        float maxWidth,
        Color color,
        out TMP_Text text)
    {
        RectTransform bub = CreateChild(row, name);
        Image img = bub.gameObject.AddComponent<Image>();
        img.color = color;

        LayoutElement le = bub.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = maxWidth;
        le.flexibleWidth = 0;

        text = CreateTmp(bub, "Text", 18, TextAlignmentOptions.TopLeft);
        Stretch(text.rectTransform, 12, 10, 12, 10);
        text.color = Color.white;
        text.enableWordWrapping = true;

        ContentSizeFitter csf = bub.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return img;
    }

    private static Button CreateButton(RectTransform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
    {
        RectTransform rt = CreateChild(parent, name);
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        Button btn = rt.gameObject.AddComponent<Button>();
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.28f, 1f);
        TMP_Text tmp = CreateTmp(rt, "Text", 22, TextAlignmentOptions.Center);
        Stretch(tmp.rectTransform);
        tmp.text = label;
        tmp.color = Color.white;
        return btn;
    }

    public static RectTransform CreateChild(RectTransform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static TMP_Text CreateTmp(RectTransform parent, string name, int fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0) =>
        StretchFill(rt, left, bottom, right, top);

    private static void SetAnchors(
        RectTransform rt,
        float minX,
        float minY,
        float maxX,
        float maxY,
        Vector2 anchoredPos,
        Vector2 sizeDelta)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.pivot = new Vector2(minX, maxY);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }
}
