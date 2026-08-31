using System.Collections.Generic;
using UnityEngine;

/// <summary>从运行时状态组装 Demo EndingPayload。</summary>
public static class RemiDemoEndingPayloadBuilder
{
    public const string PrefsPayloadKey = "RemiDemoEndingPayload";

    public static RemiDemoEndingPayload Build()
    {
        RemiDemoSpineBeat beat = RemiDemoSpineDirector.Instance != null
            ? RemiDemoSpineDirector.Instance.CurrentBeat
            : RemiDemoSpineBeat.NotStarted;

        // 脊柱已过对应 beat 但 Memory 缺条目时补登记（避免回顾直接跳过某日）
        EnsureSpineAlignedExperiences(beat);

        RemiSharedExperienceMemory.EnsureExists();
        IReadOnlyList<RemiSharedExperienceEntry> entries = RemiSharedExperienceMemory.Instance != null
            ? RemiSharedExperienceMemory.Instance.GetRecordedEntriesOrdered()
            : System.Array.Empty<RemiSharedExperienceEntry>();

        RemiPresenceService presence = RemiPresenceService.Instance;
        RemiDialogueDepthStage depth = presence != null
            ? presence.DialogueDepthStage
            : RemiDialogueDepthStage.Surface;

        List<RemiSharedExperienceEntry> shared = CopyEntries(entries);
        // 即使 Presence/Memory 写入失败，也保证回顾列表含应有经历
        BackfillSharedExperiencesForBeat(shared, beat);

        var payload = new RemiDemoEndingPayload
        {
            sharedExperiences = shared,
            route = BuildRoute(beat, shared),
            relationship = BuildRelationship(presence, depth),
            timeline = BuildTimeline(shared),
        };

        payload.bondSlots = BuildBondSlots(payload);
        payload.fragmentMemorySnapshot = CopyFragmentMemory();
        payload.chatFragmentSnapshot = CopyChatFragments();
        payload.closingTemplateFilled = RemiDemoEndingClosingTemplates.BuildDefaultClosing(payload);
        return payload;
    }

    /// <summary>
    /// 按脊柱 beat 补齐共同经历。不依赖 Presence（场景切换后 Instance 可能为空）。
    /// </summary>
    private static void EnsureSpineAlignedExperiences(RemiDemoSpineBeat beat)
    {
        RemiSharedExperienceMemory.EnsureExists();
        RemiSharedExperienceMemory mem = RemiSharedExperienceMemory.Instance;
        if (mem == null)
            return;

        RemiWorldTime wt = ResolveWorldTimeForBackfill();

        if (beat >= RemiDemoSpineBeat.Day1BookSubmitted)
            mem.TryRecord(RemiSharedExperienceId.Day1CommissionBook, WithStoryDay(wt, 1));
        if (beat >= RemiDemoSpineBeat.Day2LibraryIntroDone)
            mem.TryRecord(RemiSharedExperienceId.Day2LibraryCoPresence, WithStoryDay(wt, 2));
        if (beat >= RemiDemoSpineBeat.Day3DeviationAccepted)
            mem.TryRecord(RemiSharedExperienceId.Day3DormDeviation, WithStoryDay(wt, 3));
    }

    private static void BackfillSharedExperiencesForBeat(
        List<RemiSharedExperienceEntry> list,
        RemiDemoSpineBeat beat)
    {
        if (list == null)
            return;

        if (beat >= RemiDemoSpineBeat.Day1BookSubmitted)
            EnsureListHas(list, RemiSharedExperienceId.Day1CommissionBook, 1);
        if (beat >= RemiDemoSpineBeat.Day2LibraryIntroDone)
            EnsureListHas(list, RemiSharedExperienceId.Day2LibraryCoPresence, 2);
        if (beat >= RemiDemoSpineBeat.Day3DeviationAccepted)
            EnsureListHas(list, RemiSharedExperienceId.Day3DormDeviation, 3);

        list.Sort((a, b) =>
        {
            int sa = a != null ? RemiSharedExperienceCatalog.GetSortOrder(a.id) : int.MaxValue;
            int sb = b != null ? RemiSharedExperienceCatalog.GetSortOrder(b.id) : int.MaxValue;
            return sa.CompareTo(sb);
        });
    }

