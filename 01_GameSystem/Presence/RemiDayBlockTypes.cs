using System;

/// <summary>
/// 一天内的叙事块种类（生活语义，不是墙上钟点）。
/// Return 仅在发生 Deviation 之后出现；Demo 仅 Day3 有 Return。
/// </summary>
public enum RemiDayBlockKind
{
    None = 0,
    /// <summary>在轨日常：证明她有自己的安排。</summary>
    Routine = 1,
    /// <summary>可变窗口：锚点前玩家可影响「如何发生」。</summary>
    Window = 2,
    /// <summary>关系锚点进行中（委托 / 共现 / 偏离）。</summary>
    Anchor = 3,
    /// <summary>在轨余波（Day1/Day2；非偏离归位）。</summary>
    Aftermath = 4,
    /// <summary>偏离后的归位（仅 Deviation 之后；Demo = Day3）。</summary>
    Return = 5,
}

/// <summary>块在当日序列中的槽位（Demo 三块制：A/B/C）。</summary>
public enum RemiDayBlockSlot
{
    A = 0,
    B = 1,
    C = 2,
}

/// <summary>单日一块的静态定义。</summary>
[Serializable]
public struct RemiDayBlockDef
{
    public RemiDayBlockSlot slot;
    public RemiDayBlockKind kind;
    /// <summary>兼容旧灯光/日程：该块默认映射的 RemiDayPhase。</summary>
    public RemiDayPhase phaseHint;
    /// <summary>短标签（调试 / Prompt）。</summary>
    public string label;

    public RemiDayBlockDef(RemiDayBlockSlot slot, RemiDayBlockKind kind, RemiDayPhase phaseHint, string label)
    {
        this.slot = slot;
        this.kind = kind;
        this.phaseHint = phaseHint;
        this.label = label ?? "";
    }
}
