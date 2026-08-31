/// <summary>
/// LLM 轮次类型：玩家闲聊 vs 角色/导演触发（SendSystem；SendNpc 暂归入后者）。
/// </summary>
public enum RemiPromptTurnKind
{
    /// <summary>SendPlayer：回应玩家输入，可读 messages history（Voice / Intent 分层时仅 Voice 写 history）。</summary>
    PlayerChat = 0,

    /// <summary>SendSystem / SendNpc：Remi 先开口，跟 initiator_context。</summary>
    CharacterTriggered = 1,
}
