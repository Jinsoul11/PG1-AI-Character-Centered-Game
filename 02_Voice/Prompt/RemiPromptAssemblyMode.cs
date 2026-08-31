/// <summary>System Prompt 组装模式（块裁剪）；输出格式由 <see cref="RemiPromptChannel"/> 决定。</summary>
public enum RemiPromptAssemblyMode
{
    /// <summary>日常闲聊 / 普通 SendSystem：Voice CONTRACT + CHARACTER + 动态上下文（再接 Intent）。</summary>
    Standard = 0,
    /// <summary>
    /// Ending 呈现（Bond / 共同经历回顾 / 收束）：
    /// 仅 Ending Voice CONTRACT + [TURN] director_context。
    /// </summary>
    EndingSpeak = 1,
}
