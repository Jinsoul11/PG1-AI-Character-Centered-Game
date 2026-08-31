using UnityEngine;

/// <summary>
/// 验证 Presence / 委托里程碑 / 节奏 Gate 用。挂到场景空物体，Play 后看 Inspector 按钮与 Console。
/// </summary>
[DisallowMultipleComponent]
public class RemiPresenceDebugProbe : MonoBehaviour
{
    [Header("委托里程碑（无需 LLM）")]
    [SerializeField] private RemiPresenceEventKind testCommissionEvent = RemiPresenceEventKind.PlayerPickedUpBook;

    [ContextMenu("Log / Presence Snapshot")]
    public void LogSnapshot()
    {
        RemiPresenceService p = RemiPresenceService.Instance;
        if (p == null)
        {
            Debug.LogWarning("[RemiPresenceDebug] 场景中没有 RemiPresenceService。");
            return;
        }

        RemiRhythmGateSnapshot g = p.GateSnapshot;
        bool canEnterDay2Trunk = RemiRhythmGateEvaluator.IsRelationalGateOpen(g, null);
        bool canEnterDay3Trunk = RemiRhythmGateEvaluator.IsInfluentialGateOpen(g, null);
        Debug.Log(
            $"[RemiPresenceDebug] Stage={p.DialogueDepthStage} Anchors={p.CommittedStoryAnchors} " +
            $"Gate EnterD2={canEnterDay2Trunk} EnterD3={canEnterDay3Trunk} Beats={p.PlayedRhythmBeats} " +
            $"WorldTime day={p.WorldTime.storyDay} phase={p.WorldTime.phase} beat={p.WorldTime.beat} " +
            $"Track={p.TrackAlignment} Episode={p.CurrentEpisodeKind} occupiesPhase={p.EpisodeOccupiesPhase} " +
            $"loc={p.CurrentLocation} act={p.CurrentActivity} override={p.IsScheduleOverridden} " +
            $"bookDone={p.HasBookCommissionCompleteForGate} libCoPresence={p.HasLibraryCoPresenceCompleteForGate} " +
            $"day3Gate={p.HasInfluentialDelegationGate} story={p.StoryDayStarted}");
    }

    [ContextMenu("Apply Commission Event")]
    public void TestCommissionEvent()
    {
        RemiPresenceService.Instance?.ApplyCommissionEvent(testCommissionEvent);
        LogSnapshot();
    }

    [ContextMenu("Recalculate Dialogue Stage")]
    public void TestRecalcStage()
    {
        RemiPresenceService.Instance?.RecalculateDialogueDepthStage();
        LogSnapshot();
    }

    [ContextMenu("Simulate Delegation: Book Submitted")]
    public void TestDelegationBookSubmitted()
    {
        RemiPresenceService.Instance?.ApplyCommissionEvent(RemiPresenceEventKind.PlayerSubmittedBook);
        LogSnapshot();
    }

    [ContextMenu("Simulate Story Day Started")]
    public void TestStoryStarted()
    {
        RemiPresenceService.Instance?.NotifyStoryDayStarted();
        RemiPresenceService.Instance?.RecalculateDialogueDepthStage();
        LogSnapshot();
    }

    [ContextMenu("Simulate Anchor: Day2 Library")]
    public void TestAnchorDay2Library()
    {
        RemiPresenceService.Instance?.OnAnchorCommitted(RemiStoryAnchorId.Day2LibraryCoPresence);
        LogSnapshot();
    }

    [ContextMenu("Simulate Anchor: Day3 Apartment")]
    public void TestAnchorDay3Apartment()
    {
        RemiPresenceService.Instance?.OnAnchorCommitted(RemiStoryAnchorId.Day3ApartmentCoPresence);
        LogSnapshot();
    }

    [ContextMenu("Clear Rhythm PlayerPrefs (Presence)")]
    public void TestClearRhythmPrefs()
    {
        PlayerPrefs.DeleteKey("RemiRhythm_StoryStarted");
        PlayerPrefs.DeleteKey("RemiRhythm_Delegations");
        PlayerPrefs.DeleteKey("RemiRhythm_BookDone");
        PlayerPrefs.DeleteKey("RemiRhythm_PlayedBeats");
        PlayerPrefs.DeleteKey("RemiRhythm_DepthStage");
        PlayerPrefs.DeleteKey("RemiRhythm_StoryAnchors");
        PlayerPrefs.Save();
        Debug.Log("[RemiPresenceDebug] Rhythm prefs cleared; reload scene or restart play.");
    }

    [ContextMenu("Advance Day Phase")]
    public void TestAdvancePhase()
    {
        RemiPresenceService.Instance?.AdvanceDayPhase();
        LogSnapshot();
    }

