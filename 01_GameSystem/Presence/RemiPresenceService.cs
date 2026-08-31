using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Remi 生活轨迹层：世界（时段/地点/日程）→ 内在状态 → 决策倾向 → 由 <see cref="PromptContextManager"/> / AI 表达。
/// 事件内容（剧情/找书/问候）可换，本层结构保持稳定。
/// </summary>
[DisallowMultipleComponent]
public class RemiPresenceService : MonoBehaviour
{
    public static RemiPresenceService Instance { get; private set; }

    [Header("世界：当前时段（可由剧情/调试推进）")]
    [SerializeField] private RemiDayPhase currentPhase = RemiDayPhase.Morning;

    [Header("默认轨道（按时段）")]
    [SerializeField] private RemiScheduleSlot[] defaultSchedule =
    {
        new RemiScheduleSlot
        {
            phase = RemiDayPhase.Morning,
            location = RemiLocation.Classroom,
            activity = RemiActivity.InClass,
            scheduleNote = "上午在教室上课或刚下课。"
        },
        new RemiScheduleSlot
        {
            phase = RemiDayPhase.Afternoon,
            location = RemiLocation.Library,
            activity = RemiActivity.Free,
            scheduleNote = "下午常在图书馆自习。"
        },
        new RemiScheduleSlot
        {
            phase = RemiDayPhase.Evening,
            location = RemiLocation.Dorm,
            activity = RemiActivity.Cooking,
            scheduleNote = "傍晚在宿舍，可能在做饭。"
        },
        new RemiScheduleSlot
        {
            phase = RemiDayPhase.Night,
            location = RemiLocation.Dorm,
            activity = RemiActivity.Sleeping,
            scheduleNote = "夜间休息，不宜打扰。"
        },
    };

    [Header("偏离日程（改地点 = 占满 phase；无 micro 短暂偏离）")]
    [SerializeField] private bool useScheduleOverride;
    [SerializeField] private RemiLocation overrideLocation;
    [SerializeField] private RemiActivity overrideActivity;

    [Header("当前时段 Episode（叙事占格，与轨道偏离正交）")]
    [SerializeField] private RemiPhaseEpisodeKind currentEpisodeKind = RemiPhaseEpisodeKind.Default;
    [SerializeField] private bool episodeOccupiesPhase;
    [Tooltip("面对面/社媒短互动结束是否 BeatOnly+1。")]
    [SerializeField] private bool advanceBeatOnInterludeEnd = true;

    [Header("交互通道（由 RemiInteraction / 社媒 UI 设置）")]
    [SerializeField] private RemiInteractionChannel currentChannel = RemiInteractionChannel.FaceToFace;

    [Header("互动节奏（锚点写关系档 · Gate 仅进场资格 · Beat 一次性展现）")]
    [SerializeField] private RemiInteractionRhythmThresholds rhythmThresholds = new RemiInteractionRhythmThresholds();
    [SerializeField] private bool persistRhythmProgress = true;
    [SerializeField] private bool storyDayStarted;
    [SerializeField] private RemiDelegationProgress delegationProgress = new RemiDelegationProgress();
    [SerializeField] private RemiDelegationGateRule[] delegationGateRules = RemiDelegationGateCatalog.CreateDefaultRules();
    [SerializeField] private RemiRhythmBeatFlags playedRhythmBeats;
    [SerializeField] private RemiDialogueDepthStage dialogueDepthStage = RemiDialogueDepthStage.Surface;
    [SerializeField] private RemiStoryAnchorFlags committedStoryAnchors;

    private const string PrefsRhythmStory = "RemiRhythm_StoryStarted";
    private const string PrefsRhythmDelegations = "RemiRhythm_Delegations";
    private const string PrefsRhythmBookLegacy = "RemiRhythm_BookDone";
    private const string PrefsRhythmBeats = "RemiRhythm_PlayedBeats";
    private const string PrefsRhythmDepthStage = "RemiRhythm_DepthStage";
    private const string PrefsRhythmAnchors = "RemiRhythm_StoryAnchors";
    private const string PrefsWorldTime = "RemiWorldTime";
    private const string PrefsDayBlockSlot = "RemiDayBlock_Slot";
    private const string PrefsDayBlockKind = "RemiDayBlock_Kind";
    private const string PrefsDayBlockInAnchor = "RemiDayBlock_InAnchor";

    [Header("叙事时钟（权威：日程 / 动态 / 社媒共用）")]
    [SerializeField] private RemiWorldTime worldTime = RemiWorldTime.BeforeStory;
    [SerializeField] private bool persistWorldTime = true;

    [Header("Day Block（日内叙事块；非钟点）")]
    [SerializeField] private RemiDayBlockSlot currentDayBlockSlot = RemiDayBlockSlot.A;
    [SerializeField] private RemiDayBlockKind currentDayBlockKind = RemiDayBlockKind.None;
    [SerializeField] private bool dayBlockInAnchor;

    public RemiDayPhase CurrentPhase => currentPhase;
    public RemiDayBlockSlot CurrentDayBlockSlot => currentDayBlockSlot;
    public RemiDayBlockKind CurrentDayBlockKind => currentDayBlockKind;
    public bool DayBlockInAnchor => dayBlockInAnchor;
    public bool IsAgencyWindowOpen =>
        currentDayBlockKind == RemiDayBlockKind.Window && !dayBlockInAnchor;
    public RemiWorldTime WorldTime => worldTime;
    public RemiLocation CurrentLocation => ResolveLocation();
    public RemiActivity CurrentActivity => ResolveActivity();
    public RemiInteractionChannel CurrentChannel => currentChannel;
    public RemiDialogueDepthStage DialogueDepthStage => dialogueDepthStage;
    public RemiStoryAnchorFlags CommittedStoryAnchors => committedStoryAnchors;
    public bool StoryDayStarted => storyDayStarted;
    public RemiDelegationProgress DelegationProgress => delegationProgress;
    public int DelegationMilestoneCountForGate =>
        RemiDelegationGateCatalog.CountRelationalMilestones(delegationProgress?.CompletedMilestones, delegationGateRules);

