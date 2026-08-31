using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>已完成的委托类里程碑（Gate 用；与 ApplyCommissionEvent 状态 delta 并行）。</summary>
[Serializable]
public class RemiDelegationProgress
{
    [SerializeField] private List<RemiPresenceEventKind> completedMilestones = new List<RemiPresenceEventKind>();

    public IReadOnlyList<RemiPresenceEventKind> CompletedMilestones => completedMilestones;

    public bool HasCompleted(RemiPresenceEventKind kind) => completedMilestones.Contains(kind);

    public bool TryRecord(RemiPresenceEventKind kind)
    {
        if (completedMilestones.Contains(kind))
            return false;
        completedMilestones.Add(kind);
        return true;
    }

    public void Clear()
    {
        completedMilestones.Clear();
    }

    public string SerializeForPrefs()
    {
        if (completedMilestones == null || completedMilestones.Count == 0)
            return string.Empty;
        var sb = new StringBuilder();
        for (int i = 0; i < completedMilestones.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append((int)completedMilestones[i]);
        }

        return sb.ToString();
    }

    public void DeserializeFromPrefs(string data)
    {
        completedMilestones ??= new List<RemiPresenceEventKind>();
        completedMilestones.Clear();
        if (string.IsNullOrWhiteSpace(data)) return;

        string[] parts = data.Split(',');
        foreach (string p in parts)
        {
            if (!int.TryParse(p.Trim(), out int v)) continue;
            if (Enum.IsDefined(typeof(RemiPresenceEventKind), v))
                completedMilestones.Add((RemiPresenceEventKind)v);
        }
    }
}
