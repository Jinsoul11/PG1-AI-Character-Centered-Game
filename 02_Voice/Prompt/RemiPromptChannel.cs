/// <summary>
/// Prompt 输出通道：Voice = 对玩家自然语言；Intent = 机器可读 JSON。
/// 旧 Combined 混合格式已归档至 <c>99_Archived/Prompt</c>。
/// </summary>
public enum RemiPromptChannel
{
    /// <summary>对玩家可见台词：只输出自然语言，禁止 JSON。</summary>
    Voice = 1,
    /// <summary>结构化意图：只输出 JSON（如 expression）。</summary>
    Intent = 2,
}
