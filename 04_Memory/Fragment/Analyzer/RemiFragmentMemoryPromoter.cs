using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 将已分析的 Fragment Unit 晋升为 Fragment Memory（过程印象终库）。
/// </summary>
public static class RemiFragmentMemoryPromoter
{
    public static int PromoteStoryDay(int storyDay)
    {
        RemiFragmentUnitStore.EnsureExists();
        RemiFragmentMemory.EnsureExists();
        if (RemiFragmentUnitStore.Instance == null || RemiFragmentMemory.Instance == null)
            return 0;

        int count = 0;
        foreach (RemiFragmentUnit unit in RemiFragmentUnitStore.Instance.GetUnitsForStoryDay(storyDay))
        {
            if (unit == null || !unit.weightReady || string.IsNullOrWhiteSpace(unit.summary))
                continue;
            if (PromoteUnit(unit))
                count++;
        }

        if (count > 0)
            Debug.Log($"[RemiFragmentMemoryPromoter] Day {storyDay}: promoted {count} → Fragment Memory");
        return count;
    }

    public static bool PromoteUnit(RemiFragmentUnit unit)
    {
        if (unit == null || RemiFragmentMemory.Instance == null)
            return false;

        var meaningTags = new List<string>();
        IList<string> sourceTags = unit.meaningTags != null && unit.meaningTags.Count > 0
            ? unit.meaningTags
            : unit.candidateTags;
        if (sourceTags != null)
        {
            foreach (string key in sourceTags)
            {
                if (!RemiChatFragmentTagRules.TryParse(key, out RemiChatFragmentTag tag))
                    continue;
                string normalized = RemiChatFragmentTagRules.ToKey(tag);
                if (!meaningTags.Contains(normalized))
                    meaningTags.Add(normalized);
            }
        }

        RemiFragmentWeightBreakdown breakdown = unit.weightBreakdown != null
            ? new RemiFragmentWeightBreakdown
            {
                semantic = unit.weightBreakdown.semantic,
                repetition = unit.weightBreakdown.repetition,
                novelty = unit.weightBreakdown.novelty,
                crossDay = unit.weightBreakdown.crossDay,
                eventAffinity = unit.weightBreakdown.eventAffinity,
                endingProximity = unit.weightBreakdown.endingProximity,
            }
            : new RemiFragmentWeightBreakdown();

        var impression = new RemiFragmentImpression
        {
            id = unit.id,
            summary = RemiFragmentSummarySanitize.ReplaceAmbiguousOtherParty(unit.summary.Trim()),
            storyDay = unit.storyDay,
            meaningTags = meaningTags,
            atmosphere = unit.atmosphere ?? "",
            weight = unit.weight,
            weightReason = unit.weightReason ?? "",
            weightBreakdown = breakdown,
            intrinsicStrength = unit.intrinsicStrength,
            quote = unit.quoteCandidate ?? "",
            quoteCiteEligible = false,
            resonanceHint = BuildResonanceHint(unit),
            promotedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            sourceUnitId = unit.id,
        };

        RemiFragmentTopicAliasBuilder.ApplyFromUnit(impression, unit);
        return RemiFragmentMemory.Instance.TryUpsert(impression);
    }

    private static string BuildResonanceHint(RemiFragmentUnit unit)
    {
        if (unit == null)
            return "";

        bool isResonance = false;
        if (unit.meaningTags != null)
        {
            foreach (string t in unit.meaningTags)
            {
                if (RemiChatFragmentTagRules.TryParse(t, out RemiChatFragmentTag tag) &&
                    tag == RemiChatFragmentTag.Resonance)
                {
                    isResonance = true;
                    break;
                }
            }
        }

        if (!isResonance && unit.candidateTags != null)
        {
            foreach (string t in unit.candidateTags)
            {
                if (RemiChatFragmentTagRules.TryParse(t, out RemiChatFragmentTag tag) &&
                    tag == RemiChatFragmentTag.Resonance)
                {
                    isResonance = true;
                    break;
                }
            }
        }

        if (!isResonance)
            return "";

        if (unit.evidence != null)
        {
            foreach (string e in unit.evidence)
            {
                if (string.IsNullOrWhiteSpace(e))
                    continue;
                string hint = e.Trim();
                if (hint.Length > 12)
                    hint = hint.Substring(0, 12);
                return hint;
            }
        }

        if (!string.IsNullOrWhiteSpace(unit.atmosphere))
            return unit.atmosphere.Trim();

        return "那些话";
    }
}
