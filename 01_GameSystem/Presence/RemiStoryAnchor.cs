using UnityEngine;

/// <summary>
/// Demo 树干锚点：关系档由锚点提交推出，不由日常委托/Gate 反算。
/// 对外可称 Companion/Partner；对内仍用 Surface / Relational / Influential。
/// </summary>
public enum RemiStoryAnchorId
{
    None = 0,
    /// <summary>Day1 Remi↔Ema 教室开场 → Surface。</summary>
    Day1ClassroomOpening = 1,
    /// <summary>Day2 玩家↔Remi 图书馆共现剧情 → Relational。</summary>
    Day2LibraryCoPresence = 2,
    /// <summary>Day3 公寓共现剧情 → Influential。</summary>
    Day3ApartmentCoPresence = 3,
}

[System.Flags]
public enum RemiStoryAnchorFlags
{
    None = 0,
    Day1ClassroomOpening = 1 << 0,
    Day2LibraryCoPresence = 1 << 1,
    Day3ApartmentCoPresence = 1 << 2,
}

/// <summary>锚点 → 目标关系档 + 日历日校验。</summary>
public static class RemiStoryAnchorCatalog
{
    public static bool TryGetCommitSpec(
        RemiStoryAnchorId anchorId,
        out RemiDialogueDepthStage targetStage,
        out int requiredStoryDay,
        out RemiStoryAnchorFlags flag)
    {
        switch (anchorId)
        {
            case RemiStoryAnchorId.Day1ClassroomOpening:
                targetStage = RemiDialogueDepthStage.Surface;
                requiredStoryDay = 1;
                flag = RemiStoryAnchorFlags.Day1ClassroomOpening;
                return true;
            case RemiStoryAnchorId.Day2LibraryCoPresence:
                targetStage = RemiDialogueDepthStage.Relational;
                requiredStoryDay = 2;
                flag = RemiStoryAnchorFlags.Day2LibraryCoPresence;
                return true;
            case RemiStoryAnchorId.Day3ApartmentCoPresence:
                targetStage = RemiDialogueDepthStage.Influential;
                requiredStoryDay = 3;
                flag = RemiStoryAnchorFlags.Day3ApartmentCoPresence;
                return true;
            default:
                targetStage = RemiDialogueDepthStage.Surface;
                requiredStoryDay = 0;
                flag = RemiStoryAnchorFlags.None;
                return false;
        }
    }

    public static RemiStoryAnchorFlags ToFlag(RemiStoryAnchorId anchorId) =>
        TryGetCommitSpec(anchorId, out _, out _, out RemiStoryAnchorFlags flag) ? flag : RemiStoryAnchorFlags.None;
}
