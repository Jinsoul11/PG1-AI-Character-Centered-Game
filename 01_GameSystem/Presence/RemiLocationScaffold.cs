using System.Text;

/// <summary>
/// 地点×时段×Episode 的 Prompt 事实脚手架（动态 A）。
/// </summary>
public static class RemiLocationScaffold
{
    public static string BuildPromptBlock(
        RemiDayPhase phase,
        RemiLocation location,
        RemiActivity activity,
        RemiPhaseEpisodeKind episode,
        RemiTrackAlignment track)
    {
        string phaseName = RemiWorldTimeFormat.PhaseShortName(phase);
        string locName = LocationShortName(location);
        string actName = ActivityShortName(activity);

        var sb = new StringBuilder();
        sb.Append("【场景事实】\n");
        sb.Append($"{phaseName} · {locName} · {actName}。");

        string episodeLine = EpisodeFactLine(episode, track);
        if (!string.IsNullOrEmpty(episodeLine))
            sb.Append('\n').Append(episodeLine);

        string placeLine = PlaceFactLine(phase, location, episode, track);
        if (!string.IsNullOrEmpty(placeLine))
            sb.Append('\n').Append(placeLine);

        return sb.ToString().TrimEnd();
    }

    private static string EpisodeFactLine(RemiPhaseEpisodeKind episode, RemiTrackAlignment track)
    {
        return episode switch
        {
            RemiPhaseEpisodeKind.Commission => "Episode 事实：角色委托进行中。",
            RemiPhaseEpisodeKind.CoPresence => "Episode 事实：共现邀请，仍在原轨日程。",
            RemiPhaseEpisodeKind.BeatInterlude => "Episode 事实：原轨 beat 插曲，不改地点、不占满时段。",
            RemiPhaseEpisodeKind.DeviationSession => "Episode 事实：玩家邀请导致的偏离，占满当前时段。",
            _ => track == RemiTrackAlignment.Deviation
                ? "Episode 事实：当前为临时偏离，非日常轨道。"
                : string.Empty,
        };
    }

    private static string PlaceFactLine(
        RemiDayPhase phase,
        RemiLocation location,
        RemiPhaseEpisodeKind episode,
        RemiTrackAlignment track)
    {
        if (phase == RemiDayPhase.Afternoon && location == RemiLocation.Library)
            return "地点事实：图书馆自习。";

        if (phase == RemiDayPhase.Afternoon && location == RemiLocation.Classroom)
        {
            if (track == RemiTrackAlignment.Deviation || episode == RemiPhaseEpisodeKind.DeviationSession)
                return "地点事实：下午原轨为图书馆，现位于教室（偏离）。";
            return "地点事实：下午教室，可能刚下课或自习。";
        }

        if (phase == RemiDayPhase.Morning && location == RemiLocation.Classroom)
            return "地点事实：上午教室，有课表感。";

        if (phase == RemiDayPhase.Evening && location == RemiLocation.Dorm)
            return "地点事实：傍晚宿舍，可能做饭或休息。";

        if (phase == RemiDayPhase.Night && location == RemiLocation.Dorm)
            return "地点事实：夜间宿舍休息。";

        return location switch
        {
            RemiLocation.Classroom => "地点事实：教室，半公开。",
            RemiLocation.Library => "地点事实：图书馆，安静。",
            RemiLocation.Dorm => "地点事实：宿舍，私人空间。",
            _ => string.Empty,
        };
    }

    private static string LocationShortName(RemiLocation loc) =>
        loc switch
        {
            RemiLocation.Classroom => "教室",
            RemiLocation.Library => "图书馆",
            RemiLocation.Dorm => "宿舍",
            _ => loc.ToString(),
        };

    private static string ActivityShortName(RemiActivity act) =>
        act switch
        {
            RemiActivity.InClass => "上课",
            RemiActivity.Free => "空闲/自习",
            RemiActivity.AtDorm => "在宿舍",
            RemiActivity.Cooking => "做饭",
            RemiActivity.Busy => "忙碌",
            RemiActivity.Sleeping => "休息",
            _ => act.ToString(),
        };
}
