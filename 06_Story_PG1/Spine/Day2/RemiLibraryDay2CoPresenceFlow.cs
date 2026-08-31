using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Day2 图书馆共现：Window（进馆未对话仅时间保底）→ AnchorStory → FreeChat → Studying → Farewell → Finished。
/// </summary>
[DisallowMultipleComponent]
public class RemiLibraryDay2CoPresenceFlow : MonoBehaviour
{
    public static RemiLibraryDay2CoPresenceFlow Instance { get; private set; }

    public const string PrefsKeyState = "RemiDay2CoPresence_State";

    public enum FlowState
    {
        Inactive = 0,
        WindowOpen = 1,
        /// <summary>Remi Anchor 短 Story 播放中（旧存档值 2 = AnchorActive）。</summary>
        AnchorStory = 2,
        /// <summary>共现已收束（数值保持 3，兼容旧 Prefs）。</summary>
        Finished = 3,
        /// <summary>Story 结束后就地自由聊（LibraryDefaultPos，不瞬移）。</summary>
        FreeChat = 4,
        /// <summary>自习巡逻：LibraryDefaultPos → InStudy(R)…(4)。</summary>
        Studying = 5,
        /// <summary>到达终点后的告别演出。</summary>
        Farewell = 6,
    }

    public enum AnchorEntryKind
    {
        Stay = 0,
        PlayerAsked = 1,
        RemiGuarantee = 2,
        /// <summary>玩家按 F 主动搭话。</summary>
        FaceApproach = 3,
    }

    [Header("总开关")]
    [SerializeField] private bool enableFlow = true;
    [SerializeField] private bool persistProgress = true;

    [Header("依赖")]
    [SerializeField] private RemiLibraryDay2CoPresenceStory storyBeat;
    [SerializeField] private RemiLibraryStudyPatrol studyPatrol;
    [SerializeField] private RemiLibraryDay2StudyWhisper studyWhisper;

    [Header("Window（进馆未开对话：仅时间保底）")]
    [Tooltip("进入图书馆后，若一直未按 F / 触发共现，满该秒数由 Remi 主动开场（不看闲聊轮数）。")]
    [SerializeField] private float guaranteeSeconds = 75f;
    [SerializeField] private string[] playerAskAliases =
    {
        "作品展", "展览", "作业", "自习", "资料", "筹备",
        "忙什么", "在干什么", "为什么叫我", "叫我来",
        "图书馆", "刷怪点", "AI游戏", "AI游戏入门",
    };

    [Header("UI")]
    [SerializeField] private string stayButtonLabel = "留下来";
    [SerializeField] private string stayConfirmButtonLabel = "确认";
    [SerializeField] private string studyButtonLabel = "开始自习";

    [Header("馆内朝向（默认面向书架=0；第一次剧情/闲聊面向玩家=-90）")]
    [SerializeField] private float remiBookshelfYawDegrees = 0f;
    [SerializeField] private float remiFacingPlayerYawDegrees = -90f;
    [SerializeField] private float remiYawTurnDurationSeconds = 0.7f;

    [Header("告别（到达 InStudy(R) (4)）")]
    [SerializeField] private string[] farewellRemiLines =
    {
        "今天谢谢你陪我待这么久。",
        "我明天应该还会来这里。",
        "嗯。路上小心。",
    };
    [Tooltip("场景中机位根名；含 Camera 子物体。摆绝对位置即可，告别时挂到 Remi 保持相对构图。")]
    [SerializeField] private string farewellCamMarker =
        SceneTravelCatalog.LibraryDay2FarewellCamMarkerName;
    [SerializeField] private RemiRelativeCameraAnchor farewellCamAnchor;
    [Tooltip("台词播完后、隐藏 Remi 前多停一会儿，避免人突然消失。")]
    [SerializeField] private float farewellHoldAfterLinesSeconds = 0.45f;

