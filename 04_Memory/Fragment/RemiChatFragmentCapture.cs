using System.Collections.Generic;

/// <summary>
/// 会话结束采集：关键词命中 → 多标签记忆单元。
/// Relation / Atmosphere 可并入标签与权重，但仅 Relation 可见标签的命中不入库。
/// 不写入 raw quote（圣物须另闸门）。
/// </summary>
public static class RemiChatFragmentCapture
{
    public static void TryCaptureSession(
        RemiInteractionChannel channel,
        IReadOnlyList<ChatMessage> sessionMessages)
    {
        if (sessionMessages == null || sessionMessages.Count == 0)
            return;

        var playerLines = new List<string>();
        foreach (ChatMessage message in sessionMessages)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.content))
                continue;
            if (!IsPlayerRole(message.role))
                continue;
            playerLines.Add(message.content.Trim());
        }

        if (playerLines.Count == 0)
            return;

        RemiChatFragmentCatalog.PatternDef? bestVisible = null;
        int bestHits = 0;
        float bestWeight = 0f;
        var unionTags = new List<RemiChatFragmentTag>();
        string atmosphere = "";
        string bondKeyword = "";
        int relationBoostHits = 0;

        foreach (RemiChatFragmentCatalog.PatternDef pattern in RemiChatFragmentCatalog.AllPatterns)
        {
            int totalHits = 0;
            foreach (string line in playerLines)
                totalHits += RemiChatFragmentCatalog.CountKeywordHits(line, pattern);

            if (totalHits <= 0)
                continue;

            UnionTags(unionTags, pattern.Tags);

            bool onlyRelationOrAtmosphere = IsIndexOnlyPattern(pattern);
            if (onlyRelationOrAtmosphere)
            {
                relationBoostHits += totalHits;
                if (string.IsNullOrWhiteSpace(atmosphere) && !string.IsNullOrWhiteSpace(pattern.Atmosphere))
                    atmosphere = pattern.Atmosphere;
                continue;
            }

            float weight = ScorePattern(pattern, totalHits);
            if (weight > bestWeight || (weight >= bestWeight && totalHits > bestHits))
            {
                bestWeight = weight;
                bestHits = totalHits;
                bestVisible = pattern;
                if (!string.IsNullOrWhiteSpace(pattern.Atmosphere))
                    atmosphere = pattern.Atmosphere;
                if (!string.IsNullOrWhiteSpace(pattern.BondKeyword))
                    bondKeyword = pattern.BondKeyword;
            }
        }

        if (!bestVisible.HasValue || bestHits <= 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log(
                $"[RemiChatFragmentCapture] Session ended ({channel}) with {playerLines.Count} player line(s); no ending-visible pattern matched.");
#endif
            return;
        }

        RemiChatFragmentCatalog.PatternDef chosen = bestVisible.Value;
        UnionTags(unionTags, chosen.Tags);

        // Relation 命中只加权重，不单独决定 summary。
        float finalWeight = bestWeight + relationBoostHits * 0.35f;

        RemiChatFragmentMemory.EnsureExists();
        if (RemiChatFragmentMemory.Instance == null)
            return;

        RemiWorldTime worldTime = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.WorldTime
            : RemiWorldTime.BeforeStory;

        var entry = new RemiChatFragmentEntry(
            chosen.Id,
            chosen.Summary,
            unionTags,
            atmosphere,
            channel,
            worldTime,
            bestHits,
            finalWeight);

        // 预留：BondKeyword 不进正文字段；Resonance 关键词由 Ending 组装时从 Catalog 读。
        _ = bondKeyword;

        RemiChatFragmentMemory.Instance.TryRecord(entry);
    }

    private static bool IsIndexOnlyPattern(RemiChatFragmentCatalog.PatternDef pattern)
    {
        if (pattern.Tags == null || pattern.Tags.Length == 0)
            return true;
        if (RemiChatFragmentCatalog.HasEndingVisibleTag(pattern))
            return false;

        foreach (RemiChatFragmentTag tag in pattern.Tags)
        {
            if (tag != RemiChatFragmentTag.Relation && tag != RemiChatFragmentTag.Atmosphere)
                return false;
        }

        return true;
    }

    private static float ScorePattern(RemiChatFragmentCatalog.PatternDef pattern, int hits)
    {
        float score = hits;
        if (pattern.Tags == null)
            return score;

        foreach (RemiChatFragmentTag tag in pattern.Tags)
        {
            if (tag == RemiChatFragmentTag.Moment)
                score += 1.5f;
            else if (tag == RemiChatFragmentTag.Identity)
                score += 1f;
            else if (tag == RemiChatFragmentTag.Resonance)
                score += 0.75f;
        }

        if (pattern.Atmosphere == "认真")
            score += 0.5f;

        return score;
    }

    private static void UnionTags(List<RemiChatFragmentTag> target, RemiChatFragmentTag[] source)
    {
        if (source == null)
            return;
        foreach (RemiChatFragmentTag tag in source)
        {
            if (!target.Contains(tag))
                target.Add(tag);
        }
    }

    private static bool IsPlayerRole(string role) =>
        string.Equals(role, "user", System.StringComparison.OrdinalIgnoreCase);
}
