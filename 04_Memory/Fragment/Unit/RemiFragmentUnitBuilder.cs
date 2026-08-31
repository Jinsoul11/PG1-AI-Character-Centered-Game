using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fragment Unit 固化（纯系统）：把 Curator 候选变成标准化记忆单元。
/// 无 LLM；不写 Analyzer Meaning / Final Weight；不进 Fragment Memory。
/// </summary>
public static class RemiFragmentUnitBuilder
{
    /// <summary>
    /// 将某日 Curator 结果固化为 Unit，并替换该日旧 Unit。
    /// 返回写入条数。
    /// </summary>
    public static int MaterializeFromCuratorDay(RemiMemoryCuratorDayResult dayResult)
    {
        if (dayResult == null || !dayResult.success)
            return 0;

        RemiFragmentUnitStore.EnsureExists();
        if (RemiFragmentUnitStore.Instance == null)
            return 0;

        var built = new List<RemiFragmentUnit>();
        IList<RemiMemoryCuratorCandidate> candidates = dayResult.candidates;
        if (candidates != null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                RemiFragmentUnit unit = FromCandidate(dayResult.storyDay, candidates[i]);
                if (unit != null)
                    built.Add(unit);
            }
        }

        RemiFragmentUnitStore.Instance.ReplaceUnitsForStoryDay(dayResult.storyDay, built);
        Debug.Log(
            $"[RemiFragmentUnitBuilder] Day {dayResult.storyDay}: " +
            $"materialized {built.Count} unit(s) from curator (weight pending Analyzer).");
        return built.Count;
    }

    /// <summary>若该日已有成功策展但尚无 Unit，补固化（切日跳过策展时用）。</summary>
    public static int EnsureMaterializedForStoryDay(int storyDay)
    {
        RemiFragmentUnitStore.EnsureExists();
        if (RemiFragmentUnitStore.Instance != null &&
            RemiFragmentUnitStore.Instance.GetUnitsForStoryDay(storyDay).Count > 0)
            return 0;

        RemiMemoryCuratorStore.EnsureExists();
        if (RemiMemoryCuratorStore.Instance == null)
            return 0;
        if (!RemiMemoryCuratorStore.Instance.TryGetDay(storyDay, out RemiMemoryCuratorDayResult day) ||
            day == null ||
            !day.success)
            return 0;

        return MaterializeFromCuratorDay(day);
    }

    public static RemiFragmentUnit FromCandidate(int storyDay, RemiMemoryCuratorCandidate candidate)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(candidate.summary))
            return null;

        RemiFragmentUnitStore.EnsureExists();
        string id = RemiFragmentUnitStore.Instance != null
            ? RemiFragmentUnitStore.Instance.AllocateId(storyDay)
            : $"fu_d{storyDay}_{Guid.NewGuid():N}".Substring(0, 16);

        string quote = "";
        if (candidate.evidence != null)
        {
            foreach (string e in candidate.evidence)
            {
                if (!string.IsNullOrWhiteSpace(e))
                {
                    quote = e.Trim();
                    break;
                }
            }
        }

        var unit = new RemiFragmentUnit
        {
            id = id,
            summary = RemiFragmentSummarySanitize.ReplaceAmbiguousOtherParty(candidate.summary.Trim()),
            storyDay = storyDay,
            speaker = "player",
            quoteCandidate = quote,
            quoteCiteEligible = false,
            curatorReason = candidate.reason ?? "",
            curatorConfidence = Mathf.Clamp01(candidate.confidence),
            // Final Weight 留给 Analyzer：回忆概率，不是策展自信。
            weightReady = false,
            meaningReady = false,
            weight = 0f,
            weightReason = "",
            weightBreakdown = new RemiFragmentWeightBreakdown(),
            intrinsicStrength = 0f,
            weightComputedUnixMs = 0,
            createdUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            sourceCuratorStoryDay = storyDay,
        };

        if (candidate.evidence != null)
        {
            foreach (string e in candidate.evidence)
            {
                if (!string.IsNullOrWhiteSpace(e))
                    unit.evidence.Add(e.Trim());
            }
        }

        if (candidate.candidateTags != null)
        {
            foreach (string tag in candidate.candidateTags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    continue;
                if (!RemiChatFragmentTagRules.TryParse(tag, out RemiChatFragmentTag parsed))
                    continue;
                string key = RemiChatFragmentTagRules.ToKey(parsed);
                if (!unit.candidateTags.Contains(key))
                    unit.candidateTags.Add(key);
            }
        }

        return unit;
    }
}
