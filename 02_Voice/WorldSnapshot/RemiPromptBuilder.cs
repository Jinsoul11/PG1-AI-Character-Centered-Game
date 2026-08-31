using System.Text;
using UnityEngine;

/// <summary>
/// 将 Presence 快照组装为结构化 Prompt 段：[STATE]、[POLICY]。
/// </summary>
public static class RemiPromptBuilder
{
    public static string BuildStateBlock(RemiPresenceService service)
    {
        if (service == null)
            return string.Empty;

        RemiWorldTime wt = service.WorldTime;
        RemiDialogueDepthStage stage = service.DialogueDepthStage;

        bool canFace = service.CanOpenFaceToFaceDialogue();
        bool canSocial = service.CanUseSocialChannelNow();
        int socialDelay = service.GetSocialReplyDelaySeconds();

        var sb = new StringBuilder();
        sb.Append("world:\n");
        if (wt.IsStoryStarted)
            sb.Append($"  day: {wt.storyDay}\n");
        sb.Append($"  phase: {PhaseKey(service.CurrentPhase)}\n");
        if (wt.IsStoryStarted)
            sb.Append($"  beat: {wt.beat}\n");

        sb.Append("location:\n");
        sb.Append($"  place: {LocationKey(service.CurrentLocation)}\n");
        sb.Append($"  activity: {ActivityKey(service.CurrentActivity)}\n");

        sb.Append("episode:\n");
        sb.Append($"  type: {EpisodeKey(service.CurrentEpisodeKind)}\n");
        sb.Append($"  occupies_phase: {(service.EpisodeOccupiesPhase ? "true" : "false")}\n");
        sb.Append($"  track: {(service.TrackAlignment == RemiTrackAlignment.OnTrack ? "on_track" : "deviation")}\n");

        sb.Append("relationship:\n");
        sb.Append($"  stage: {RelationshipStageKey(stage)}\n");
        sb.Append($"  closeness: {ClosenessKey(stage)}\n");
        sb.Append($"  openness: {OpennessKey(stage)}\n");

        sb.Append("emotion:\n");
        sb.Append($"  stress: {StressKey(service.CurrentActivity)}\n");

        sb.Append("availability:\n");
        sb.Append($"  channel: {(service.CurrentChannel == RemiInteractionChannel.FaceToFace ? "face_to_face" : "social")}\n");
        sb.Append($"  face_to_face: {(canFace ? "true" : "false")}\n");
        sb.Append($"  social: {(canSocial ? "true" : "false")}\n");
        if (!canSocial)
            sb.Append($"  social_reply_delay_sec: {socialDelay}\n");

        string placeFact = RemiLocationScaffold.BuildPromptBlock(
            service.CurrentPhase,
            service.CurrentLocation,
            service.CurrentActivity,
            service.CurrentEpisodeKind,
            service.TrackAlignment);
        if (!string.IsNullOrWhiteSpace(placeFact))
        {
            sb.Append("scene_fact: ");
            sb.Append(CompactSingleLine(placeFact));
            sb.Append('\n');
        }

        if (RemiDemoSpineDirector.Instance != null)
        {
            string exhibition = RemiDemoSpineDirector.Instance.ExhibitionBackgroundFact;
            if (!string.IsNullOrWhiteSpace(exhibition))
                sb.Append($"background_exhibition: {exhibition.Trim()}\n");
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildPolicyBlock(RemiConversationPolicy policy)
    {
        var sb = new StringBuilder();
        sb.Append($"stage: {StageKey(policy.Stage)}\n");
        sb.Append($"initiative: {InitiativeKey(policy.Initiative)}\n");

        sb.Append('\n');
        sb.Append(RemiTopicScopePolicy.BuildBlock(policy.Stage));

        return sb.ToString().TrimEnd();
    }

    public static string BuildPolicySection(RemiPresenceService service)
    {
        if (service == null)
            return string.Empty;

        return "[POLICY]\n" + BuildPolicyBlock(RemiConversationPolicy.FromService(service));
    }

    /// <summary>Voice 用：[CURRENT_CONTEXT] 仅 day / phase / location / day_block。</summary>
    public static string BuildActorCurrentContextBlock(RemiPresenceService service)
    {
        if (service == null)
            return string.Empty;

        RemiWorldTime wt = service.WorldTime;
        var sb = new StringBuilder();
        if (wt.IsStoryStarted)
            sb.Append($"day: {wt.storyDay}\n");
        sb.Append($"phase: {PhaseKey(service.CurrentPhase)}\n");
        sb.Append($"location: {LocationKey(service.CurrentLocation)}\n");
        if (service.CurrentDayBlockKind != RemiDayBlockKind.None)
        {
            sb.Append("day_block: ")
                .Append(RemiDayBlockCatalog.SlotKey(service.CurrentDayBlockSlot))
                .Append('/')
                .Append(RemiDayBlockCatalog.KindKey(service.CurrentDayBlockKind));
            if (service.DayBlockInAnchor)
                sb.Append(" (anchor)");
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>Voice 用：[RELATIONSHIP] 仅关系档（topic_hint 暂不拼接）。</summary>
    public static string BuildRelationshipBlock(RemiPresenceService service)
    {
        RemiConversationPolicy policy = RemiConversationPolicy.FromService(service);
        return $"stage: {StageKey(policy.Stage)}";
    }

    private static string RelationshipStageKey(RemiDialogueDepthStage stage) =>
        stage switch
        {
            RemiDialogueDepthStage.Influential => "close",
            RemiDialogueDepthStage.Relational => "acquaintance",
            _ => "stranger",
        };

    private static int ClosenessKey(RemiDialogueDepthStage stage) =>
        stage switch
        {
            RemiDialogueDepthStage.Influential => 2,
            RemiDialogueDepthStage.Relational => 1,
            _ => 0,
        };

    private static string OpennessKey(RemiDialogueDepthStage stage) =>
        stage switch
        {
            RemiDialogueDepthStage.Influential => "high",
            RemiDialogueDepthStage.Relational => "medium",
            _ => "low",
        };

    private static string StressKey(RemiActivity activity) =>
        activity switch
        {
            RemiActivity.Busy => "high",
            RemiActivity.InClass => "medium",
            _ => "low",
        };

    private static string CompactSingleLine(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Replace('\n', ' ').Replace('\r', ' ').Trim();

    private static string StageKey(RemiDialogueDepthStage stage) =>
        stage switch
        {
            RemiDialogueDepthStage.Relational => "玩家愿意帮助你准备作品展，你对玩家的印象变好了",
            RemiDialogueDepthStage.Influential => "你和玩家在准备作品展的过程中互帮互助，关系亲密",
            _ => "你和玩家刚刚认识，关系比较陌生",
        };

    private static string InitiativeKey(RemiPromptInitiativeLevel level) =>
        level switch
        {
            RemiPromptInitiativeLevel.High => "high",
            RemiPromptInitiativeLevel.Low => "low",
            _ => "medium",
        };

    private static string PhaseKey(RemiDayPhase phase) =>
        phase switch
        {
            RemiDayPhase.Morning => "morning",
            RemiDayPhase.Afternoon => "afternoon",
            RemiDayPhase.Evening => "evening",
            _ => "night",
        };

    private static string LocationKey(RemiLocation location) =>
        location switch
        {
            RemiLocation.Classroom => "classroom",
            RemiLocation.Library => "library",
            _ => "dorm",
        };

    private static string ActivityKey(RemiActivity activity) =>
        activity switch
        {
            RemiActivity.InClass => "in_class",
            RemiActivity.Free => "free",
            RemiActivity.AtDorm => "at_dorm",
            RemiActivity.Cooking => "cooking",
            RemiActivity.Busy => "busy",
            _ => "sleeping",
        };

    private static string EpisodeKey(RemiPhaseEpisodeKind episode) =>
        episode switch
        {
            RemiPhaseEpisodeKind.Commission => "commission",
            RemiPhaseEpisodeKind.CoPresence => "co_presence",
            RemiPhaseEpisodeKind.BeatInterlude => "beat_interlude",
            RemiPhaseEpisodeKind.DeviationSession => "deviation_session",
            _ => "default",
        };
}
