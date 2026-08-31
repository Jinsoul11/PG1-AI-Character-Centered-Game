/// <summary>
/// 当轮对话行为策略（Prompt [POLICY] 单一真源；全主动玩法：仅闲聊 / SendSystem，无 action/feeling）。
/// </summary>
public readonly struct RemiConversationPolicy
{
    public RemiDialogueDepthStage Stage { get; }
    public RemiPromptInitiativeLevel Initiative { get; }

    public RemiConversationPolicy(RemiDialogueDepthStage stage, RemiPromptInitiativeLevel initiative)
    {
        Stage = stage;
        Initiative = initiative;
    }

    public static RemiConversationPolicy FromService(RemiPresenceService service)
    {
        if (service == null)
            return DefaultSurface();

        return service.DialogueDepthStage switch
        {
            RemiDialogueDepthStage.Relational => new RemiConversationPolicy(
                RemiDialogueDepthStage.Relational,
                RemiPromptInitiativeLevel.Medium),
            RemiDialogueDepthStage.Influential => new RemiConversationPolicy(
                RemiDialogueDepthStage.Influential,
                RemiPromptInitiativeLevel.Medium),
            _ => DefaultSurface(),
        };
    }

    private static RemiConversationPolicy DefaultSurface() =>
        new RemiConversationPolicy(RemiDialogueDepthStage.Surface, RemiPromptInitiativeLevel.Low);
}

public enum RemiPromptInitiativeLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
}
