using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 委托类里程碑（过程资产记账）。关系档由故事锚点写入，不由委托打开。
/// </summary>
[Serializable]
public class RemiDelegationGateRule
{
    public RemiPresenceEventKind eventKind;

    [Tooltip("是否计入委托里程碑统计（过程资产；不升关系档）。")]
    public bool countsForRelationalMilestone = true;

    [Tooltip("已弃用：Influential 由公寓故事锚点写入。")]
    public bool satisfiesInfluentialGate;
}

/// <summary>PG1 默认委托里程碑表；可在 RemiPresenceService Inspector 覆盖。</summary>
public static class RemiDelegationGateCatalog
{
    public static RemiDelegationGateRule[] CreateDefaultRules() => new[]
    {
        new RemiDelegationGateRule
        {
            eventKind = RemiPresenceEventKind.RemiRequestedBookHelp,
            countsForRelationalMilestone = true,
        },
        new RemiDelegationGateRule
        {
            eventKind = RemiPresenceEventKind.PlayerPickedUpBook,
            countsForRelationalMilestone = true,
        },
        new RemiDelegationGateRule
        {
            eventKind = RemiPresenceEventKind.PlayerSubmittedBook,
            countsForRelationalMilestone = true,
            satisfiesInfluentialGate = false,
        },
        new RemiDelegationGateRule
        {
            eventKind = RemiPresenceEventKind.PlayerApproachedFront,
            countsForRelationalMilestone = true,
        },
    };

    public static RemiDelegationGateRule FindRule(RemiDelegationGateRule[] rules, RemiPresenceEventKind kind)
    {
        if (rules == null) return null;
        foreach (RemiDelegationGateRule r in rules)
        {
            if (r != null && r.eventKind == kind)
                return r;
        }

        return null;
    }

    public static int CountRelationalMilestones(
        IReadOnlyList<RemiPresenceEventKind> completed,
        RemiDelegationGateRule[] rules)
    {
        if (completed == null || rules == null) return 0;
        int n = 0;
        foreach (RemiPresenceEventKind k in completed)
        {
            RemiDelegationGateRule r = FindRule(rules, k);
            if (r != null && r.countsForRelationalMilestone)
                n++;
        }

        return n;
    }

    public static bool HasInfluentialDelegationComplete(
        IReadOnlyList<RemiPresenceEventKind> completed,
        RemiDelegationGateRule[] rules)
    {
        if (completed == null || rules == null) return false;
        foreach (RemiPresenceEventKind k in completed)
        {
            RemiDelegationGateRule r = FindRule(rules, k);
            if (r != null && r.satisfiesInfluentialGate)
                return true;
        }

        return false;
    }
}
