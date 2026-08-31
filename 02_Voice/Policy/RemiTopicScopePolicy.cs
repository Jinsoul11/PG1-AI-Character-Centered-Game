/// <summary>
/// 关系阶段 × 话题倾向；Voice 用精简 hint，不写 DAY_PLAN / 全量 MEMORY。
/// </summary>
public static class RemiTopicScopePolicy
{
    public static string BuildBlock(RemiDialogueDepthStage stage)
    {
        return stage switch
        {
            RemiDialogueDepthStage.Relational => RelationalBlock(),
            RemiDialogueDepthStage.Influential => InfluentialBlock(),
            _ => SurfaceBlock(),
        };
    }

    /// <summary>Voice [RELATIONSHIP] 内嵌 hint（无 DAY_PLAN 依赖）。</summary>
    public static string BuildVoiceRelationshipHint(RemiDialogueDepthStage stage) =>
        stage switch
        {
            RemiDialogueDepthStage.Relational =>
                "topic_hint: 愿意自然分享过去；即兴须与 [CHARACTER] seeds 及本轮 [ACTIVE_MEMORY] 一致。",
            RemiDialogueDepthStage.Influential =>
                "topic_hint: 可聊理想与近期打算；即兴须与 [CHARACTER] 及本轮 [ACTIVE_MEMORY]/[ACTIVE_KNOWLEDGE] 一致。",
            _ =>
                "topic_hint: 优先聊眼前与当下感受；个人过往与人生设想仅在被问到时简短带过。",
        };

    private static string SurfaceBlock() =>
        "topic_hint: 优先聊眼前与当下感受；个人过往与人生设想仅在被问到时简短带过。";

    private static string RelationalBlock() =>
        "canon_rule: 即兴过去须与 [CHARACTER] biography_seeds_personal 及 [MEMORY] 一致。\n" +
        "topic_hint: 愿意自然分享过去；今日稍后安排引用 [DAY_PLAN]。";

    private static string InfluentialBlock() =>
        "canon_rule: 即兴细节须与 [CHARACTER] 与 [MEMORY] 一致；既定日程以 [DAY_PLAN] 为准。\n" +
        "topic_hint: 可聊理想与近期打算；人生设想可即兴，今日计划仍引用 [DAY_PLAN]。";
}
