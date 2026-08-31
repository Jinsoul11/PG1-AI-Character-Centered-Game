using UnityEngine;

/// <summary>
/// 互动节奏：关系档由故事锚点提交写入；Gate 仅校验进场资格。
/// Demo：教室→Surface，图书馆→Relational，公寓→Influential。
/// </summary>
public enum RemiDialogueDepthStage
{
    /// <summary>观察/浅接触：Day1 教室开场锚点后。</summary>
    Surface = 0,
    /// <summary>关系建立：Day2 图书馆共现锚点后。</summary>
    Relational = 1,
    /// <summary>深关系（Demo 搭档弧顶点）：Day3 公寓共现锚点后。</summary>
    Influential = 2,
}

/// <summary>
/// Gate 阈值（PG1）。进场资格用；关系档不由本类计算。
/// </summary>
[System.Serializable]
public class RemiInteractionRhythmThresholds
{
    [Tooltip("须完成 Day1 教室锚点才开放后续树干资格。")]
    public bool requireStoryStarted = true;

    [Tooltip("已弃用：关系档改由故事锚点写入。")]
    [Min(0)] public int relationalMinCommissionMilestones = 1;

    [Tooltip("已弃用：关系档改由故事锚点写入。")]
    public bool influentialRequiresMajorDelegation = true;
}

public static class RemiInteractionRhythm
{
    /// <summary>读取已提交的关系档（不再用 Gate 反算）。</summary>
    public static RemiDialogueDepthStage ComputeStage(
        RemiInteractionRhythmThresholds thresholds,
        RemiPresenceService presence)
    {
        if (presence == null)
            return RemiDialogueDepthStage.Surface;
        return presence.DialogueDepthStage;
    }

    public static string BuildStagePromptBlock(RemiDialogueDepthStage stage)
    {
        return stage switch
        {
            RemiDialogueDepthStage.Surface =>
                "【互动节奏·表层】\n聚焦现在与当面感受；对玩家只说自然语言。\n",
            RemiDialogueDepthStage.Relational =>
                "【互动节奏·关系】\n可触及共同经历；对玩家只说自然语言。\n",
            RemiDialogueDepthStage.Influential =>
                "【互动节奏·深关系】\n可触及更深层关系；对玩家只说自然语言；轨道偏离由 Remi/剧情发起。\n",
            _ => "",
        };
    }

    public static string StageDisplayName(RemiDialogueDepthStage stage) =>
        stage switch
        {
            RemiDialogueDepthStage.Relational => "关系建立（愿分享过去）",
            RemiDialogueDepthStage.Influential => "深关系（愿聊未来）",
            _ => "表层观察（聚焦现在）",
        };
}
