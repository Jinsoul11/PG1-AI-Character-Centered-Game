using System;
using System.Collections.Generic;

/// <summary>框架认定的共同经历（仅系统登记；LLM 不可新增）。</summary>
public enum RemiSharedExperienceId
{
    Day1CommissionBook = 1,
    Day2LibraryCoPresence = 2,
    Day3DormDeviation = 3,
}

[Serializable]
public class RemiSharedExperienceEntry
{
    public string id;
    public string kind;
    public string frame;
    public int storyDay;
    public int phase;

    public RemiSharedExperienceEntry() { }

    public RemiSharedExperienceEntry(
        RemiSharedExperienceId experienceId,
        string kind,
        string frame,
        int storyDay,
        RemiDayPhase phase)
    {
        id = RemiSharedExperienceCatalog.IdKey(experienceId);
        this.kind = kind;
        this.frame = frame ?? "";
        this.storyDay = storyDay;
        this.phase = (int)phase;
    }
}

[Serializable]
public class RemiSharedExperienceStore
{
    public List<RemiSharedExperienceEntry> entries = new List<RemiSharedExperienceEntry>();
}