    /// <summary>找书委托已交书（过程/里程碑记账；不升关系档）。</summary>
    public bool HasBookCommissionCompleteForGate =>
        delegationProgress != null &&
        delegationProgress.HasCompleted(RemiPresenceEventKind.PlayerSubmittedBook);

    /// <summary>图书馆 Day2 共现锚点或共享经历是否已登记。</summary>
    public bool HasLibraryCoPresenceCompleteForGate
    {
        get
        {
            if ((committedStoryAnchors & RemiStoryAnchorFlags.Day2LibraryCoPresence) != 0)
                return true;
            RemiSharedExperienceMemory.EnsureExists();
            return RemiSharedExperienceMemory.Instance != null &&
                   RemiSharedExperienceMemory.Instance.HasRecorded(RemiSharedExperienceId.Day2LibraryCoPresence);
        }
    }

    /// <summary>是否已具备进入 Day3 树干的资格（日≥3 + 图书馆锚点）；≠ 已是 Influential。</summary>
    public bool HasInfluentialDelegationGate =>
        RemiRhythmGateEvaluator.IsInfluentialGateOpen(GateSnapshot, rhythmThresholds);

    public RemiRhythmBeatFlags PlayedRhythmBeats => playedRhythmBeats;
    public RemiRhythmGateSnapshot GateSnapshot => RemiRhythmGateSnapshot.FromService(this);

    /// <summary>默认轨道中某时段的日程槽（供 <see cref="RemiDayPlanBuilder"/> 等读取）。</summary>
    public RemiScheduleSlot GetScheduleSlot(RemiDayPhase phase) => FindSlot(phase);

    public bool IsScheduleOverridden => useScheduleOverride;
    public RemiLocation OverrideLocation => overrideLocation;
    public RemiActivity OverrideActivity => overrideActivity;
    public RemiPhaseEpisodeKind CurrentEpisodeKind => currentEpisodeKind;
    public bool EpisodeOccupiesPhase => episodeOccupiesPhase;
    public RemiTrackAlignment TrackAlignment => GetTrackAlignment();

    /// <summary>Episode 结束或 phase 复位时。</summary>
    public event System.Action<RemiPhaseEpisodeKind, RemiEpisodeEndReason> PhaseEpisodeEnded;

    /// <summary>叙事时段变化（Morning/Afternoon/Evening/Night）；供场景光照等订阅。</summary>
    public event Action<RemiDayPhase> DayPhaseChanged;

