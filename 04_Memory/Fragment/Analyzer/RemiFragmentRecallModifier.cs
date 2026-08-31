using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Recall Modifier（纯系统）：客观修正回忆概率，绝不编造语义。
/// Demo 不做时间衰减。
/// </summary>
public static class RemiFragmentRecallModifier
{
    public const float MaxRepetition = 0.12f;
    public const float NoveltyBonus = 0.05f;
    public const float MaxCrossDay = 0.10f;
    public const float EventAffinityBonus = 0.04f;
    public const float EndingProximityDay2 = 0.02f;
    public const float EndingProximityDay3Plus = 0.05f;

    public static RemiFragmentWeightBreakdown ComputeModifiers(
        RemiFragmentUnit unit,
        IReadOnlyList<RemiFragmentUnit> allUnits,
        IReadOnlyList<RemiDialogueArchiveEntry> archive,
        IReadOnlyList<RemiSharedExperienceEntry> experiences,
        out string reasonSnippet)
    {
        var breakdown = new RemiFragmentWeightBreakdown();
        var reasons = new List<string>();

        if (unit == null)
        {
            reasonSnippet = "";
            return breakdown;
        }

        breakdown.repetition = ComputeRepetition(unit, allUnits, archive);
        if (breakdown.repetition > 0.001f)
            reasons.Add($"相关主题重复(+{breakdown.repetition:0.00})");

        breakdown.novelty = ComputeNovelty(unit, allUnits);
        if (breakdown.novelty > 0.001f)
            reasons.Add("该 Meaning 首次出现(+novelty)");

        breakdown.crossDay = ComputeCrossDay(unit, archive);
        if (breakdown.crossDay > 0.001f)
            reasons.Add($"跨日再提(+{breakdown.crossDay:0.00})");

        breakdown.eventAffinity = ComputeEventAffinity(unit.storyDay, experiences);
        if (breakdown.eventAffinity > 0.001f)
            reasons.Add("与同日共同经历邻近");

        breakdown.endingProximity = ComputeEndingProximity(unit.storyDay);
        if (breakdown.endingProximity > 0.001f)
            reasons.Add("接近旅程后段");

        reasonSnippet = string.Join("；", reasons);
        return breakdown;
    }

    public static float CombineFinal(float intrinsicSemantic, RemiFragmentWeightBreakdown modifiers)
    {
        if (modifiers == null)
            return Mathf.Clamp01(intrinsicSemantic);

        float sum = intrinsicSemantic
                    + modifiers.repetition
                    + modifiers.novelty
                    + modifiers.crossDay
                    + modifiers.eventAffinity
                    + modifiers.endingProximity;
        return Mathf.Clamp01(sum);
    }

