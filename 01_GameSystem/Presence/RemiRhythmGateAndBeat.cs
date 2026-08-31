using System;
using UnityEngine;

/// <summary>阶段切换展现（各播一次；与 Gate 分离）。升档时机：故事锚点提交。</summary>
[Flags]
public enum RemiRhythmBeatFlags
{
    None = 0,
    StoryDayBegins = 1 << 0,
    EnteredRelational = 1 << 1,
    EnteredInfluential = 1 << 2,
}

/// <summary>
/// Gate：进入下一锚点的资格验证（不负责算出关系档）。
/// Relational 资格 = 可走 Day2 图书馆树干；Influential 资格 = 可走 Day3 偏离/公寓树干。
/// </summary>
[Serializable]
public struct RemiRhythmGateSnapshot
{
    public bool storyDayStarted;
    public int storyDay;
    public RemiStoryAnchorFlags committedAnchors;
    public RemiDialogueDepthStage dialogueDepthStage;

    public bool HasClassroomOpening =>
        (committedAnchors & RemiStoryAnchorFlags.Day1ClassroomOpening) != 0 || storyDayStarted;

    public bool HasLibraryCoPresence =>
        (committedAnchors & RemiStoryAnchorFlags.Day2LibraryCoPresence) != 0;

    public bool HasApartmentCoPresence =>
        (committedAnchors & RemiStoryAnchorFlags.Day3ApartmentCoPresence) != 0;

    public static RemiRhythmGateSnapshot FromService(RemiPresenceService service)
    {
        if (service == null) return default;
        return new RemiRhythmGateSnapshot
        {
            storyDayStarted = service.StoryDayStarted,
            storyDay = service.WorldTime.storyDay,
            committedAnchors = service.CommittedStoryAnchors,
            dialogueDepthStage = service.DialogueDepthStage,
        };
    }
}

/// <summary>资格验证器：是否满足进入某类树干内容；关系档由 <see cref="RemiPresenceService.OnAnchorCommitted"/> 写入。</summary>
public static class RemiRhythmGateEvaluator
{
    public const int RelationalMinStoryDay = 2;
    public const int InfluentialMinStoryDay = 3;

    /// <summary>可进入 Day2 图书馆锚点相关内容：日≥2 且 Day1 教室锚点已立。</summary>
    public static bool IsRelationalGateOpen(RemiRhythmGateSnapshot g, RemiInteractionRhythmThresholds t)
    {
        t ??= new RemiInteractionRhythmThresholds();
        if (t.requireStoryStarted && !g.HasClassroomOpening)
            return false;
        return g.storyDay >= RelationalMinStoryDay;
    }

    /// <summary>可进入 Day3 偏离/公寓锚点相关内容：日≥3 且图书馆锚点已立（已 Relational）。</summary>
    public static bool IsInfluentialGateOpen(RemiRhythmGateSnapshot g, RemiInteractionRhythmThresholds t)
    {
        t ??= new RemiInteractionRhythmThresholds();
        if (!IsRelationalGateOpen(g, t))
            return false;
        if (g.storyDay < InfluentialMinStoryDay)
            return false;
        return g.HasLibraryCoPresence || g.dialogueDepthStage >= RemiDialogueDepthStage.Relational;
    }
}

public static class RemiRhythmBeatPlayer
{
    public static bool TryPlayStageAdvance(
        RemiDialogueDepthStage fromStage,
        RemiDialogueDepthStage toStage,
        RemiPresenceEventKind? trigger,
        ref RemiRhythmBeatFlags playedBeats)
    {
        if (toStage <= fromStage)
            return false;

        bool played = false;

        if (toStage >= RemiDialogueDepthStage.Relational && fromStage < RemiDialogueDepthStage.Relational)
            played |= TryPlayEnteredRelational(ref playedBeats);

        if (toStage >= RemiDialogueDepthStage.Influential && fromStage < RemiDialogueDepthStage.Influential)
            played |= TryPlayEnteredInfluential(ref playedBeats);

        return played;
    }

    private static bool TryPlayEnteredRelational(ref RemiRhythmBeatFlags playedBeats)
    {
        if ((playedBeats & RemiRhythmBeatFlags.EnteredRelational) != 0)
            return false;

        playedBeats |= RemiRhythmBeatFlags.EnteredRelational;
        RefreshDialogueUi();
        Debug.Log("[RemiRhythm] Beat EnteredRelational（图书馆锚点）");
        return true;
    }

    private static bool TryPlayEnteredInfluential(ref RemiRhythmBeatFlags playedBeats)
    {
        if ((playedBeats & RemiRhythmBeatFlags.EnteredInfluential) != 0)
            return false;

        playedBeats |= RemiRhythmBeatFlags.EnteredInfluential;
        RefreshDialogueUi();
        Debug.Log("[RemiRhythm] Beat EnteredInfluential（公寓锚点）");
        return true;
    }

    public static bool TryPlayStoryDayBegins(ref RemiRhythmBeatFlags playedBeats)
    {
        if ((playedBeats & RemiRhythmBeatFlags.StoryDayBegins) != 0)
            return false;
        playedBeats |= RemiRhythmBeatFlags.StoryDayBegins;
        StoryNarrativeHintView.TryPlayAfterRhythmStoryDayBegins();
        Debug.Log("[RemiRhythm] Beat StoryDayBegins");
        return true;
    }

    private static void RefreshDialogueUi()
    {
        DialoguePanel dp = UiManager.Instance?.GetPanel<DialoguePanel>();
        if (dp != null)
            dp.RefreshSuggestedQuestionsForRhythm();
    }
}
