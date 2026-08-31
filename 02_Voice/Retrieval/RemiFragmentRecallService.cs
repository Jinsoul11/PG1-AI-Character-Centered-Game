using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fragment ACTIVE_MEMORY 召回：别名强命中主路径；多命中 LLM relevance，失败/并列 weight 保底。
/// </summary>
public static class RemiFragmentRecallService
{
    public const int MaxActiveMemory = 1;

    public static List<RemiFragmentImpression> ScanAliasHits(string haystack, int currentStoryDay)
    {
        var hits = new List<RemiFragmentImpression>();
        if (string.IsNullOrEmpty(haystack))
            return hits;

        RemiFragmentMemory.EnsureExists();
        if (RemiFragmentMemory.Instance == null)
            return hits;

        IReadOnlyList<RemiFragmentImpression> pool = RemiFragmentMemory.Instance.GetImpressionsOrdered();
        if (pool == null || pool.Count == 0)
            return hits;

        foreach (RemiFragmentImpression imp in pool)
        {
            if (imp == null || string.IsNullOrWhiteSpace(imp.id))
                continue;
            if (currentStoryDay > 0 && imp.storyDay >= currentStoryDay)
                continue;

            RemiFragmentTopicAliasBuilder.EnsureRecallEligible(imp);
            if (!imp.recallEligible || imp.topicAliases == null || imp.topicAliases.Count == 0)
                continue;

            if (ContainsAnyAlias(haystack, imp.topicAliases))
                hits.Add(imp);
        }

        return hits;
    }

    public static RemiFragmentImpression ResolveSingleOrNull(IReadOnlyList<RemiFragmentImpression> hits)
    {
        if (hits == null || hits.Count == 0)
            return null;
        if (hits.Count == 1)
            return hits[0];
        return null;
    }

    public static RemiFragmentImpression SelectByWeight(
        IReadOnlyList<RemiFragmentImpression> hits,
        IReadOnlyDictionary<string, float> relevanceById = null)
    {
        if (hits == null || hits.Count == 0)
            return null;

        RemiFragmentImpression best = hits[0];
        float bestRelevance = GetRelevance(relevanceById, best);
        float bestWeight = best != null ? best.weight : float.MinValue;

        for (int i = 1; i < hits.Count; i++)
        {
            RemiFragmentImpression candidate = hits[i];
            if (candidate == null)
                continue;

            float rel = GetRelevance(relevanceById, candidate);
            if (rel > bestRelevance + 0.0001f)
            {
                best = candidate;
                bestRelevance = rel;
                bestWeight = candidate.weight;
                continue;
            }

            if (Math.Abs(rel - bestRelevance) <= 0.0001f)
            {
                if (candidate.weight > bestWeight + 0.0001f)
                {
                    best = candidate;
                    bestWeight = candidate.weight;
                    continue;
                }

                if (Math.Abs(candidate.weight - bestWeight) <= 0.0001f &&
                    string.CompareOrdinal(candidate.id, best.id) < 0)
                {
                    best = candidate;
                }
            }
        }

        return best;
    }

    public static IEnumerator CoResolveMultiHit(
        string playerText,
        IReadOnlyList<RemiFragmentImpression> hits,
        Action<RemiFragmentImpression, string> onDone)
    {
        if (hits == null || hits.Count < 2)
        {
            onDone?.Invoke(ResolveSingleOrNull(hits), "single_or_empty");
            yield break;
        }

        List<RemiFragmentRecallRelevance.RelevanceScore> scores = null;
        string llmError = null;
        yield return RemiFragmentRecallRelevance.CoScoreHits(
            playerText,
            hits,
            s => scores = s,
            e => llmError = e);

        if (scores == null || scores.Count == 0 || !string.IsNullOrEmpty(llmError))
        {
            RemiFragmentImpression fallback = SelectByWeight(hits);
            Debug.Log(
                $"[RemiFragmentRecall] Multi-hit LLM failed ({llmError ?? "no scores"}), " +
                $"weight fallback → {fallback?.id}");
            onDone?.Invoke(fallback, "weight_fallback");
            yield break;
        }

        var relevanceById = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (RemiFragmentRecallRelevance.RelevanceScore s in scores)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.fragmentId))
                continue;
            relevanceById[s.fragmentId] = s.relevance;
        }

        RemiFragmentImpression picked = SelectByWeight(hits, relevanceById);
        Debug.Log(
            $"[RemiFragmentRecall] Multi-hit LLM → {picked?.id} " +
            $"(hits={hits.Count}, scored={scores.Count})");
        onDone?.Invoke(picked, "llm_relevance");
    }

    private static float GetRelevance(IReadOnlyDictionary<string, float> relevanceById, RemiFragmentImpression imp)
    {
        if (imp == null || relevanceById == null || string.IsNullOrWhiteSpace(imp.id))
            return float.MinValue;
        return relevanceById.TryGetValue(imp.id, out float rel) ? rel : float.MinValue;
    }

    private static bool ContainsAnyAlias(string haystack, IList<string> aliases)
    {
        if (string.IsNullOrEmpty(haystack) || aliases == null)
            return false;

        for (int i = 0; i < aliases.Count; i++)
        {
            string alias = aliases[i];
            if (string.IsNullOrWhiteSpace(alias))
                continue;
            if (haystack.IndexOf(Normalize(alias), StringComparison.Ordinal) >= 0)
                return true;
        }

        return false;
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        return text.Trim().ToLowerInvariant().Replace(" ", "").Replace("　", "");
    }
}