    public static string BuildWeightReason(string semanticReason, string modifierSnippet, float finalWeight)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(semanticReason))
            sb.Append(semanticReason.Trim());
        if (!string.IsNullOrWhiteSpace(modifierSnippet))
        {
            if (sb.Length > 0)
                sb.Append("；");
            sb.Append(modifierSnippet.Trim());
        }

        if (sb.Length == 0)
            sb.Append("回忆概率 ").Append(finalWeight.ToString("0.00"));
        return sb.ToString();
    }

    private static float ComputeRepetition(
        RemiFragmentUnit unit,
        IReadOnlyList<RemiFragmentUnit> allUnits,
        IReadOnlyList<RemiDialogueArchiveEntry> archive)
    {
        float score = 0f;
        // 其它 Unit 共享 Meaning 标签 → 主题强化
        if (unit.meaningTags != null && allUnits != null)
        {
            int shared = 0;
            foreach (RemiFragmentUnit other in allUnits)
            {
                if (other == null || other.id == unit.id || other.meaningTags == null)
                    continue;
                foreach (string tag in unit.meaningTags)
                {
                    if (other.meaningTags.Contains(tag))
                    {
                        shared++;
                        break;
                    }
                }
            }

            score += Mathf.Min(0.08f, shared * 0.03f);
        }

        // Archive 中其它日玩家句命中 evidence 关键词
        int hits = CountArchiveKeywordHits(unit, archive, excludeStoryDay: unit.storyDay);
        score += Mathf.Min(0.06f, hits * 0.015f);
        return Mathf.Min(MaxRepetition, score);
    }

    private static float ComputeNovelty(RemiFragmentUnit unit, IReadOnlyList<RemiFragmentUnit> allUnits)
    {
        if (unit.meaningTags == null || unit.meaningTags.Count == 0 || allUnits == null)
            return 0f;

        foreach (string tag in unit.meaningTags)
        {
            bool seenBefore = false;
            foreach (RemiFragmentUnit other in allUnits)
            {
                if (other == null || other.id == unit.id || other.storyDay >= unit.storyDay)
                    continue;
                if (other.meaningTags != null && other.meaningTags.Contains(tag))
                {
                    seenBefore = true;
                    break;
                }
            }

            if (!seenBefore)
                return NoveltyBonus;
        }

        return 0f;
    }

    private static float ComputeCrossDay(
        RemiFragmentUnit unit,
        IReadOnlyList<RemiDialogueArchiveEntry> archive)
    {
        int hits = CountArchiveKeywordHits(unit, archive, excludeStoryDay: unit.storyDay);
        if (hits <= 0)
            return 0f;
        return Mathf.Min(MaxCrossDay, 0.03f + hits * 0.02f);
    }

    private static float ComputeEventAffinity(
        int storyDay,
        IReadOnlyList<RemiSharedExperienceEntry> experiences)
    {
        if (experiences == null)
            return 0f;
        foreach (RemiSharedExperienceEntry e in experiences)
        {
            if (e != null && e.storyDay == storyDay)
                return EventAffinityBonus;
        }

        return 0f;
    }

    private static float ComputeEndingProximity(int storyDay)
    {
        if (storyDay >= 3)
            return EndingProximityDay3Plus;
        if (storyDay == 2)
            return EndingProximityDay2;
        return 0f;
    }

    private static int CountArchiveKeywordHits(
        RemiFragmentUnit unit,
        IReadOnlyList<RemiDialogueArchiveEntry> archive,
        int excludeStoryDay)
    {
        if (archive == null || unit == null)
            return 0;

        var keywords = new List<string>();
        CollectKeywords(unit.summary, keywords);
        if (unit.evidence != null)
        {
            foreach (string e in unit.evidence)
                CollectKeywords(e, keywords);
        }

        if (keywords.Count == 0)
            return 0;

        int hits = 0;
        foreach (RemiDialogueArchiveEntry entry in archive)
        {
            if (entry == null || entry.storyDay == excludeStoryDay)
                continue;
            if (entry.SourceKind != RemiDialogueArchiveSource.FreeChat)
                continue;
            if (!string.Equals(
                    RemiDialogueArchive.NormalizeSpeaker(entry.speaker),
                    "player",
                    System.StringComparison.OrdinalIgnoreCase))
                continue;

            string content = entry.content ?? "";
            foreach (string kw in keywords)
            {
                if (kw.Length >= 2 && content.Contains(kw))
                {
                    hits++;
                    break;
                }
            }
        }

        return hits;
    }

    private static void CollectKeywords(string text, List<string> into)
    {
        if (string.IsNullOrWhiteSpace(text) || into == null)
            return;

        // 粗粒度：取 2～4 字片段与整词，避免停用词表过重。
        string t = text.Trim();
        string[] seeds =
        {
            "未来", "过去", "自己", "害怕", "喜欢", "画画", "虚拟", "真实", "觉醒",
            "压力", "孤独", "理解", "世界", "普通", "适合", "硕士", "毕业",
        };
        foreach (string s in seeds)
        {
            if (t.Contains(s) && !into.Contains(s))
                into.Add(s);
        }

        if (t.Length >= 4 && into.Count == 0)
        {
            string slice = t.Length > 8 ? t.Substring(0, 4) : t.Substring(0, Mathf.Min(4, t.Length));
            if (!into.Contains(slice))
                into.Add(slice);
        }
    }
}
