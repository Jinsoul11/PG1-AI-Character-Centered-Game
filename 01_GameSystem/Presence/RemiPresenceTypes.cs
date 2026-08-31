using System;
using UnityEngine;

/// <summary>
/// Remi「生活轨迹 / 在场」层：世界时间、地点、活动、通道、关系养成与决策倾向。
/// </summary>

/// <summary>
/// Presence 层主流程：角色委托玩家（找书、帮忙）→ ApplyCommissionEvent；轨道偏离由 Remi/剧情脊柱主动触发。
/// </summary>
public enum RemiPresenceFlowKind
{
    CharacterCommission = 0,
}

/// <summary>当前 location/activity 是否与 defaultSchedule[currentPhase] 一致。</summary>
public enum RemiTrackAlignment
{
    OnTrack = 0,
    Deviation = 1,
}

/// <summary>当前时段 narrative 单元（占格类型）；与轨道偏离正交。</summary>
public enum RemiPhaseEpisodeKind
{
    /// <summary>默认：跟随 defaultSchedule 的常规过法。</summary>
    Default = 0,
    /// <summary>Remi 委托（找书等）；在轨、beat 级，不占满 phase。</summary>
    Commission = 1,
    /// <summary>Remi 邀玩家共赴在轨日程；可占满 phase，但不改轨道。</summary>
    CoPresence = 2,
    /// <summary>原轨上的小插曲：面对面/社媒短互动，不改地点、不占满 phase。</summary>
    BeatInterlude = 3,
    /// <summary>剧情脊柱主动偏离导致改地点 + 占满当前 phase（如 Day3 来宿舍）。</summary>
    DeviationSession = 4,
}

/// <summary>Episode 占格与轨道规则（团队约定）。</summary>
public static class RemiPhaseEpisodeRules
{
    /// <summary>不改 location、不占满 phase 的互动。</summary>
    public static bool IsBeatLevel(RemiPhaseEpisodeKind kind) =>
        kind is RemiPhaseEpisodeKind.Default
            or RemiPhaseEpisodeKind.BeatInterlude
            or RemiPhaseEpisodeKind.Commission;

    /// <summary>改地点时必须占满 phase；仅 DeviationSession 会改轨道。</summary>
    public static bool RequiresTrackDeviation(RemiPhaseEpisodeKind kind) =>
        kind == RemiPhaseEpisodeKind.DeviationSession;

    /// <summary>不允许 beat 级占格的类型。</summary>
    public static bool AllowsPhaseOccupancy(RemiPhaseEpisodeKind kind) =>
        kind is RemiPhaseEpisodeKind.CoPresence or RemiPhaseEpisodeKind.DeviationSession;
}

/// <summary>Episode 结束原因（调试 / 系统推进）。</summary>
public enum RemiEpisodeEndReason
{
    Goodbye = 0,
    PhaseAdvanced = 1,
    CommissionComplete = 2,
    StoryBeat = 3,
}

public enum RemiDayPhase
{
    Morning = 0,
    Afternoon = 1,
    Evening = 2,
    Night = 3,
}

public enum RemiLocation
{
    Classroom = 0,
    Library = 1,
    Dorm = 2,
}

public enum RemiActivity
{
    InClass = 0,
    Free = 1,
    AtDorm = 2,
    Cooking = 3,
    Busy = 4,
    Sleeping = 5,
}

/// <summary>当前与 Remi 交互的通道（影响可达性与 Prompt 描述）。</summary>
public enum RemiInteractionChannel
{
    FaceToFace = 0,
    Social = 1,
}

/// <summary>叙事时钟推进原因（仅事件可拨针，非真实秒表）。</summary>
public enum RemiTimeAdvanceReason
{
    SyncOnly = 0,
    StoryDayBegan = 1,
    PhaseChanged = 2,
    BeatOnly = 3,
    NextDay = 4,
}

/// <summary>权威叙事时间：(storyDay, phase, beat)。</summary>
[Serializable]
public struct RemiWorldTime
{
    public int storyDay;
    public RemiDayPhase phase;
    public int beat;

    public static RemiWorldTime BeforeStory => new RemiWorldTime
    {
        storyDay = 0,
        phase = RemiDayPhase.Morning,
        beat = 0,
    };

    public bool IsStoryStarted => storyDay > 0;

    public int CompareTo(RemiWorldTime other)
    {
        int d = storyDay.CompareTo(other.storyDay);
        if (d != 0) return d;
        d = ((int)phase).CompareTo((int)other.phase);
        if (d != 0) return d;
        return beat.CompareTo(other.beat);
    }

    public bool Equals(RemiWorldTime other) =>
        storyDay == other.storyDay && phase == other.phase && beat == other.beat;