    [ContextMenu("World Time/Advance Phase")]
    public void TestAdvanceWorldPhase()
    {
        RemiPresenceService.Instance?.AdvanceDayPhase();
        LogSnapshot();
    }

    [ContextMenu("Lighting/Apply Current Phase To Scene")]
    public void TestApplySceneLighting()
    {
        RemiSceneDayPhaseLighting profile =
#if UNITY_2023_1_OR_NEWER
            Object.FindFirstObjectByType<RemiSceneDayPhaseLighting>(FindObjectsInactive.Exclude);
#else
            Object.FindObjectOfType<RemiSceneDayPhaseLighting>();
#endif
        if (profile == null)
        {
            Debug.LogWarning("[RemiPresenceDebug] 当前场景无 RemiSceneDayPhaseLighting。");
            return;
        }

        RemiDayPhase phase = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.WorldTime.phase
            : RemiDayPhase.Morning;
        profile.ApplyPhase(phase);
        Debug.Log($"[RemiPresenceDebug] 已应用场景光照：{phase}");
    }

    [ContextMenu("World Time/Advance Beat Only")]
    public void TestAdvanceWorldBeat()
    {
        RemiPresenceService.Instance?.AdvanceWorldTime(RemiTimeAdvanceReason.BeatOnly);
        LogSnapshot();
    }

    [ContextMenu("Episode/End Current Phase Episode")]
    public void TestEndPhaseEpisode()
    {
        RemiPresenceService.Instance?.EndCurrentPhaseEpisode(RemiEpisodeEndReason.Goodbye);
        LogSnapshot();
    }

    [ContextMenu("Episode/Complete Phase And Advance")]
    public void TestCompletePhaseAndAdvance()
    {
        RemiPresenceService.Instance?.CompleteCurrentPhaseAndAdvance();
        LogSnapshot();
    }

    [ContextMenu("Episode/Begin Co-Presence (on-track)")]
    public void TestBeginCoPresence()
    {
        RemiPresenceService.Instance?.BeginCoPresenceEpisode(occupiesPhase: true);
        LogSnapshot();
    }

    [ContextMenu("Episode/Begin Co-Presence (beat-level)")]
    public void TestBeginCoPresenceLight()
    {
        RemiPresenceService.Instance?.BeginCoPresenceEpisode(occupiesPhase: false);
        LogSnapshot();
    }

    [ContextMenu("Episode/Begin Commission")]
    public void TestBeginCommission()
    {
        RemiPresenceService.Instance?.BeginCommissionEpisode();
        LogSnapshot();
    }

    [ContextMenu("Open Phone App (Contact)")]
    public void TestOpenSocialChat()
    {
        PhoneAppPanel.Open();
    }

    [ContextMenu("Open Phone → Moments")]
    public void TestOpenSocialMoments()
    {
        PhoneAppPanel.OpenMoments();
    }

    [ContextMenu("Moments/Sync Publish For Current Stage")]
    public void TestSyncMoments()
    {
        RemiMomentsService.Instance?.SyncForCurrentStage(force: true);
    }

    [ContextMenu("Memory/Toggle Debug Overlay (F9)")]
    public void TestToggleMemoryDebugOverlay()
    {
        RemiDemoMemoryDebugOverlay.EnsureExists();
        RemiDemoMemoryDebugOverlay.Instance?.ToggleVisible();
    }

    [ContextMenu("Memory/Refresh Debug Overlay")]
    public void TestRefreshMemoryDebugOverlay()
    {
        RemiDemoMemoryDebugOverlay.EnsureExists();
        RemiDemoMemoryDebugOverlay.Instance?.RefreshAll();
    }

    [ContextMenu("SendSystem/Toggle Director Debug (F8)")]
    public void TestToggleSendSystemDirectorDebug()
    {
        RemiSendSystemDebugDirector.EnsureExists();
        RemiSendSystemDebugDirector.Instance?.ToggleVisible();
    }

    [ContextMenu("SendSystem/Fire Test With Debug Text")]
    public void TestFireSendSystemWithDebugText()
    {
        RemiSendSystemDebugDirector.EnsureExists();
        RemiSendSystemDebugDirector.Instance?.FireTestSendSystemNow();
    }

    [ContextMenu("SendSystem/Ensure Content Manager")]
    public void TestEnsureSendSystemContentManager()
    {
        RemiSendSystemContentManager.EnsureExists();
        Debug.Log(
            RemiSendSystemContentManager.Instance != null
                ? "[RemiPresenceDebug] RemiSendSystemContentManager ready — edit its Inspector texts."
                : "[RemiPresenceDebug] RemiSendSystemContentManager missing.");
    }
}
