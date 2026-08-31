using System;

/// <summary>
/// Demo 三日块计划：A Routine → B Window/Anchor → C Aftermath|Return。
/// Day1/Day2 的 C = Aftermath（一直在轨）；Day3 的 C = Return（仅偏离后）。
/// </summary>
public static class RemiDayBlockCatalog
{
    private static readonly RemiDayBlockDef[] Day1 =
    {
        new RemiDayBlockDef(RemiDayBlockSlot.A, RemiDayBlockKind.Routine, RemiDayPhase.Morning, "classroom_routine"),
        new RemiDayBlockDef(RemiDayBlockSlot.B, RemiDayBlockKind.Window, RemiDayPhase.Morning, "commission_window_anchor"),
        new RemiDayBlockDef(RemiDayBlockSlot.C, RemiDayBlockKind.Aftermath, RemiDayPhase.Evening, "on_track_aftermath"),
    };

    private static readonly RemiDayBlockDef[] Day2 =
    {
        new RemiDayBlockDef(RemiDayBlockSlot.A, RemiDayBlockKind.Routine, RemiDayPhase.Morning, "library_bound_routine"),
        new RemiDayBlockDef(RemiDayBlockSlot.B, RemiDayBlockKind.Window, RemiDayPhase.Afternoon, "copresence_window_anchor"),
        new RemiDayBlockDef(RemiDayBlockSlot.C, RemiDayBlockKind.Aftermath, RemiDayPhase.Evening, "on_track_aftermath"),
    };

    private static readonly RemiDayBlockDef[] Day3 =
    {
        new RemiDayBlockDef(RemiDayBlockSlot.A, RemiDayBlockKind.Routine, RemiDayPhase.Morning, "exhibition_routine"),
        new RemiDayBlockDef(RemiDayBlockSlot.B, RemiDayBlockKind.Window, RemiDayPhase.Afternoon, "deviation_window_anchor"),
        new RemiDayBlockDef(RemiDayBlockSlot.C, RemiDayBlockKind.Return, RemiDayPhase.Evening, "post_deviation_return"),
    };

    public static RemiDayBlockDef[] GetPlan(int storyDay) =>
        storyDay switch
        {
            1 => Day1,
            2 => Day2,
            3 => Day3,
            _ => Array.Empty<RemiDayBlockDef>(),
        };

    public static bool TryGetDef(int storyDay, RemiDayBlockSlot slot, out RemiDayBlockDef def)
    {
        RemiDayBlockDef[] plan = GetPlan(storyDay);
        int i = (int)slot;
        if (plan == null || i < 0 || i >= plan.Length)
        {
            def = default;
            return false;
        }

        def = plan[i];
        return true;
    }

    public static string KindKey(RemiDayBlockKind kind) =>
        kind switch
        {
            RemiDayBlockKind.Routine => "routine",
            RemiDayBlockKind.Window => "window",
            RemiDayBlockKind.Anchor => "anchor",
            RemiDayBlockKind.Aftermath => "aftermath",
            RemiDayBlockKind.Return => "return",
            _ => "none",
        };

    public static string SlotKey(RemiDayBlockSlot slot) =>
        slot switch
        {
            RemiDayBlockSlot.A => "A",
            RemiDayBlockSlot.B => "B",
            RemiDayBlockSlot.C => "C",
            _ => "?",
        };

    /// <summary>Return 仅允许出现在已发生偏离的 Day3 计划中。</summary>
    public static bool PlanAllowsReturn(int storyDay) => storyDay == 3;
}
