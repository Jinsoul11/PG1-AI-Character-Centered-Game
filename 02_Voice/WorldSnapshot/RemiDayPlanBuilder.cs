using System.Text;
using UnityEngine;

/// <summary>
/// 生成 Prompt [DAY_PLAN]：当日既定轨道（只读事实；过去可即兴，计划内未来不可编造）。
/// </summary>
public static class RemiDayPlanBuilder
{
    public static string Build(RemiPresenceService service)
    {
        if (service == null)
            return string.Empty;

        RemiWorldTime wt = service.WorldTime;
        if (!wt.IsStoryStarted)
            return string.Empty;

        RemiDayPhase currentPhase = wt.phase;
        int currentIndex = (int)currentPhase;
        bool dayDeviation = service.IsScheduleOverridden &&
                            service.CurrentEpisodeKind == RemiPhaseEpisodeKind.DeviationSession;

        var sb = new StringBuilder();
        sb.Append("readonly: true\n");
        sb.Append("rule: 不得编造与下方 plan 冲突的既定安排；提及今日/稍后计划时引用本段。\n");
        sb.Append($"day: {wt.storyDay}\n");
        sb.Append($"track: {(dayDeviation ? "deviation" : "on_track")}\n");
        sb.Append("phases:\n");

        AppendPhase(sb, service, RemiDayPhase.Morning, currentIndex, wt.storyDay);
        AppendPhase(sb, service, RemiDayPhase.Afternoon, currentIndex, wt.storyDay);
        AppendPhase(sb, service, RemiDayPhase.Evening, currentIndex, wt.storyDay);
        AppendPhase(sb, service, RemiDayPhase.Night, currentIndex, wt.storyDay);

        return sb.ToString().TrimEnd();
    }

    private static void AppendPhase(
        StringBuilder sb,
        RemiPresenceService service,
        RemiDayPhase phase,
        int currentPhaseIndex,
        int storyDay)
    {
        RemiScheduleSlot slot = service.GetScheduleSlot(phase);
        if (slot == null)
            return;

        int phaseIndex = (int)phase;
        string status = ResolvePhaseStatus(service, phase, phaseIndex, currentPhaseIndex);
        string plan = BuildPlanText(service, slot, phase, storyDay, status);

        sb.Append($"  {PhaseKey(phase)}:\n");
        sb.Append($"    location: {LocationKey(slot.location)}\n");
        sb.Append($"    activity: {ActivityKey(slot.activity)}\n");
        sb.Append($"    plan: {plan}\n");
        sb.Append($"    status: {status}\n");

        if (status == "overridden")
            AppendOverrideActual(sb, service);

        string episode = ResolveEpisodeTag(service, phase, phaseIndex, currentPhaseIndex);
        if (!string.IsNullOrEmpty(episode))
            sb.Append($"    episode: {episode}\n");
    }

    private static string ResolvePhaseStatus(
        RemiPresenceService service,
        RemiDayPhase phase,
        int phaseIndex,
        int currentPhaseIndex)
    {
        if (phaseIndex > currentPhaseIndex)
            return "planned";

        if (phaseIndex < currentPhaseIndex)
            return "completed";

        if (service.IsScheduleOverridden &&
            service.CurrentEpisodeKind == RemiPhaseEpisodeKind.DeviationSession)
            return "overridden";

        return "current";
    }

    private static void AppendOverrideActual(StringBuilder sb, RemiPresenceService service)
    {
        sb.Append($"    actual_location: {LocationKey(service.OverrideLocation)}\n");
        sb.Append($"    actual_activity: {ActivityKey(service.OverrideActivity)}\n");
    }

    private static string ResolveEpisodeTag(
        RemiPresenceService service,
        RemiDayPhase phase,
        int phaseIndex,
        int currentPhaseIndex)
    {
        if (phaseIndex != currentPhaseIndex)
            return string.Empty;

        return EpisodeKey(service.CurrentEpisodeKind);
    }

    private static string BuildPlanText(
        RemiPresenceService service,
        RemiScheduleSlot slot,
        RemiDayPhase phase,
        int storyDay,
        string status)
    {
        string baseNote = string.IsNullOrWhiteSpace(slot.scheduleNote)
            ? string.Empty
            : slot.scheduleNote.Trim();

        string annotation = GetDayPhaseAnnotation(storyDay, phase, service);
        if (string.IsNullOrEmpty(annotation))
            return string.IsNullOrEmpty(baseNote) ? "—" : baseNote;

        if (string.IsNullOrEmpty(baseNote))
            return annotation;

        return baseNote + "；" + annotation;
    }

    /// <summary>Demo / 剧情对默认轨道的当日注解（游戏线事实，非 LLM 即兴）。</summary>
    private static string GetDayPhaseAnnotation(int storyDay, RemiDayPhase phase, RemiPresenceService service)
    {
        RemiDemoSpineBeat spine = RemiDemoSpineDirector.Instance != null
            ? RemiDemoSpineDirector.Instance.CurrentBeat
            : RemiDemoSpineBeat.NotStarted;

        if (storyDay == 1 && phase == RemiDayPhase.Morning)
        {
            if (service.CurrentEpisodeKind == RemiPhaseEpisodeKind.Commission ||
                spine <= RemiDemoSpineBeat.Day1BookSubmitted)
                return "作品展筹备；可能请玩家帮忙找参考书《AI游戏入门》";
        }

        if (storyDay == 2 && phase == RemiDayPhase.Morning)
        {
            if (spine >= RemiDemoSpineBeat.Day1Complete)
                return "Remi 不在教室（已在图书馆）；玩家会在教室看到空座位";
        }

        if (storyDay == 2 && phase == RemiDayPhase.Afternoon)
        {
            if (spine >= RemiDemoSpineBeat.Day2InviteDelivered)
                return "图书馆自习查展资料；共现邀请；已发消息请玩家下午来";
            return "图书馆自习查展资料";
        }

        if (storyDay == 3 && phase == RemiDayPhase.Afternoon)
        {
            if (spine >= RemiDemoSpineBeat.Day3DeviationAccepted)
                return "原计划在图书馆；已答应破例改安排（见 actual）";
        }

        return string.Empty;
    }

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
            _ => string.Empty,
        };
}
