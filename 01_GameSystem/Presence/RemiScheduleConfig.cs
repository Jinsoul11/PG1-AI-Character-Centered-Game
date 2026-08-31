using System;
using UnityEngine;

/// <summary>
/// 默认生活轨道：某时段 Remi 应在哪、在做什么（可被偏离临时覆盖）。
/// </summary>
[Serializable]
public class RemiScheduleSlot
{
    public RemiDayPhase phase = RemiDayPhase.Morning;
    public RemiLocation location = RemiLocation.Classroom;
    public RemiActivity activity = RemiActivity.InClass;
    [TextArea(1, 2)]
    public string scheduleNote = "";
}