    private FlowState _state = FlowState.Inactive;
    private bool _sequenceRunning;
    private Coroutine _windowWatchRoutine;
    private bool _windowTimingActive;
    private AnchorEntryKind _entryKind = AnchorEntryKind.Stay;
    private RemiInteraction _stayInteraction;
    private bool _awaitingStayConfirm;

    public FlowState State => _state;
    public bool IsSequenceRunning => _sequenceRunning;
    public bool IsAwaitingStayConfirm => _awaitingStayConfirm;
    public string StayButtonLabel => stayButtonLabel;
    public string StayConfirmButtonLabel => stayConfirmButtonLabel;
    public string StudyButtonLabel => studyButtonLabel;

    public bool IsQuestFeatureEnabled => enableFlow;

    public bool IsDay2CommissionWindowOpen =>
        enableFlow && _state == FlowState.WindowOpen;

    public bool IsInAnchorStory =>
        enableFlow && _state == FlowState.AnchorStory;

    public bool IsInFreeChat =>
        enableFlow && _state == FlowState.FreeChat;

    public bool IsStudying =>
        enableFlow && _state == FlowState.Studying;

    public bool IsInFarewell =>
        enableFlow && _state == FlowState.Farewell;

    public bool IsStudyWhisperActive =>
        studyWhisper != null && studyWhisper.IsAskActive;

    /// <summary>自习告别已播完（离馆 Ending 可跳过重复谢词）。</summary>
    public bool HasCompletedStudyFarewell =>
        enableFlow && _state == FlowState.Finished;

    /// <summary>
    /// 自习终点或中途离馆共用：FinalSpecial 机位 + 告别台词 + 藏 Remi。
    /// 已播过则立刻结束。由 Spine Day2 Ending 在切黑屏前 yield。
    /// </summary>
    public IEnumerator CoPlayFarewellWithCameraIfNeeded()
    {
        if (!enableFlow)
            yield break;
        if (_state == FlowState.Finished || _state == FlowState.Farewell)
            yield break;
        // 尚未进入可告别阶段（窗期 / 锚点剧情）不播馆内机位
        if (_state == FlowState.Inactive ||
            _state == FlowState.WindowOpen ||
            _state == FlowState.AnchorStory)
            yield break;

        yield return CoStudyFarewell();
    }

    /// <summary>已进共现（Story 及之后）；不再开 Window。</summary>
    public bool HasEnteredCoPresence =>
        enableFlow &&
        (_state == FlowState.AnchorStory ||
         _state == FlowState.FreeChat ||
         _state == FlowState.Studying ||
         _state == FlowState.Farewell ||
         _state == FlowState.Finished);

    /// <summary>图书馆共现期间允许面对面共位判断。</summary>
    public bool AllowsFaceToFaceCoLocation =>
        enableFlow &&
        (_state == FlowState.WindowOpen ||
         _state == FlowState.AnchorStory ||
         _state == FlowState.FreeChat ||
         _state == FlowState.Studying);

    /// <summary>Window：留下来 → 进 Anchor Story。</summary>
    public bool ShouldShowStayExitUx() => IsDay2CommissionWindowOpen;

    /// <summary>FreeChat：开始自习 → Studying 路点。</summary>
    public bool ShouldShowStudyExitUx() => IsInFreeChat;

    public bool IsBlockingDialogueExit =>
        _sequenceRunning || _awaitingStayConfirm || _state == FlowState.Farewell;