    /// <summary>ApplyScheduleOverride 成功时（剧情脊柱主动偏离，如 Day3 来宿舍）。</summary>
    public event System.Action<RemiLocation, RemiActivity> ScheduleOverrideApplied;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RemiPresenceService] 重复实例，销毁。", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // System 子物体须先脱父再 DDOL，否则切场景会随父物体销毁。
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        RemiSharedExperienceMemory.EnsureExists();
        RemiDialogueArchive.EnsureExists();
        RemiMemoryCuratorStore.EnsureExists();
        RemiMemoryCurator.EnsureExists();
        RemiFragmentUnitStore.EnsureExists();
        RemiFragmentAnalyzer.EnsureExists();
        RemiFragmentMemory.EnsureExists();
        RemiMemoryDaySettlement.EnsureExists();
        PhoneAppAccess.EnsureLoaded();
        RemiDemoRunTelemetry.EnsureExists();
        RemiChatFragmentMemory.EnsureExists();
        RemiDemoMemoryDebugOverlay.EnsureExists();
        RemiFragmentPipelineTestRunner.EnsureExists();
        RemiSendSystemContentManager.EnsureExists();
        delegationProgress ??= new RemiDelegationProgress();
        if (delegationGateRules == null || delegationGateRules.Length == 0)
            delegationGateRules = RemiDelegationGateCatalog.CreateDefaultRules();
        LoadRhythmProgress();
        LoadWorldTime();
        SyncPhaseFromWorldTime();
        ApplyScheduleForPhase(currentPhase, clearOverride: true);
        PushToPromptContext();
        EnsureMomentsService();
        EnsurePhoneAppController();
        RemiDayPhaseLightingCoordinator.EnsureExists();
        RemiDemoSpineDirector.EnsureExists();
    }

    private static void EnsurePhoneAppController()
    {
        if (FindObjectOfType<PhoneAppController>() != null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            PlayerController controller = FindObjectOfType<PlayerController>();
            if (controller != null)
                player = controller.gameObject;
        }

        if (player != null)
            player.AddComponent<PhoneAppController>();
    }

    private static void EnsureMomentsService()
    {
        if (RemiMomentsService.Instance != null) return;
        var go = new GameObject("RemiMomentsService");
        go.AddComponent<RemiMomentsService>();
    }

    public void NotifyStoryDayStarted()
    {
        storyDayStarted = true;
        SaveRhythmProgress();
        RemiRhythmBeatPlayer.TryPlayStoryDayBegins(ref playedRhythmBeats);
        SaveRhythmProgress();
    }

    /// <summary>捕获当前叙事时间快照（发动态、聊天存档用）。</summary>
    public RemiWorldTime CaptureWorldTime() => worldTime.Capture();

    /// <summary>叙事时钟前进；仅由剧情 / 时段 / 重要事件调用。跨日不改关系档（档由锚点提交）。</summary>
    public RemiWorldTime AdvanceWorldTime(RemiTimeAdvanceReason reason, RemiDayPhase? targetPhase = null)
    {
        RemiDayPhase previousPhase = worldTime.phase;
        int previousStoryDay = worldTime.storyDay;

        switch (reason)
        {
            case RemiTimeAdvanceReason.StoryDayBegan:
                worldTime.storyDay = Mathf.Max(1, worldTime.storyDay <= 0 ? 1 : worldTime.storyDay);
                worldTime.phase = RemiDayPhase.Morning;
                worldTime.beat++;
                break;

            case RemiTimeAdvanceReason.PhaseChanged:
            {
                RemiDayPhase next = targetPhase ?? worldTime.phase;
                if (previousPhase == RemiDayPhase.Night && next == RemiDayPhase.Morning)
                    worldTime.storyDay = Mathf.Max(1, worldTime.storyDay) + 1;
                worldTime.phase = next;
                worldTime.beat++;
                break;
            }

            case RemiTimeAdvanceReason.NextDay:
                worldTime.storyDay = Mathf.Max(1, worldTime.storyDay) + 1;
                worldTime.phase = RemiDayPhase.Morning;
                worldTime.beat++;
                break;

            case RemiTimeAdvanceReason.BeatOnly:
                worldTime.beat++;
                break;

            case RemiTimeAdvanceReason.SyncOnly:
            default:
                if (targetPhase.HasValue)
                    worldTime.phase = targetPhase.Value;
                break;
        }

        SyncPhaseFromWorldTime();
        SaveWorldTime();
        PushToPromptContext();
        if (previousPhase != worldTime.phase)
            DayPhaseChanged?.Invoke(worldTime.phase);

        // 叙事日前进 → 日结刚关闭的那天（Filter → Curator）。
        if (worldTime.storyDay > previousStoryDay && previousStoryDay > 0)
            RemiMemoryDaySettlement.NotifyStoryDayClosed(previousStoryDay);

        return worldTime;
    }

    /// <summary>
    /// 提交故事锚点：校验日历日后写入关系档（只升不降）。
    /// Demo：教室→Surface，图书馆→Relational，公寓→Influential。
    /// </summary>
    public bool OnAnchorCommitted(RemiStoryAnchorId anchorId)
    {
        if (!RemiStoryAnchorCatalog.TryGetCommitSpec(
                anchorId,
                out RemiDialogueDepthStage targetStage,
                out int requiredStoryDay,
                out RemiStoryAnchorFlags flag))
        {
            Debug.LogWarning($"[RemiPresence] OnAnchorCommitted: 未知锚点 {anchorId}");
            return false;
        }

        int day = Mathf.Max(0, worldTime.storyDay);
        if (day != requiredStoryDay)
        {
            Debug.LogWarning(
                $"[RemiPresence] OnAnchorCommitted({anchorId}) 日历不符：需要 Day{requiredStoryDay}，当前 Day{day}");
            return false;
        }

        bool already = (committedStoryAnchors & flag) != 0;
        committedStoryAnchors |= flag;

        RemiDialogueDepthStage oldStage = dialogueDepthStage;
        if (targetStage > dialogueDepthStage)
            dialogueDepthStage = targetStage;
        else if (targetStage == RemiDialogueDepthStage.Surface &&
                 dialogueDepthStage == RemiDialogueDepthStage.Surface)
        {
            // Day1：确认 Surface，无升档。
        }

        if (dialogueDepthStage > oldStage)
        {
            RemiRhythmBeatPlayer.TryPlayStageAdvance(oldStage, dialogueDepthStage, null, ref playedRhythmBeats);
            EnsureMomentsService();
            RemiMomentsService.Instance?.NotifyStageAdvanced(dialogueDepthStage);
        }
        else if (!already && anchorId == RemiStoryAnchorId.Day1ClassroomOpening)
        {
            // Surface 确认时仍刷新 Prompt / Moments 同步。
            EnsureMomentsService();
            RemiMomentsService.Instance?.NotifyStageAdvanced(dialogueDepthStage);
        }

        SaveRhythmProgress();
        PushToPromptContext();
        Debug.Log(
            $"[RemiPresence] AnchorCommitted {anchorId} → Stage={dialogueDepthStage} anchors={committedStoryAnchors}");

        // 锚点与共同经历对齐：避免只升档未写 Memory 导致 Ending 回顾缺页
        if (anchorId == RemiStoryAnchorId.Day2LibraryCoPresence)
            RecordSharedExperience(RemiSharedExperienceId.Day2LibraryCoPresence);
        else if (anchorId == RemiStoryAnchorId.Day3ApartmentCoPresence)
            RecordSharedExperience(RemiSharedExperienceId.Day3DormDeviation);

        return true;
    }

    private void SyncPhaseFromWorldTime()
    {
        if (currentPhase == worldTime.phase)
            return;

        RemiPhaseEpisodeKind ended = currentEpisodeKind;
        currentPhase = worldTime.phase;
        ClearTrackOverride(restoreDefaultEpisode: true);
        if (ended != RemiPhaseEpisodeKind.Default)
            PhaseEpisodeEnded?.Invoke(ended, RemiEpisodeEndReason.PhaseAdvanced);
    }

    private void LoadWorldTime()
    {
        if (!persistWorldTime) return;
        if (!PlayerPrefs.HasKey(PrefsWorldTime))
        {
            worldTime = RemiWorldTime.BeforeStory;
            if (storyDayStarted)
            {
                worldTime.storyDay = 1;
                worldTime.phase = currentPhase;
                worldTime.beat = Mathf.Max(1, worldTime.beat);
            }
            return;
        }

        string json = PlayerPrefs.GetString(PrefsWorldTime, string.Empty);
        if (string.IsNullOrEmpty(json))
            return;
        try
        {
            RemiWorldTime loaded = JsonUtility.FromJson<RemiWorldTime>(json);
            worldTime = loaded;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RemiPresence] LoadWorldTime failed: {e.Message}");
        }
    }

    private void SaveWorldTime()
    {
        if (!persistWorldTime) return;
        PlayerPrefs.SetString(PrefsWorldTime, JsonUtility.ToJson(worldTime));
        PlayerPrefs.Save();
    }

    /// <summary>日起点存档前：把内存状态刷到 PlayerPrefs。</summary>
    public void FlushPersistedStateForSave()
    {
        SaveRhythmProgress();
        SaveDayBlockProgress();
        SaveWorldTime();
    }

    /// <summary>读档后：从 PlayerPrefs 重载时间与节奏（不清 Archive）。</summary>
    public void ReloadPersistedState()
    {
        LoadRhythmProgress();
        LoadDayBlockProgress();
        LoadWorldTime();
        SyncPhaseFromWorldTime();
        ApplyScheduleForPhase(currentPhase, clearOverride: true);
        PushToPromptContext();
        DeepSeekDialogueManager.Instance?.ResetPromptLogTracking();
    }

    /// <summary>委托类里程碑完成（过程记账）；状态 delta 仍走 <see cref="ApplyCommissionEvent"/>。</summary>
    public void RecordDelegationMilestone(RemiPresenceEventKind eventKind)
    {
        if (delegationProgress == null)
            delegationProgress = new RemiDelegationProgress();
        if (!delegationProgress.TryRecord(eventKind))
            return;
        SaveRhythmProgress();
    }

    /// <summary>已弃用：关系档改由 <see cref="OnAnchorCommitted"/> 写入；保留供调试刷新 Prompt。</summary>
    public void RecalculateDialogueDepthStage()
    {
        SaveRhythmProgress();
        PushToPromptContext();
    }

    private void LoadRhythmProgress()
    {
        if (!persistRhythmProgress) return;
        storyDayStarted = PlayerPrefs.GetInt(PrefsRhythmStory, 0) != 0;
        delegationProgress ??= new RemiDelegationProgress();
        delegationProgress.DeserializeFromPrefs(PlayerPrefs.GetString(PrefsRhythmDelegations, string.Empty));
        MigrateLegacyBookQuestPref();
        playedRhythmBeats = (RemiRhythmBeatFlags)PlayerPrefs.GetInt(PrefsRhythmBeats, 0);
        committedStoryAnchors = (RemiStoryAnchorFlags)PlayerPrefs.GetInt(PrefsRhythmAnchors, 0);
        if (PlayerPrefs.HasKey(PrefsRhythmDepthStage))
            dialogueDepthStage = (RemiDialogueDepthStage)Mathf.Clamp(
                PlayerPrefs.GetInt(PrefsRhythmDepthStage, 0), 0, 2);
        else
            MigrateDepthStageFromLegacyAnchors();
        LoadDayBlockProgress();
    }

    /// <summary>旧档无 DepthStage prefs：用共现/交书痕迹粗迁移（仅抬档，不瞎降）。</summary>
    private void MigrateDepthStageFromLegacyAnchors()
    {
        if ((committedStoryAnchors & RemiStoryAnchorFlags.Day3ApartmentCoPresence) != 0)
            dialogueDepthStage = RemiDialogueDepthStage.Influential;
        else if ((committedStoryAnchors & RemiStoryAnchorFlags.Day2LibraryCoPresence) != 0 ||
                 HasLibraryCoPresenceCompleteForGate)
        {
            dialogueDepthStage = RemiDialogueDepthStage.Relational;
            committedStoryAnchors |= RemiStoryAnchorFlags.Day2LibraryCoPresence;
        }
        else if (storyDayStarted ||
                 (committedStoryAnchors & RemiStoryAnchorFlags.Day1ClassroomOpening) != 0)
        {
            dialogueDepthStage = RemiDialogueDepthStage.Surface;
            committedStoryAnchors |= RemiStoryAnchorFlags.Day1ClassroomOpening;
        }
        else
            dialogueDepthStage = RemiDialogueDepthStage.Surface;
    }

    private void MigrateLegacyBookQuestPref()
    {
        if (!PlayerPrefs.HasKey(PrefsRhythmBookLegacy)) return;
        if (PlayerPrefs.GetInt(PrefsRhythmBookLegacy, 0) != 0)
            delegationProgress.TryRecord(RemiPresenceEventKind.PlayerSubmittedBook);
        PlayerPrefs.DeleteKey(PrefsRhythmBookLegacy);
        PlayerPrefs.Save();
    }

    private void SaveRhythmProgress()
    {
        if (!persistRhythmProgress) return;
        PlayerPrefs.SetInt(PrefsRhythmStory, storyDayStarted ? 1 : 0);
        PlayerPrefs.SetString(PrefsRhythmDelegations, delegationProgress?.SerializeForPrefs() ?? string.Empty);
        PlayerPrefs.SetInt(PrefsRhythmBeats, (int)playedRhythmBeats);
        PlayerPrefs.SetInt(PrefsRhythmDepthStage, (int)dialogueDepthStage);
        PlayerPrefs.SetInt(PrefsRhythmAnchors, (int)committedStoryAnchors);
        PlayerPrefs.Save();
        SaveDayBlockProgress();
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Clear Rhythm PlayerPrefs")]
    private void Editor_ClearRhythmPrefs()
    {
        PlayerPrefs.DeleteKey(PrefsRhythmStory);
        PlayerPrefs.DeleteKey(PrefsRhythmDelegations);
        PlayerPrefs.DeleteKey(PrefsRhythmBookLegacy);
        PlayerPrefs.DeleteKey(PrefsRhythmBeats);
        PlayerPrefs.DeleteKey(PrefsRhythmDepthStage);
        PlayerPrefs.DeleteKey(PrefsRhythmAnchors);
        PlayerPrefs.DeleteKey(PrefsDayBlockSlot);
        PlayerPrefs.DeleteKey(PrefsDayBlockKind);
        PlayerPrefs.DeleteKey(PrefsDayBlockInAnchor);
        PlayerPrefs.Save();
        storyDayStarted = false;
        delegationProgress?.Clear();
        playedRhythmBeats = RemiRhythmBeatFlags.None;
        committedStoryAnchors = RemiStoryAnchorFlags.None;
        dialogueDepthStage = RemiDialogueDepthStage.Surface;
        currentDayBlockSlot = RemiDayBlockSlot.A;
        currentDayBlockKind = RemiDayBlockKind.None;
        dayBlockInAnchor = false;
        worldTime = RemiWorldTime.BeforeStory;
        PlayerPrefs.DeleteKey(PrefsWorldTime);
        RemiSharedExperienceMemory.Instance?.ClearAll();
        Debug.Log("[RemiPresence] Rhythm PlayerPrefs cleared.");
    }
#endif

    /// <summary>调试：重置委托进度与节奏（不删 PlayerPrefs，除非另调 Editor_ClearRhythmPrefs）。</summary>
    public void ResetRelationshipStateForDebug()
    {
        delegationProgress ??= new RemiDelegationProgress();
        delegationProgress.Clear();
        storyDayStarted = false;
        playedRhythmBeats = RemiRhythmBeatFlags.None;
        committedStoryAnchors = RemiStoryAnchorFlags.None;
        dialogueDepthStage = RemiDialogueDepthStage.Surface;
        currentDayBlockSlot = RemiDayBlockSlot.A;
        currentDayBlockKind = RemiDayBlockKind.None;
        dayBlockInAnchor = false;
        PlayerPrefs.DeleteKey(PrefsDayBlockSlot);
        PlayerPrefs.DeleteKey(PrefsDayBlockKind);
        PlayerPrefs.DeleteKey(PrefsDayBlockInAnchor);
        worldTime = RemiWorldTime.BeforeStory;
        useScheduleOverride = false;
        currentEpisodeKind = RemiPhaseEpisodeKind.Default;
        episodeOccupiesPhase = false;
        ApplyScheduleForPhase(currentPhase, clearOverride: true);
        RemiSharedExperienceMemory.Instance?.ClearAll();
        SaveRhythmProgress();
        SaveWorldTime();
        PushToPromptContext();
        DeepSeekDialogueManager.Instance?.ResetPromptLogTracking();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetInteractionChannel(RemiInteractionChannel channel)
    {
        currentChannel = channel;
    }

    /// <summary>剧情结束、调试或系统推进时调用。</summary>
    public void SetDayPhase(RemiDayPhase phase)
    {
        if (phase != worldTime.phase)
            AdvanceWorldTime(RemiTimeAdvanceReason.PhaseChanged, phase);
        else
        {
            currentPhase = phase;
            ClearTrackOverride(restoreDefaultEpisode: true);
        }
    }


    /// <summary>进入指定叙事块；可选同步 RemiDayPhase（灯光/旧日程兼容）。</summary>
    public void EnterDayBlock(RemiDayBlockSlot slot, bool syncPhaseHint = true, bool enterAnchor = false)
    {
        int day = worldTime.IsStoryStarted ? worldTime.storyDay : 0;
        if (!RemiDayBlockCatalog.TryGetDef(day, slot, out RemiDayBlockDef def))
        {
            Debug.LogWarning($"[RemiPresence] EnterDayBlock: no plan for day={day} slot={slot}");
            return;
        }

        currentDayBlockSlot = slot;
        dayBlockInAnchor = enterAnchor;
        currentDayBlockKind = enterAnchor ? RemiDayBlockKind.Anchor : def.kind;

        // Return 仅 Day3 计划允许
        if (currentDayBlockKind == RemiDayBlockKind.Return && !RemiDayBlockCatalog.PlanAllowsReturn(day))
            currentDayBlockKind = RemiDayBlockKind.Aftermath;

        if (syncPhaseHint && worldTime.phase != def.phaseHint)
            SetDayPhase(def.phaseHint);

        SaveDayBlockProgress();
        PushToPromptContext();
        Debug.Log($"[RemiPresence] DayBlock → day{day} {RemiDayBlockCatalog.SlotKey(slot)}/{RemiDayBlockCatalog.KindKey(currentDayBlockKind)} anchor={dayBlockInAnchor}");
    }

    /// <summary>B 块内：从 Window 进入 Anchor（委托/共现/偏离进行中）。</summary>
    public void EnterDayBlockAnchor()
    {
        if (currentDayBlockSlot != RemiDayBlockSlot.B && currentDayBlockKind != RemiDayBlockKind.Window)
            EnterDayBlock(RemiDayBlockSlot.B, syncPhaseHint: true, enterAnchor: true);
        else
        {
            dayBlockInAnchor = true;
            currentDayBlockKind = RemiDayBlockKind.Anchor;
            SaveDayBlockProgress();
            PushToPromptContext();
        }
    }

    /// <summary>进入 C 块：Day3=Return，其它日=Aftermath。</summary>
    public void EnterDayBlockClosing()
    {
        EnterDayBlock(RemiDayBlockSlot.C, syncPhaseHint: true, enterAnchor: false);
    }

    /// <summary>新叙事日开始时落到 A·Routine。</summary>
    public void ResetDayBlockForStoryDay(int storyDay)
    {
        if (storyDay <= 0)
        {
            currentDayBlockSlot = RemiDayBlockSlot.A;
            currentDayBlockKind = RemiDayBlockKind.None;
            dayBlockInAnchor = false;
            SaveDayBlockProgress();
            return;
        }

        // 确保 worldTime.storyDay 已更新后再 Enter
        EnterDayBlock(RemiDayBlockSlot.A, syncPhaseHint: true, enterAnchor: false);
    }

    private void SaveDayBlockProgress()
    {
        PlayerPrefs.SetInt(PrefsDayBlockSlot, (int)currentDayBlockSlot);
        PlayerPrefs.SetInt(PrefsDayBlockKind, (int)currentDayBlockKind);
        PlayerPrefs.SetInt(PrefsDayBlockInAnchor, dayBlockInAnchor ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void LoadDayBlockProgress()
    {
        if (!PlayerPrefs.HasKey(PrefsDayBlockSlot))
        {
            currentDayBlockSlot = RemiDayBlockSlot.A;
            currentDayBlockKind = RemiDayBlockKind.None;
            dayBlockInAnchor = false;
            return;
        }

        currentDayBlockSlot = (RemiDayBlockSlot)Mathf.Clamp(PlayerPrefs.GetInt(PrefsDayBlockSlot, 0), 0, 2);
        currentDayBlockKind = (RemiDayBlockKind)Mathf.Clamp(PlayerPrefs.GetInt(PrefsDayBlockKind, 0), 0, 5);
        dayBlockInAnchor = PlayerPrefs.GetInt(PrefsDayBlockInAnchor, 0) != 0;
    }

    public void AdvanceDayPhase(bool wrap = true)
    {
        int next = (int)worldTime.phase + 1;
        if (wrap && next > (int)RemiDayPhase.Night)
            AdvanceWorldTime(RemiTimeAdvanceReason.NextDay);
        else
        {
            next = Mathf.Min(next, (int)RemiDayPhase.Night);
            SetDayPhase((RemiDayPhase)next);
        }
    }

    public void ApplyScheduleForPhase(RemiDayPhase phase, bool clearOverride)
    {
        if (clearOverride)
            ClearTrackOverride(restoreDefaultEpisode: false);

        RemiScheduleSlot slot = FindSlot(phase);
        if (slot == null) return;
    }

    /// <summary>
    /// Day1 教室找书窗口/委托：把日程地点钉在教室，避免共位判定把面对面关掉。
    /// 不记 DeviationSession（不是剧情偏离）。
    /// </summary>
    public void PinClassroomForDay1Commission()
    {
        if (!worldTime.IsStoryStarted || worldTime.storyDay != 1)
            return;

        useScheduleOverride = true;
        overrideLocation = RemiLocation.Classroom;
        overrideActivity = RemiActivity.Free;
        PushToPromptContext();
    }

    /// <summary>过场用：软钉地点/活动（不写 DeviationSession）。</summary>
    public void PinLocationSoft(RemiLocation location, RemiActivity activity)
    {
        useScheduleOverride = true;
        overrideLocation = location;
        overrideActivity = activity;
        PushToPromptContext();
    }

    /// <summary>偏离轨道：剧情脊柱主动调用（如 Day3 来宿舍）；玩家闲聊不可触发。</summary>
    public void ApplyTrackDeviation(RemiLocation location, RemiActivity activity)
    {
        useScheduleOverride = true;
        overrideLocation = location;
        overrideActivity = activity;
        episodeOccupiesPhase = true;
        currentEpisodeKind = RemiPhaseEpisodeKind.DeviationSession;
        RecordSharedExperienceFromDeviation(location);
        EnterDayBlockAnchor();
        PushToPromptContext();
    }

    /// <summary>偏离轨道（整格 DeviationSession）；由 RemiDemoSpineDirector 等剧情入口调用。</summary>
    public void ApplyScheduleOverride(RemiLocation location, RemiActivity activity)
    {
        ApplyTrackDeviation(location, activity);
        ScheduleOverrideApplied?.Invoke(location, activity);
    }

    private void RecordSharedExperienceFromDeviation(RemiLocation location)
    {
        if (location == RemiLocation.Dorm)
            RecordSharedExperience(RemiSharedExperienceId.Day3DormDeviation);
    }

    public void SetPhaseEpisode(RemiPhaseEpisodeKind kind, bool occupiesPhase = false)
    {
        if (RemiPhaseEpisodeRules.IsBeatLevel(kind))
            occupiesPhase = false;
        else if (kind == RemiPhaseEpisodeKind.DeviationSession)
            occupiesPhase = true;

        currentEpisodeKind = kind;
        episodeOccupiesPhase = occupiesPhase;
        PushToPromptContext();
    }

    /// <summary>Remi 委托进行中（在轨 beat 级，不占满 phase）。</summary>
    public void BeginCommissionEpisode() => SetPhaseEpisode(RemiPhaseEpisodeKind.Commission, occupiesPhase: false);

    /// <summary>Remi 邀玩家共赴在轨日程；<paramref name="occupiesPhase"/> 为 true 时占满本时段。</summary>
    public void BeginCoPresenceEpisode(bool occupiesPhase = true) =>
        SetPhaseEpisode(RemiPhaseEpisodeKind.CoPresence, occupiesPhase);

    /// <summary>面对面打开：Default 时记为 BeatInterlude；共现/偏离/委托保持不动。</summary>
    public void OnFaceToFaceSessionOpened()
    {
        if (currentEpisodeKind != RemiPhaseEpisodeKind.Default)
            return;
        SetPhaseEpisode(RemiPhaseEpisodeKind.BeatInterlude, occupiesPhase: false);
    }

    /// <summary>社媒远程短互动结束（关手机）；在轨且非占格 episode 时 BeatOnly+1。</summary>
    public void NotifyRemoteBeatInterludeEnded()
    {
        if (currentChannel != RemiInteractionChannel.Social)
            return;
        if (useScheduleOverride || episodeOccupiesPhase)
            return;
        if (!RemiPhaseEpisodeRules.IsBeatLevel(currentEpisodeKind))
            return;
        if (advanceBeatOnInterludeEnd)
            AdvanceWorldTime(RemiTimeAdvanceReason.BeatOnly);
    }

    /// <summary>面对面结束（回头见）。</summary>
    public void EndFaceToFaceSession(RemiEpisodeEndReason reason = RemiEpisodeEndReason.Goodbye)
    {
        switch (currentEpisodeKind)
        {
            case RemiPhaseEpisodeKind.Commission:
                return;
            // Day3 宿舍偏离占满 phase：关面板只结束本轮面对面，不提前清轨道；
            // 真正收束由门口离开 / EnterDayBlockClosing 负责。
            case RemiPhaseEpisodeKind.DeviationSession:
                return;
            case RemiPhaseEpisodeKind.CoPresence:
                if (!episodeOccupiesPhase)
                    FinishNonOccupyingEpisode(reason);
                break;
            case RemiPhaseEpisodeKind.BeatInterlude:
                FinishBeatInterlude(reason);
                break;
            default:
                if (useScheduleOverride)
                    ClearTrackOverride(restoreDefaultEpisode: true);
                break;
        }
    }

    private void FinishBeatInterlude(RemiEpisodeEndReason reason)
    {
        SetPhaseEpisode(RemiPhaseEpisodeKind.Default, false);
        if (advanceBeatOnInterludeEnd)
            AdvanceWorldTime(RemiTimeAdvanceReason.BeatOnly);
        PhaseEpisodeEnded?.Invoke(RemiPhaseEpisodeKind.BeatInterlude, reason);
        PushToPromptContext();
    }

    private void FinishNonOccupyingEpisode(RemiEpisodeEndReason reason)
    {
        RemiPhaseEpisodeKind ended = currentEpisodeKind;
        SetPhaseEpisode(RemiPhaseEpisodeKind.Default, false);
        PhaseEpisodeEnded?.Invoke(ended, reason);
    }

    /// <summary>结束当前占格 episode 并清轨道偏离（若有）。不自动 AdvancePhase。</summary>
    public void EndCurrentPhaseEpisode(RemiEpisodeEndReason reason = RemiEpisodeEndReason.Goodbye)
    {
        EndPhaseEpisodeInternal(reason, advancePhase: false);
    }

    /// <summary>结束当前 phase episode 并推进到下一时段（傍晚仍回 defaultSchedule）。</summary>
    public void CompleteCurrentPhaseAndAdvance()
    {
        EndPhaseEpisodeInternal(RemiEpisodeEndReason.PhaseAdvanced, advancePhase: true);
    }

    private void EndPhaseEpisodeInternal(
        RemiEpisodeEndReason reason,
        bool advancePhase)
    {
        RemiPhaseEpisodeKind ended = currentEpisodeKind;
        bool hadOverride = useScheduleOverride;

        ClearTrackOverride(restoreDefaultEpisode: true);
        if (ended != RemiPhaseEpisodeKind.Default || hadOverride)
            PhaseEpisodeEnded?.Invoke(ended, reason);

        if (advancePhase)
            AdvanceDayPhase();
        else
            PushToPromptContext();
    }

    private void ClearTrackOverride(bool restoreDefaultEpisode)
    {
        useScheduleOverride = false;
        if (restoreDefaultEpisode)
        {
            currentEpisodeKind = RemiPhaseEpisodeKind.Default;
            episodeOccupiesPhase = false;
        }
        ApplyScheduleForPhase(currentPhase, clearOverride: false);
    }

    public RemiTrackAlignment GetTrackAlignment()
    {
        if (!useScheduleOverride)
            return RemiTrackAlignment.OnTrack;
        RemiScheduleSlot slot = FindSlot(currentPhase);
        if (slot == null)
            return RemiTrackAlignment.Deviation;
        return slot.location == overrideLocation && slot.activity == overrideActivity
            ? RemiTrackAlignment.OnTrack
            : RemiTrackAlignment.Deviation;
    }

    /// <summary>
    /// 在发起任意 LLM 请求前调用：写入 [STATE]、[POLICY] 到 <see cref="PromptContextManager"/>。
    /// </summary>
    public void PushToPromptContext(RemiStageExpressionContext? expressionContext = null)
    {
        if (PromptContextManager.Instance == null) return;

        PromptContextManager.Instance.SetDayPlanContext(RemiDayPlanBuilder.Build(this));
        PromptContextManager.Instance.SetSceneContext(RemiPromptBuilder.BuildStateBlock(this));
        PromptContextManager.Instance.SetPolicyContext(RemiPromptBuilder.BuildPolicySection(this));
        PromptContextManager.Instance.SetMemoryExperiencesContext(RemiMemoryBuilder.BuildExperiencesBlock());
    }

    /// <summary>登记共同经历（框架触发；LLM 不可写）。</summary>
    public bool RecordSharedExperience(RemiSharedExperienceId experienceId, string frameOverride = null)
    {
        RemiSharedExperienceMemory.EnsureExists();
        if (RemiSharedExperienceMemory.Instance == null)
            return false;

        if (!RemiSharedExperienceMemory.Instance.TryRecord(experienceId, worldTime, frameOverride))
            return false;

        RemiDemoRunTelemetry.EnsureExists();
        RemiDemoRunTelemetry.Instance?.RecordSharedExperienceRecorded();

        PushToPromptContext();
        Debug.Log($"[RemiPresence] Shared experience recorded: {RemiSharedExperienceCatalog.IdKey(experienceId)}");
        return true;
    }

    public string BuildWorldPromptBlock() => RemiPromptBuilder.BuildStateBlock(this);

    public string BuildAvailabilityPromptBlock() =>
        RemiPromptBuilder.BuildPolicyBlock(RemiConversationPolicy.FromService(this));

    public bool CanOpenFaceToFaceDialogue()
    {
        RemiActivity act = CurrentActivity;
        if (act == RemiActivity.Sleeping || act == RemiActivity.Cooking || act == RemiActivity.Busy)
            return false;
        return true;
    }

    public bool CanUseSocialChannelNow()
    {
        return CurrentActivity != RemiActivity.Sleeping;
    }

    public int GetSocialReplyDelaySeconds()
    {
        return CurrentActivity switch
        {
            RemiActivity.InClass => UnityEngine.Random.Range(25, 70),
            RemiActivity.Busy => UnityEngine.Random.Range(15, 45),
            RemiActivity.Cooking => UnityEngine.Random.Range(10, 35),
            RemiActivity.Sleeping => UnityEngine.Random.Range(90, 180),
            _ => UnityEngine.Random.Range(0, 8),
        };
    }

    /// <summary>
    /// <b>委托类</b>：角色托付玩家的事（找书等）。
    /// </summary>
    public void ApplyCommissionEvent(RemiPresenceEventKind eventKind) =>
        ApplyPresenceEvent(eventKind);

    public void ApplyPresenceEvent(RemiPresenceEventKind eventKind)
    {
        switch (eventKind)
        {
            case RemiPresenceEventKind.StoryClassroomOpened:
                AdvanceWorldTime(RemiTimeAdvanceReason.StoryDayBegan);
                NotifyStoryDayStarted();
                SetPhaseEpisode(RemiPhaseEpisodeKind.Default, false);
                OnAnchorCommitted(RemiStoryAnchorId.Day1ClassroomOpening);
                ResetDayBlockForStoryDay(1);
                RemiBookQuestFlow.Instance?.NotifyStoryDay1WindowStart();
                break;
            case RemiPresenceEventKind.RemiRequestedBookHelp:
                AdvanceWorldTime(RemiTimeAdvanceReason.BeatOnly);
                BeginCommissionEpisode();
                EnterDayBlock(RemiDayBlockSlot.B, syncPhaseHint: false, enterAnchor: false);
                EnterDayBlockAnchor();
                PinClassroomForDay1Commission();
                break;
            case RemiPresenceEventKind.PlayerPickedUpBook:
                AdvanceWorldTime(RemiTimeAdvanceReason.BeatOnly);
                break;
            case RemiPresenceEventKind.PlayerSubmittedBook:
                AdvanceWorldTime(RemiTimeAdvanceReason.BeatOnly);
                if (currentEpisodeKind == RemiPhaseEpisodeKind.Commission)
                    EndCurrentPhaseEpisode(RemiEpisodeEndReason.CommissionComplete);
                RecordSharedExperience(RemiSharedExperienceId.Day1CommissionBook);
                EnterDayBlockClosing();
                break;
            case RemiPresenceEventKind.PlayerApproachedFront:
                break;
        }

        if (eventKind != RemiPresenceEventKind.StoryClassroomOpened)
            RecordDelegationMilestone(eventKind);

        SaveRhythmProgress();
        // 关系档改由 OnAnchorCommitted（故事锚点）写入；委托事件只记账。
        PushToPromptContext();
    }

    private RemiScheduleSlot FindSlot(RemiDayPhase phase)
    {
        if (defaultSchedule == null) return null;
        foreach (RemiScheduleSlot s in defaultSchedule)
        {
            if (s != null && s.phase == phase)
                return s;
        }

        return null;
    }

    private RemiLocation ResolveLocation()
    {
        if (useScheduleOverride) return overrideLocation;
        RemiScheduleSlot slot = FindSlot(currentPhase);
        return slot != null ? slot.location : RemiLocation.Classroom;
    }

    private RemiActivity ResolveActivity()
    {
        if (useScheduleOverride) return overrideActivity;
        RemiScheduleSlot slot = FindSlot(currentPhase);
        return slot != null ? slot.activity : RemiActivity.Free;
    }

    private static string PhaseDisplayName(RemiDayPhase p)
    {
        return p switch
        {
            RemiDayPhase.Morning => "上午",
            RemiDayPhase.Afternoon => "下午",
            RemiDayPhase.Evening => "傍晚",
            _ => "夜间",
        };
    }

    private static string LocationDisplayName(RemiLocation l)
    {
        return l switch
        {
            RemiLocation.Classroom => "教室",
            RemiLocation.Library => "图书馆",
            _ => "宿舍",
        };
    }

    private static string ActivityDisplayName(RemiActivity a)
    {
        return a switch
        {
            RemiActivity.InClass => "上课/刚下课",
            RemiActivity.Free => "空闲",
            RemiActivity.AtDorm => "在宿舍休息",
            RemiActivity.Cooking => "做饭",
            RemiActivity.Busy => "忙碌",
            _ => "休息",
        };
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Advance Day Phase")]
    private void Editor_AdvancePhase() => AdvanceDayPhase();

    [ContextMenu("Debug/Push To Prompt Context")]
    private void Editor_Push() => PushToPromptContext();
#endif
}

/// <summary>
/// <b>委托类</b>流程中的里程碑（角色→玩家的任务进度）。一律用 ApplyCommissionEvent。
/// </summary>
public enum RemiPresenceEventKind
{
    /// <summary>世界推进：剧情结束进入上午（改时段/轨道，不是委托也不是玩家请求）。</summary>
    StoryClassroomOpened,
    RemiRequestedBookHelp,
    PlayerPickedUpBook,
    PlayerSubmittedBook,
    PlayerApproachedFront,
}

public static class RemiPresenceEventKindExtensions
{
    public static RemiPresenceFlowKind GetFlowKind(this RemiPresenceEventKind kind) =>
        RemiPresenceFlowKind.CharacterCommission;
}
