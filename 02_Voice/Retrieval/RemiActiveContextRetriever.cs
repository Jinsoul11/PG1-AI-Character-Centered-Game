using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Voice Actor Context：按玩家话语 + Presence 唤醒 ACTIVE_KNOWLEDGE / ACTIVE_MEMORY。
/// ACTIVE_MEMORY 走 Fragment 别名召回；多命中 LLM relevance，失败/并列 weight 保底。
/// </summary>
public static class RemiActiveContextRetriever
{
    public const int MaxKnowledge = 2;
    public const float AliasScore = 10f;
    public const float StickyScore = 4f;
    public const float EpisodeScore = 6f;
    public const float SceneScore = 1.5f;
    public const float MinKnowledgeInject = 4f;

    public static RemiActiveContextResult RetrieveKnowledgeOnly(
        string playerText,
        RemiPresenceService presence,
        IReadOnlyList<string> stickyKnowledgeIds = null)
    {
        var result = new RemiActiveContextResult();
        string haystack = Normalize(playerText);
        ScoreKnowledge(haystack, presence, stickyKnowledgeIds, result);
        return result;
    }

    /// <summary>写入 PromptContext，供 Voice 组装读取（含多命中 LLM）。</summary>
    public static IEnumerator CoPrepareVoiceContext(string playerText)
    {
        PromptContextManager ctx = PromptContextManager.Instance;
        if (ctx == null)
            yield break;

        RemiPresenceService presence = RemiPresenceService.Instance;
        RemiActiveContextResult result = RetrieveKnowledgeOnly(
            playerText,
            presence,
            ctx.StickyKnowledgeIds);

        string haystack = Normalize(playerText);
        int currentDay = presence != null && presence.WorldTime.IsStoryStarted
            ? presence.WorldTime.storyDay
            : 0;

        List<RemiFragmentImpression> hits =
            RemiFragmentRecallService.ScanAliasHits(haystack, currentDay);

        RemiFragmentImpression selected = null;
        if (hits.Count == 0)
        {
            selected = null;
        }
        else if (hits.Count == 1)
        {
            selected = hits[0];
            Debug.Log($"[RemiActiveContext] Fragment recall single-hit → {selected.id}");
        }
        else
        {
            string resolveMode = null;
            yield return RemiFragmentRecallService.CoResolveMultiHit(
                playerText,
                hits,
                (imp, mode) =>
                {
                    selected = imp;
                    resolveMode = mode;
                });
            Debug.Log(
                $"[RemiActiveContext] Fragment recall multi-hit ({hits.Count}) " +
                $"mode={resolveMode} → {selected?.id}");
        }

        if (selected != null)
            result.FragmentMemories.Add(selected);

        ctx.SetActiveKnowledgeBlock(result.BuildKnowledgeBlock());
        ctx.SetActiveMemoryBlock(result.BuildMemoryBlock());
        ctx.SetStickyKnowledgeIds(result.SelectedKnowledgeIds);
    }

    /// <summary>同步检索（仅 Knowledge；Memory 请用 CoPrepareVoiceContext）。</summary>
    public static RemiActiveContextResult Retrieve(
        string playerText,
        RemiPresenceService presence,
        IReadOnlyList<string> stickyKnowledgeIds = null)
    {
        return RetrieveKnowledgeOnly(playerText, presence, stickyKnowledgeIds);
    }

    private static void ScoreKnowledge(
        string haystack,
        RemiPresenceService presence,
        IReadOnlyList<string> stickyKnowledgeIds,
        RemiActiveContextResult result)
    {
        var scored = new List<(RemiKnowledgeCard card, float score)>();
        string episodeKey = presence != null
            ? EpisodeKey(presence.CurrentEpisodeKind)
            : "";
        string placeKey = presence != null
            ? LocationKey(presence.CurrentLocation)
            : "";
        string activityKey = presence != null
            ? ActivityKey(presence.CurrentActivity)
            : "";

        foreach (RemiKnowledgeCard card in RemiKnowledgeCatalog.GetAll())
        {
            if (card == null || string.IsNullOrWhiteSpace(card.id))
                continue;

            float score = 0f;
            bool aliasHit = ContainsAnyAlias(haystack, card.aliases);
            if (aliasHit)
                score += AliasScore + card.priority;

            bool stickyHit = false;
            if (stickyKnowledgeIds != null)
            {
                for (int i = 0; i < stickyKnowledgeIds.Count; i++)
                {
                    if (string.Equals(stickyKnowledgeIds[i], card.id, StringComparison.Ordinal))
                    {
                        stickyHit = true;
                        score += StickyScore;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(episodeKey) && ContainsTag(card.episodeKinds, episodeKey))
                score += EpisodeScore;

            if (!string.IsNullOrEmpty(placeKey) && ContainsTag(card.sceneTags, placeKey))
                score += SceneScore;
            if (!string.IsNullOrEmpty(activityKey) && ContainsTag(card.sceneTags, activityKey))
                score += SceneScore;

            if ((aliasHit || stickyHit) && score >= MinKnowledgeInject)
                scored.Add((card, score));
        }

        scored.Sort((a, b) => b.score.CompareTo(a.score));
        int take = Mathf.Min(MaxKnowledge, scored.Count);
        for (int i = 0; i < take; i++)
        {
            result.Knowledge.Add(scored[i].card);
            result.SelectedKnowledgeIds.Add(scored[i].card.id);
        }
    }

    private static bool ContainsAnyAlias(string haystack, string[] aliases)
    {
        if (string.IsNullOrEmpty(haystack) || aliases == null)
            return false;
        for (int i = 0; i < aliases.Length; i++)
        {
            string a = aliases[i];
            if (string.IsNullOrWhiteSpace(a))
                continue;
            if (haystack.IndexOf(Normalize(a), StringComparison.Ordinal) >= 0)
                return true;
        }

        return false;
    }

    private static bool ContainsTag(string[] tags, string key)
    {
        if (tags == null || string.IsNullOrEmpty(key))
            return false;
        for (int i = 0; i < tags.Length; i++)
        {
            if (string.Equals(tags[i], key, StringComparison.OrdinalIgnoreCase))
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

    private static string EpisodeKey(RemiPhaseEpisodeKind kind) =>
        kind switch
        {
            RemiPhaseEpisodeKind.Commission => "commission",
            RemiPhaseEpisodeKind.CoPresence => "co_presence",
            RemiPhaseEpisodeKind.DeviationSession => "deviation",
            _ => "default",
        };

    private static string LocationKey(RemiLocation loc) =>
        loc switch
        {
            RemiLocation.Library => "library",
            RemiLocation.Classroom => "classroom",
            RemiLocation.Dorm => "dorm",
            _ => loc.ToString().ToLowerInvariant(),
        };

    private static string ActivityKey(RemiActivity act) =>
        act switch
        {
            RemiActivity.InClass => "in_class",
            RemiActivity.Free => "free",
            RemiActivity.Busy => "busy",
            _ => act.ToString().ToLowerInvariant(),
        };
}