    public static void EnsureExists()
    {
        if (Instance != null)
        {
            RemiLibraryStudyPatrol.EnsureOn(Instance);
            RemiLibraryDay2StudyWhisper.EnsureOn(Instance);
            return;
        }

        RemiLibraryDay2CoPresenceStory story =
            FindObjectOfType<RemiLibraryDay2CoPresenceStory>(true);
        if (story == null)
            return;

        RemiLibraryDay2CoPresenceFlow existing =
            story.GetComponent<RemiLibraryDay2CoPresenceFlow>();
        if (existing == null)
            existing = story.gameObject.AddComponent<RemiLibraryDay2CoPresenceFlow>();
        existing.storyBeat = story;
        RemiLibraryStudyPatrol.EnsureOn(existing);
        RemiLibraryDay2StudyWhisper.EnsureOn(existing);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RemiLibraryDay2CoPresenceFlow] 多实例，保留先激活的。", this);
            enabled = false;
            return;
        }

        Instance = this;
        if (storyBeat == null)
            storyBeat = GetComponent<RemiLibraryDay2CoPresenceStory>();
        if (studyPatrol == null)
            studyPatrol = GetComponent<RemiLibraryStudyPatrol>();
        if (studyWhisper == null)
            studyWhisper = GetComponent<RemiLibraryDay2StudyWhisper>();
        RemiLibraryDay2StudyWhisper.EnsureOn(this);
        if (studyWhisper == null)
            studyWhisper = GetComponent<RemiLibraryDay2StudyWhisper>();

        if (!enableFlow)
        {
            _state = FlowState.Finished;
            return;
        }

        if (persistProgress && PlayerPrefs.HasKey(PrefsKeyState))
        {
            int raw = PlayerPrefs.GetInt(PrefsKeyState, 0);
            _state = (FlowState)Mathf.Clamp(raw, 0, 6);
            // 旧档 AnchorActive(=2)：若 intro 已播完，视为已进入 FreeChat
            if (_state == FlowState.AnchorStory &&
                PlayerPrefs.GetInt("RemiStory_LibraryDay2CoPresence", 0) != 0)
            {
                _state = FlowState.FreeChat;
                PersistState();
            }
        }
        else
            _state = FlowState.Inactive;
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        TryRecoverInterruptedAnchorStory();
        TryRecoverInterruptedStudy();
        TryBootstrapWindowForActiveScene();
    }

    /// <summary>
    /// 读档落在 AnchorStory 且无进行中协程：按 intro 是否已播，恢复到 FreeChat 或退回 Window。
    /// </summary>
    private void TryRecoverInterruptedAnchorStory()
    {
        if (!enableFlow || _state != FlowState.AnchorStory || _sequenceRunning)
            return;

        if (PlayerPrefs.GetInt("RemiStory_LibraryDay2CoPresence", 0) != 0)
        {
            EnterFreeChat(openDialogue: false);
            return;
        }

        _state = FlowState.WindowOpen;
        PersistState();
    }

    /// <summary>读档落在 Studying / Farewell：自习重跑或直接标完成。</summary>
    private void TryRecoverInterruptedStudy()
    {
        if (!enableFlow || _sequenceRunning)
            return;

        if (_state == FlowState.Farewell)
        {
            _state = FlowState.Finished;
            PersistState();
            return;
        }

        if (_state == FlowState.Studying)
        {
            RemiWorldPlacement.SetRemiWorldYaw(remiBookshelfYawDegrees);
            BeginStudying();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryBootstrapWindowForActiveScene();
    }

    private void TryBootstrapWindowForActiveScene()
    {
        if (!enableFlow || _state == FlowState.Finished || HasEnteredCoPresence)
            return;

        if (SceneTravelCatalog.ResolveFromActiveScene() != SceneTravelLocation.Library)
            return;

        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector spine = RemiDemoSpineDirector.Instance;
        if (spine == null || !spine.IsAwaitingDay2LibraryVisit())
            return;

        NotifyLibraryWindowStart();
    }

    /// <summary>进馆且仍等共现 intro：打开 Day2 Window。</summary>
    public void NotifyLibraryWindowStart()
    {
        if (!enableFlow)
            return;
        if (_state == FlowState.Finished || HasEnteredCoPresence)
            return;

        RemiPresenceService presence = RemiPresenceService.Instance;
        if (presence != null)
        {
            if (presence.CurrentDayBlockKind == RemiDayBlockKind.Routine ||
                presence.CurrentDayBlockSlot == RemiDayBlockSlot.A)
                presence.EnterDayBlock(RemiDayBlockSlot.B, syncPhaseHint: true, enterAnchor: false);
            presence.PinLocationSoft(RemiLocation.Library, RemiActivity.Busy);
        }

        _state = FlowState.WindowOpen;
        _awaitingStayConfirm = false;
        PersistState();
        StartWindowWatch();
    }

    /// <summary>Day2 Window 不走闲聊轮数保底；保留空实现以免旧调用报错。</summary>
    public void NotifyFaceDialogueOpened()
    {
    }

    /// <summary>Day2 Window 不走闲聊轮数保底；保留空实现以免旧调用报错。</summary>
    public void NotifyFacePlayerChat(string playerText)
    {
    }

    public bool TryBeginPlayerAskAnchorFromChat(string playerText, RemiInteraction interaction)
    {
        if (!IsDay2CommissionWindowOpen)
            return false;
        if (_sequenceRunning || _awaitingStayConfirm)
            return false;
        if (!MatchesPlayerAskAlias(playerText))
            return false;

        return TryOpenAnchor(AnchorEntryKind.PlayerAsked, interaction);
    }

    /// <summary>
    /// Window 期玩家按 F 搭话：不直接开自由聊，先进 Anchor 短 Story。
    /// </summary>
    public bool TryBeginFaceApproachAnchor(RemiInteraction interaction)
    {
        if (!IsDay2CommissionWindowOpen)
            return false;
        if (_sequenceRunning || _awaitingStayConfirm)
            return false;

        return TryOpenAnchor(AnchorEntryKind.FaceApproach, interaction);
    }

    public bool TryOpenAnchor(AnchorEntryKind kind, RemiInteraction interaction)
    {
        if (!enableFlow)
            return false;
        if (HasEnteredCoPresence)
            return false;
        if (_sequenceRunning || _awaitingStayConfirm)
            return false;

        StopWindowWatch();
        StartCoroutine(CoOpenAnchor(kind, interaction));
        return true;
    }

    public void StartStaySequence(RemiInteraction interaction)
    {
        if (!ShouldShowStayExitUx() || _sequenceRunning || _awaitingStayConfirm)
            return;
        if (interaction == null || !interaction.IsInDialogue)
            return;

        if (!TryOpenAnchor(AnchorEntryKind.Stay, interaction))
            interaction.EndDialogue();
    }

    public void ConfirmStayAndClose(RemiInteraction interaction)
    {
        if (!_awaitingStayConfirm)
            return;

        _awaitingStayConfirm = false;
        RemiInteraction target = interaction != null ? interaction : _stayInteraction;
        _stayInteraction = null;
        DialoguePanel.SetScriptedFlowInputLocked(false);

        // Anchor Story 已播完；确认只关面板，位置不动
        if (target != null && target.IsInDialogue)
            target.EndDialogue();
    }

    private IEnumerator CoOpenAnchor(AnchorEntryKind kind, RemiInteraction interaction)
    {
        _sequenceRunning = true;
        _awaitingStayConfirm = false;
        _entryKind = kind;

        if (interaction != null && interaction.IsInDialogue)
            interaction.EndDialogue();

        RemiPresenceService presence = RemiPresenceService.Instance;
        if (presence != null)
        {
            if (presence.CurrentDayBlockKind == RemiDayBlockKind.Routine ||
                presence.CurrentDayBlockSlot == RemiDayBlockSlot.A ||
                !presence.DayBlockInAnchor)
            {
                presence.EnterDayBlock(RemiDayBlockSlot.B, syncPhaseHint: true, enterAnchor: false);
                presence.EnterDayBlockAnchor();
            }

            presence.BeginCoPresenceEpisode(occupiesPhase: true);
        }

        _state = FlowState.AnchorStory;
        PersistState();

        // 默认面向书架 → 第一次剧情前转向玩家
        RemiWorldPlacement.SetRemiWorldYaw(remiBookshelfYawDegrees);
        yield return CoRotateRemiYaw(remiFacingPlayerYawDegrees, remiYawTurnDurationSeconds);

        if (storyBeat == null)
            storyBeat = FindObjectOfType<RemiLibraryDay2CoPresenceStory>(true);

        bool storyDone = false;
        if (storyBeat != null)
        {
            storyBeat.PlayAnchorIntroInPlace(() => storyDone = true);
            while (!storyDone)
                yield return null;
        }
        else
        {
            Debug.LogWarning("[RemiLibraryDay2CoPresenceFlow] 无 RemiLibraryDay2CoPresenceStory，跳过 Anchor Story。");
            RemiDemoSpineDirector.EnsureExists();
            RemiDemoSpineDirector.Instance?.NotifyDay2LibraryIntroFinished();
        }

        _state = FlowState.FreeChat;
        PersistState();

        _sequenceRunning = false;
        _awaitingStayConfirm = false;
        DialoguePanel.SetScriptedFlowInputLocked(false);

        // 就地闲聊：不瞬移；自动打开对话面板（保持面向玩家）
        yield return null;
        TryOpenFreeChatDialogue();
    }

    private void EnterFreeChat(bool openDialogue)
    {
        _state = FlowState.FreeChat;
        PersistState();
        // 读档/恢复进闲聊时保持面向玩家
        RemiWorldPlacement.SetRemiWorldYaw(remiFacingPlayerYawDegrees);
        if (openDialogue)
            TryOpenFreeChatDialogue();
    }

    private void TryOpenFreeChatDialogue()
    {
        RemiInteraction interaction = FindObjectOfType<RemiInteraction>();
        if (interaction == null || interaction.IsInDialogue)
            return;

        interaction.StartDialogue(bypassOpenGates: true);
    }

    /// <summary>FreeChat 面板「开始自习」。</summary>
    public void StartStudySequence(RemiInteraction interaction)
    {
        if (!ShouldShowStudyExitUx() || _sequenceRunning)
            return;
        if (interaction == null || !interaction.IsInDialogue)
            return;

        interaction.EndDialogue();
        StartCoroutine(CoBeginStudyingAfterFacingBookshelf());
    }

    /// <summary>闲聊结束：转回书架朝向，再由 StudyPatrol 在起点用 mid 停顿后开走。</summary>
    private IEnumerator CoBeginStudyingAfterFacingBookshelf()
    {
        if (!enableFlow || _sequenceRunning)
            yield break;
        if (_state != FlowState.FreeChat && _state != FlowState.Studying)
            yield break;

        _sequenceRunning = true;
        yield return CoRotateRemiYaw(remiBookshelfYawDegrees, remiYawTurnDurationSeconds);
        _sequenceRunning = false;
        BeginStudying();
    }

    public void BeginStudying()
    {
        if (!enableFlow)
            return;
        if (_state != FlowState.FreeChat && _state != FlowState.Studying)
            return;
        if (_sequenceRunning)
            return;

        RemiLibraryStudyPatrol.EnsureOn(this);
        if (studyPatrol == null)
            studyPatrol = GetComponent<RemiLibraryStudyPatrol>();

        RemiLibraryDay2StudyWhisper.EnsureOn(this);
        if (studyWhisper == null)
            studyWhisper = GetComponent<RemiLibraryDay2StudyWhisper>();
        studyWhisper?.ResetForNewStudy();

        _state = FlowState.Studying;
        PersistState();

        if (studyPatrol == null)
        {
            Debug.LogWarning("[RemiLibraryDay2CoPresenceFlow] 无 StudyPatrol，直接告别。");
            StartCoroutine(CoStudyFarewell());
            return;
        }

        studyPatrol.BeginStudy(OnStudyPathCompleted);
    }

    private IEnumerator CoRotateRemiYaw(float targetYawDegrees, float durationSeconds)
    {
        Transform remi = ResolveRemiRootForYaw();
        if (remi == null)
        {
            RemiWorldPlacement.SetRemiWorldYaw(targetYawDegrees);
            yield break;
        }

        float startYaw = remi.eulerAngles.y;
        float duration = Mathf.Max(0.01f, durationSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // 平滑起步/停步，避免硬切
            t = t * t * (3f - 2f * t);
            float yaw = Mathf.LerpAngle(startYaw, targetYawDegrees, t);
            Vector3 e = remi.eulerAngles;
            e.y = yaw;
            remi.eulerAngles = e;
            yield return null;
        }

        RemiWorldPlacement.SetRemiWorldYaw(targetYawDegrees);
    }

    private static Transform ResolveRemiRootForYaw()
    {
        RemiInteraction interaction = FindObjectOfType<RemiInteraction>();
        if (interaction != null)
            return interaction.transform;

        Remi remi = FindObjectOfType<Remi>();
        return remi != null ? remi.transform : null;
    }

    /// <summary>自习中按 F 点问。</summary>
    public bool TryStudyWhisperAsk(RemiInteraction interaction)
    {
        if (!IsStudying || _sequenceRunning || IsInFarewell)
            return false;

        RemiLibraryDay2StudyWhisper.EnsureOn(this);
        if (studyWhisper == null)
            studyWhisper = GetComponent<RemiLibraryDay2StudyWhisper>();
        return studyWhisper != null && studyWhisper.TryBeginAsk(interaction);
    }

    public string ResolveStudyWhisperTip()
    {
        if (studyWhisper == null)
            studyWhisper = GetComponent<RemiLibraryDay2StudyWhisper>();
        return studyWhisper != null ? studyWhisper.ResolveTipPrompt() : "自习中…";
    }

    private void OnStudyPathCompleted()
    {
        if (_state != FlowState.Studying)
            return;
        StartCoroutine(CoStudyFarewell());
    }

    private IEnumerator CoStudyFarewell()
    {
        if (_state == FlowState.Finished || _state == FlowState.Farewell)
            yield break;

        _sequenceRunning = true;
        _state = FlowState.Farewell;
        PersistState();

        studyPatrol?.EndStudy();
        studyWhisper?.AbortAskIfActive();

        RemiInteraction interaction = FindObjectOfType<RemiInteraction>();
        if (interaction != null && interaction.IsInDialogue)
            interaction.EndDialogue();

        Transform remiRoot = interaction != null
            ? interaction.transform
            : null;
        if (remiRoot == null)
        {
            Remi remi = FindObjectOfType<Remi>();
            remiRoot = remi != null ? remi.transform : null;
        }

        // Idle2 面向告别
        Remi remiBody = remiRoot != null
            ? remiRoot.GetComponent<Remi>() ?? remiRoot.GetComponentInChildren<Remi>(true)
            : null;
        remiBody?.SetDialogueBodyIdle(true);

        RemiRelativeCameraAnchor camAnchor = ResolveFarewellCamAnchor();
        bool usedFarewellCam = camAnchor != null && camAnchor.TryBeginCinematic(remiRoot);
        if (!usedFarewellCam)
            Debug.LogWarning("[RemiLibraryDay2CoPresenceFlow] 未启用 FinalSpecial 告别机位，仅播文本。");

        StoryDirector director = storyBeat != null ? storyBeat.BoundStoryDirector : null;
        if (director == null)
            director = FindObjectOfType<StoryDirector>();

        if (director != null && farewellRemiLines != null && farewellRemiLines.Length > 0)
        {
            var lines = new System.Collections.Generic.List<StoryDirector.StoryLine>();
            for (int i = 0; i < farewellRemiLines.Length; i++)
            {
                string text = farewellRemiLines[i];
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                lines.Add(new StoryDirector.StoryLine
                {
                    speakerName = "Remi",
                    text = text.Trim(),
                });
            }

            if (lines.Count > 0)
            {
                bool done = false;
                void OnFinished() => done = true;
                director.StoryFinished += OnFinished;
                director.ResetStoryPlaybackState();
                director.PrepareForTriggeredEpisode();
                director.SetRemiAfterStoryPoint(null);
                director.SetLines(lines);
                if (remiRoot != null)
                    director.SetRemiRoot(remiRoot);
                director.BeginStory();
                while (!done)
                    yield return null;
                director.StoryFinished -= OnFinished;
            }
        }

        float hold = Mathf.Max(0f, farewellHoldAfterLinesSeconds);
        if (hold > 0f)
            yield return new WaitForSecondsRealtime(hold);

        // 先藏人，再还机位，避免玩家相机看到空位闪一下
        RemiWorldPlacement.HideRemiInCurrentScene();
        if (camAnchor != null)
            camAnchor.EndCinematic();

        _state = FlowState.Finished;
        PersistState();
        _sequenceRunning = false;
        DialoguePanel.SetScriptedFlowInputLocked(false);

        RemiInteraction remiIx = interaction != null
            ? interaction
            : FindObjectOfType<RemiInteraction>();
        remiIx?.RefreshRoleWorldUiAfterStory();
    }

    private RemiRelativeCameraAnchor ResolveFarewellCamAnchor()
    {
        if (farewellCamAnchor != null)
            return farewellCamAnchor;

        string marker = string.IsNullOrWhiteSpace(farewellCamMarker)
            ? SceneTravelCatalog.LibraryDay2FarewellCamMarkerName
            : farewellCamMarker.Trim();
        farewellCamAnchor = RemiRelativeCameraAnchor.FindInActiveScene(marker);
        return farewellCamAnchor;
    }

    private void StartWindowWatch()
    {
        StopWindowWatch();
        if (!IsDay2CommissionWindowOpen)
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
        // 仅统计「进馆后尚未触发共现」的空闲时间；共现闲聊在 Anchor 之后，不参与保底。
        float startUnscaled = Time.unscaledTime;
        while (_windowTimingActive && enableFlow && _state == FlowState.WindowOpen)
        {
            if (_sequenceRunning || _awaitingStayConfirm)
            {
                yield return null;
                continue;
            }

            float elapsed = Time.unscaledTime - startUnscaled;
            if (guaranteeSeconds > 0f && elapsed >= guaranteeSeconds)
            {
                TryOpenAnchor(AnchorEntryKind.RemiGuarantee, null);
                yield break;
            }

            yield return null;
        }

        _windowWatchRoutine = null;
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

    /// <summary>读档后：按 PlayerPrefs 重载状态并停掉进行中演出。</summary>
    public void ReloadStateFromPrefs()
    {
        StopWindowWatch();
        studyPatrol?.EndStudy();
        studyWhisper?.AbortAskIfActive();
        _sequenceRunning = false;
        _awaitingStayConfirm = false;

        if (!enableFlow)
        {
            _state = FlowState.Finished;
            return;
        }

        if (persistProgress && PlayerPrefs.HasKey(PrefsKeyState))
        {
            int raw = PlayerPrefs.GetInt(PrefsKeyState, 0);
            _state = (FlowState)Mathf.Clamp(raw, 0, 6);
            if (_state == FlowState.AnchorStory &&
                PlayerPrefs.GetInt("RemiStory_LibraryDay2CoPresence", 0) != 0)
                _state = FlowState.FreeChat;
        }
        else
            _state = FlowState.Inactive;

        TryRecoverInterruptedAnchorStory();
        TryRecoverInterruptedStudy();
        TryBootstrapWindowForActiveScene();
    }

    private void PersistState()
    {
        if (!persistProgress || !enableFlow)
            return;
        PlayerPrefs.SetInt(PrefsKeyState, (int)_state);
    }

#if UNITY_EDITOR
    [ContextMenu("Reset Day2 CoPresence flow")]
    private void Editor_Reset()
    {
        PlayerPrefs.DeleteKey(PrefsKeyState);
        StopWindowWatch();
        studyPatrol?.EndStudy();
        _state = FlowState.Inactive;
        _sequenceRunning = false;
        _awaitingStayConfirm = false;
    }
#endif
}