    private static void EnsureListHas(
        List<RemiSharedExperienceEntry> list,
        RemiSharedExperienceId id,
        int storyDay)
    {
        string key = RemiSharedExperienceCatalog.IdKey(id);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].id == key)
                return;
        }

        RemiWorldTime wt = WithStoryDay(ResolveWorldTimeForBackfill(), storyDay);
        list.Add(new RemiSharedExperienceEntry(
            id,
            RemiSharedExperienceCatalog.KindKey(id),
            RemiSharedExperienceCatalog.DefaultFrame(id),
            wt.storyDay,
            wt.phase));
    }

    private static RemiWorldTime ResolveWorldTimeForBackfill()
    {
        if (RemiPresenceService.Instance != null)
            return RemiPresenceService.Instance.WorldTime;

        return new RemiWorldTime
        {
            storyDay = 3,
            phase = RemiDayPhase.Evening,
        };
    }

    private static RemiWorldTime WithStoryDay(RemiWorldTime source, int storyDay)
    {
        return new RemiWorldTime
        {
            storyDay = storyDay,
            phase = source.phase,
        };
    }

    public static void Save(RemiDemoEndingPayload payload)
    {
        if (payload == null)
            return;
        PlayerPrefs.SetString(PrefsPayloadKey, JsonUtility.ToJson(payload));
        PlayerPrefs.Save();
    }

    public static RemiDemoEndingPayload LoadSaved()
    {
        if (!PlayerPrefs.HasKey(PrefsPayloadKey))
            return null;

        try
        {
            return JsonUtility.FromJson<RemiDemoEndingPayload>(PlayerPrefs.GetString(PrefsPayloadKey, ""));
        }
        catch
        {
            return null;
        }
    }

    private static List<RemiSharedExperienceEntry> CopyEntries(IReadOnlyList<RemiSharedExperienceEntry> entries)
    {
        var copy = new List<RemiSharedExperienceEntry>();
        if (entries == null)
            return copy;

        foreach (RemiSharedExperienceEntry entry in entries)
        {
            if (entry == null)
                continue;
            copy.Add(new RemiSharedExperienceEntry
            {
                id = entry.id,
                kind = entry.kind,
                frame = entry.frame,
                storyDay = entry.storyDay,
                phase = entry.phase,
            });
        }

        return copy;
    }

    private static RemiDemoEndingRouteSnapshot BuildRoute(
        RemiDemoSpineBeat beat,
        IReadOnlyList<RemiSharedExperienceEntry> entries)
    {
        var route = new RemiDemoEndingRouteSnapshot
        {
            finalSpineBeat = (int)beat,
            day2LibraryIntroDone = beat >= RemiDemoSpineBeat.Day2LibraryIntroDone,
            day3DeviationAccepted = beat >= RemiDemoSpineBeat.Day3DeviationAccepted,
            deviationKind = (int)ResolveDeviationKind(entries),
        };

        route.missedExperienceIds = BuildMissedExperienceIds(beat, entries);
        return route;
    }

    private static RemiDemoRouteDeviationKind ResolveDeviationKind(IReadOnlyList<RemiSharedExperienceEntry> entries)
    {
        if (entries == null)
            return RemiDemoRouteDeviationKind.None;

        foreach (RemiSharedExperienceEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                continue;

            if (entry.id == RemiSharedExperienceCatalog.IdKey(RemiSharedExperienceId.Day3DormDeviation))
                return RemiDemoRouteDeviationKind.Dorm;
        }

        return RemiDemoRouteDeviationKind.None;
    }

    private static List<string> BuildMissedExperienceIds(
        RemiDemoSpineBeat beat,
        IReadOnlyList<RemiSharedExperienceEntry> entries)
    {
        var recorded = new HashSet<string>();
        if (entries != null)
        {
            foreach (RemiSharedExperienceEntry entry in entries)
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.id))
                    recorded.Add(entry.id);
            }
        }

        var missed = new List<string>();
        if (beat >= RemiDemoSpineBeat.Day1BookSubmitted &&
            !recorded.Contains(RemiSharedExperienceCatalog.IdKey(RemiSharedExperienceId.Day1CommissionBook)))
        {
            missed.Add(RemiSharedExperienceCatalog.IdKey(RemiSharedExperienceId.Day1CommissionBook));
        }

        if (beat >= RemiDemoSpineBeat.Day2LibraryIntroDone &&
            !recorded.Contains(RemiSharedExperienceCatalog.IdKey(RemiSharedExperienceId.Day2LibraryCoPresence)))
        {
            missed.Add(RemiSharedExperienceCatalog.IdKey(RemiSharedExperienceId.Day2LibraryCoPresence));
        }

        if (beat >= RemiDemoSpineBeat.Day3DeviationAccepted)
        {
            bool hasDeviation = recorded.Contains(RemiSharedExperienceCatalog.IdKey(RemiSharedExperienceId.Day3DormDeviation));
            if (!hasDeviation)
                missed.Add("day3_deviation");
        }

        return missed;
    }

    private static RemiDemoEndingRelationshipSnapshot BuildRelationship(
        RemiPresenceService presence,
        RemiDialogueDepthStage depth)
    {
        return new RemiDemoEndingRelationshipSnapshot
        {
            depthStage = (int)depth,
            delegationMilestoneCount = presence != null ? presence.DelegationMilestoneCountForGate : 0,
            hasInfluentialGate = depth >= RemiDialogueDepthStage.Influential,
        };
    }

    private static RemiDemoEndingTimeline BuildTimeline(IReadOnlyList<RemiSharedExperienceEntry> entries)
    {
        var timeline = new RemiDemoEndingTimeline
        {
            eventCount = entries?.Count ?? 0,
        };

        if (entries == null || entries.Count == 0)
            return timeline;

        int first = int.MaxValue;
        int last = 0;
        foreach (RemiSharedExperienceEntry entry in entries)
        {
            if (entry == null || entry.storyDay <= 0)
                continue;
            if (entry.storyDay < first)
                first = entry.storyDay;
            if (entry.storyDay > last)
                last = entry.storyDay;
        }

        if (first != int.MaxValue)
        {
            timeline.firstStoryDay = first;
            timeline.lastStoryDay = last;
        }

        return timeline;
    }

    private static RemiDemoEndingBondSlots BuildBondSlots(RemiDemoEndingPayload payload)
    {
        string routeFallback = RemiDemoEndingClosingTemplates.ResolveRouteBaseLine(payload);
        var slots = new RemiDemoEndingBondSlots
        {
            // 宁缺毋编：不再写入 depth 夸夸 traitFallback。
            traitFallback = "",
            routeReflectionFallback = routeFallback,
            routeReflection = routeFallback,
        };

        RemiFragmentMemory.EnsureExists();
        IReadOnlyList<RemiFragmentImpression> impressions = RemiFragmentMemory.Instance != null
            ? RemiFragmentMemory.Instance.GetImpressionsOrdered()
            : null;

        List<RemiFragmentImpression> selected = RemiDemoEndingBondSelection.SelectForBond(
            impressions,
            RemiDemoEndingBondSelection.DefaultMaxSelected);

        foreach (RemiFragmentImpression impression in selected)
            slots.selectedImpressions.Add(CloneImpression(impression));

        slots.hasBondPresentation = slots.selectedImpressions.Count > 0;
        if (slots.hasBondPresentation)
        {
            string visible = RemiChatFragmentQuotePolicy.ResolvePlayerVisibleLine(
                slots.selectedImpressions[0]);
            if (!string.IsNullOrWhiteSpace(visible))
                slots.traitPrimary = visible;
        }

        // Resonance 提示：进 Mode B brief，不单独成页、不改关系状态。
        if (impressions != null)
        {
            foreach (RemiFragmentImpression impression in impressions)
            {
                if (impression == null || !impression.HasResonanceTag())
                    continue;
                if (string.IsNullOrWhiteSpace(impression.resonanceHint))
                    continue;
                string keyword = impression.resonanceHint.Trim();
                if (!slots.insideJokeKeywords.Contains(keyword))
                    slots.insideJokeKeywords.Add(keyword);
            }
        }

        return slots;
    }

    private static List<RemiFragmentImpression> CopyFragmentMemory()
    {
        RemiFragmentMemory.EnsureExists();
        var copy = new List<RemiFragmentImpression>();
        if (RemiFragmentMemory.Instance == null)
            return copy;

        foreach (RemiFragmentImpression impression in RemiFragmentMemory.Instance.GetImpressionsOrdered())
        {
            if (impression == null)
                continue;
            copy.Add(CloneImpression(impression));
        }

        return copy;
    }

    private static RemiFragmentImpression CloneImpression(RemiFragmentImpression src)
    {
        var clone = new RemiFragmentImpression
        {
            id = src.id,
            summary = src.summary,
            storyDay = src.storyDay,
            atmosphere = src.atmosphere,
            weight = src.weight,
            weightReason = src.weightReason,
            intrinsicStrength = src.intrinsicStrength,
            quote = src.quote,
            quoteCiteEligible = src.quoteCiteEligible,
            resonanceHint = src.resonanceHint,
            promotedUnixMs = src.promotedUnixMs,
            sourceUnitId = src.sourceUnitId,
        };
        if (src.meaningTags != null)
            clone.meaningTags = new List<string>(src.meaningTags);
        if (src.weightBreakdown != null)
        {
            clone.weightBreakdown = new RemiFragmentWeightBreakdown
            {
                semantic = src.weightBreakdown.semantic,
                repetition = src.weightBreakdown.repetition,
                novelty = src.weightBreakdown.novelty,
                crossDay = src.weightBreakdown.crossDay,
                eventAffinity = src.weightBreakdown.eventAffinity,
                endingProximity = src.weightBreakdown.endingProximity,
            };
        }

        return clone;
    }

    private static List<RemiChatFragmentEntry> CopyChatFragments()
    {
        RemiChatFragmentMemory.EnsureExists();
        IReadOnlyList<RemiChatFragmentEntry> fragments = RemiChatFragmentMemory.Instance != null
            ? RemiChatFragmentMemory.Instance.GetEntriesOrdered()
            : null;

        var copy = new List<RemiChatFragmentEntry>();
        if (fragments == null)
            return copy;

        foreach (RemiChatFragmentEntry entry in fragments)
        {
            if (entry == null)
                continue;
            entry.EnsureTagsMigrated();
            copy.Add(new RemiChatFragmentEntry
            {
                id = entry.id,
                summary = entry.summary,
                tagsCsv = entry.tagsCsv,
                atmosphere = entry.atmosphere,
                channel = entry.channel,
                storyDay = entry.storyDay,
                phase = entry.phase,
                hitCount = entry.hitCount,
                weight = entry.weight,
                quote = entry.quote,
                quoteCiteEligible = entry.quoteCiteEligible,
            });
        }

        return copy;
    }

}