    public RemiWorldTime Capture() => this;
}

public static class RemiWorldTimeFormat
{
    public static string FormatRelative(RemiWorldTime past, RemiWorldTime now)
    {
        if (!past.IsStoryStarted || past.Equals(now))
            return "刚刚";
        int beatGap = now.beat - past.beat;
        if (now.storyDay == past.storyDay && now.phase == past.phase && beatGap > 0 && beatGap <= 2)
            return "刚刚";
        if (now.storyDay == past.storyDay)
            return "今天 · " + PhaseShortName(past.phase);
        if (now.storyDay == past.storyDay + 1)
            return "昨天 · " + PhaseShortName(past.phase);
        if (past.storyDay > 0)
            return $"第{past.storyDay}天 · {PhaseShortName(past.phase)}";
        return PhaseShortName(past.phase);
    }

    public static string FormatDivider(RemiWorldTime time) =>
        !time.IsStoryStarted ? "故事开始前" : $"第{time.storyDay}天 {PhaseShortName(time.phase)}";

    public static string PhaseShortName(RemiDayPhase phase) =>
        phase switch
        {
            RemiDayPhase.Morning => "上午",
            RemiDayPhase.Afternoon => "下午",
            RemiDayPhase.Evening => "傍晚",
            RemiDayPhase.Night => "夜间",
            _ => "白天",
        };

    public static bool ShouldShowDivider(RemiWorldTime previous, RemiWorldTime next, int minBeatGap = 3)
    {
        if (!previous.IsStoryStarted || !next.IsStoryStarted) return true;
        if (previous.storyDay != next.storyDay || previous.phase != next.phase) return true;
        return next.beat - previous.beat >= minBeatGap;
    }
}

public static class RemiPresenceAvailability
{
    public static string GetFaceToFaceUnavailableMessage(RemiPresenceService presence)
    {
        if (presence == null) return "现在似乎不太方便聊。";
        RemiActivity act = presence.CurrentActivity;
        return act switch
        {
            RemiActivity.Sleeping => "她好像已经睡了，明天再去找她吧。",
            RemiActivity.Cooking => "她在宿舍做饭，这会儿不太方便停下来聊。",
            RemiActivity.Busy => "她看起来很忙，过会儿再来吧。",
            _ => "现在不太方便面对面聊。",
        };
    }

    public static string GetSocialStatusBanner(RemiPresenceService presence)
    {
        if (presence == null) return string.Empty;
        if (RemiInteractionChannelPolicy.IsCoLocatedWithRemi(presence))
            return GetSocialCoLocatedBanner();
        if (presence.CanUseSocialChannelNow())
        {
            RemiActivity act = presence.CurrentActivity;
            if (act == RemiActivity.InClass || act == RemiActivity.Busy)
                return "Remi 可能在忙，回复会慢一些。";
            if (act == RemiActivity.Cooking)
                return "Remi 在做饭，可能晚点才看手机。";
            return string.Empty;
        }
        return presence.CurrentActivity switch
        {
            RemiActivity.Sleeping => "Remi 已休息，消息可能明天才会看到。",
            _ => "Remi 暂时不便回复。",
        };
    }

    public static string GetSocialCoLocatedBanner() =>
        "她就在附近，当面和她聊吧。";

    public static string GetFaceToFaceNotCoLocatedMessage(RemiPresenceService presence)
    {
        if (presence == null)
            return "她现在不在这里。";
        string place = SceneTravelCatalog.GetLocationDisplayName(
            SceneTravelCatalog.MapRemiLocation(presence.CurrentLocation));
        return $"她现在应该在{place}，用手机联系她吧。";
    }

    public static string GetSocialOfflineSystemLine(RemiPresenceService presence)
    {
        if (presence == null) return "消息已发送。";
        if (presence.CurrentActivity == RemiActivity.Sleeping)
            return "消息已发送 · 对方已休息";
        return "消息已发送 · 对方暂时不在线";
    }

    public static bool ShouldBlockSocialLlm(RemiPresenceService presence) =>
        presence == null
        || !RemiInteractionChannelPolicy.CanPlayerTypeInSocialChannel(presence);
}

public enum RemiStageExpressionContext
{
    SocialChat = 0,
    SocialMomentComment = 1,
    FaceToFaceChat = 2,
}

public static class RemiStageExpressionGuide
{
    public static RemiStageExpressionContext ContextForChannel(RemiInteractionChannel channel) =>
        channel == RemiInteractionChannel.FaceToFace
            ? RemiStageExpressionContext.FaceToFaceChat
            : RemiStageExpressionContext.SocialChat;
}
