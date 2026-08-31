using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Debug：Archive 直写 → 一键跑 Fragment 全链 → 产出 Bond 看法与分阶段报告。
/// F10 = 专项库（Reveal 等，不覆盖）；F11 = Depth 验收计划库。
/// </summary>
[DisallowMultipleComponent]
public class RemiFragmentPipelineTestRunner : MonoBehaviour
{
    public static RemiFragmentPipelineTestRunner Instance { get; private set; }

    [Header("触发")]
    [SerializeField] private KeyCode specialHotkey = KeyCode.F10;
    [SerializeField] private KeyCode depthPlanHotkey = KeyCode.F11;
    [Tooltip("按住 Shift + F10/F11：切换当前库用例（跳过 Custom）。")]
    [SerializeField] private bool enableFixtureCycleHotkey = true;
    [SerializeField] private bool enableHotkey = true;

    [Header("专项库 · F10（原文保留）")]
    [SerializeField] private RemiFragmentPipelineTestFixtureId specialFixture =
        RemiFragmentPipelineTestFixtureId.Reveal;
    [SerializeField] private List<RemiFragmentPipelineTestSeedLine> specialCustomLines =
        new List<RemiFragmentPipelineTestSeedLine>();

    [Header("Depth 计划库 · F11")]
    [SerializeField] private RemiFragmentPipelineDepthFixtureId depthFixture =
        RemiFragmentPipelineDepthFixtureId.BlindExhibitReconcileD1;
    [SerializeField] private List<RemiFragmentPipelineTestSeedLine> depthCustomLines =
        new List<RemiFragmentPipelineTestSeedLine>();

    [Header("共用")]
    [SerializeField] private int defaultStoryDay = 1;
    [SerializeField] private bool clearArchiveBeforeSeed = true;
    [SerializeField] private bool clearDownstreamBeforeSettle = true;
    [SerializeField] private bool runBondCompose = true;
    [SerializeField] private int bondMaxImpressions = RemiDemoEndingBondSelection.DefaultMaxSelected;
    [SerializeField] private bool copyReportJsonToClipboard = true;
    [SerializeField] private bool logFullJson = true;

    [Header("依赖（可空）")]
    [SerializeField] private PromptedDialogueAgent promptedAgent;

    private bool _busy;
    private RemiFragmentPipelineTestReport _lastReport;
    private string _lastReportJson = "";

    public bool IsBusy => _busy;
    public RemiFragmentPipelineTestReport LastReport => _lastReport;
    public string LastReportJson => _lastReportJson;
    public RemiFragmentPipelineTestFixtureId SpecialFixture => specialFixture;
    public RemiFragmentPipelineDepthFixtureId DepthFixture => depthFixture;
    public string SpecialFixtureLabel => RemiFragmentPipelineTestFixtures.DisplayName(specialFixture);
    public string DepthFixtureLabel => RemiFragmentPipelineDepthFixtures.DisplayName(depthFixture);

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiFragmentPipelineTestRunner));
        go.AddComponent<RemiFragmentPipelineTestRunner>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!enableHotkey || _busy)
            return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (enableFixtureCycleHotkey && shift)
        {
            if (Input.GetKeyDown(specialHotkey))
                CycleSpecialFixture(+1);
            else if (Input.GetKeyDown(depthPlanHotkey))
                CycleDepthFixture(+1);
            return;
        }

        if (Input.GetKeyDown(specialHotkey))
            RunSpecialPipelineTest();
        else if (Input.GetKeyDown(depthPlanHotkey))
            RunDepthPlanPipelineTest();
    }

    public void SetSpecialFixture(RemiFragmentPipelineTestFixtureId id)
    {
        specialFixture = id;
        Debug.Log($"[FragmentPipelineTest] F10 fixture → {SpecialFixtureLabel}");
    }

    public void SetDepthFixture(RemiFragmentPipelineDepthFixtureId id)
    {
        depthFixture = id;
        Debug.Log($"[FragmentPipelineTest] F11 fixture → {DepthFixtureLabel}");
    }

    public void CycleSpecialFixture(int delta)
    {
        RemiFragmentPipelineTestFixtureId next =
            RemiFragmentPipelineTestFixtures.Cycle(specialFixture, delta);
        SetSpecialFixture(next);
    }

    public void CycleDepthFixture(int delta)
    {
        RemiFragmentPipelineDepthFixtureId next =
            RemiFragmentPipelineDepthFixtures.Cycle(depthFixture, delta);
        SetDepthFixture(next);
    }

    /// <summary>F10：专项库（Reveal 等）。</summary>
    [ContextMenu("Run Special Pipeline Test (F10)")]
    public void RunSpecialPipelineTest()
    {
        if (_busy)
        {
            Debug.LogWarning("[FragmentPipelineTest] 已在运行中。");
            return;
        }

        StartCoroutine(CoRun(useDepthLibrary: false));
    }

    /// <summary>F11：Depth 验收计划库。</summary>
    [ContextMenu("Run Depth Plan Pipeline Test (F11)")]
    public void RunDepthPlanPipelineTest()
    {
        if (_busy)
        {
            Debug.LogWarning("[FragmentPipelineTest] 已在运行中。");
            return;
        }

        StartCoroutine(CoRun(useDepthLibrary: true));
    }

    /// <summary>兼容旧调用：默认跑专项库。</summary>
    [ContextMenu("Run Fragment Pipeline Test")]
    public void RunPipelineTest() => RunSpecialPipelineTest();

    [ContextMenu("Log Last Report JSON")]
    public void LogLastReport()
    {
        if (string.IsNullOrEmpty(_lastReportJson))
        {
            Debug.Log("[FragmentPipelineTest] 尚无报告。");
            return;
        }

        Debug.Log(_lastReportJson);
    }

    private IEnumerator CoRun(bool useDepthLibrary)
    {
        _busy = true;
        string fixtureLabel = useDepthLibrary
            ? RemiFragmentPipelineDepthFixtures.DisplayName(depthFixture)
            : RemiFragmentPipelineTestFixtures.DisplayName(specialFixture);

        var report = new RemiFragmentPipelineTestReport
        {
            runId = Guid.NewGuid().ToString("N").Substring(0, 8),
            fixtureId = fixtureLabel,
            startedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        report.selection.maxSelected = Mathf.Max(1, bondMaxImpressions);

        var totalSw = Stopwatch.StartNew();
        // C#：带 catch 的 try 内不可 yield；仅用 try/finally。
        try
        {
            EnsurePipelineStores();
            if (promptedAgent == null)
                promptedAgent = FindObjectOfType<PromptedDialogueAgent>();

            List<RemiFragmentPipelineTestSeedLine> seeds = ResolveSeeds(useDepthLibrary);
            SeedArchive(seeds, report);

            var daySet = CollectStoryDays(seeds);
            if (daySet.Count == 0)
                daySet.Add(Mathf.Max(1, defaultStoryDay));

            foreach (int d in daySet)
                report.storyDays.Add(d);
            report.storyDays.Sort();

            if (clearDownstreamBeforeSettle)
                ClearDownstreamMemory();

            foreach (int day in report.storyDays)
            {
                var dayReport = new RemiFragmentPipelineTestDayReport { storyDay = day };
                yield return CoSettleOneDay(day, dayReport, report);
                report.days.Add(dayReport);
            }

            SnapshotFragmentMemory(report);
            BuildSelection(report);

            if (runBondCompose && report.selection.hasBondPresentation)
            {
                yield return CoComposeBond(report);
            }
            else if (!report.selection.hasBondPresentation)
            {
                report.bond.source = "skipped";
                report.bond.skipReason = "no_eligible_impressions";
            }
            else
            {
                ApplyHonestBondFallback(report, "runBondCompose_disabled");
            }

            report.success = true;
            report.status = report.selection.hasBondPresentation
                ? $"ok · bond={report.bond.source}"
                : "ok · bond skipped";
        }
        finally
        {
            totalSw.Stop();
            report.durationMs = totalSw.ElapsedMilliseconds;
            report.finishedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (!report.success && string.IsNullOrEmpty(report.status))
                report.status = "failed_or_aborted";
            FinalizeAndPublish(report);
            _busy = false;
        }
    }

    private List<RemiFragmentPipelineTestSeedLine> ResolveSeeds(bool useDepthLibrary)
    {
        if (useDepthLibrary)
        {
            if (depthFixture == RemiFragmentPipelineDepthFixtureId.Custom)
                return depthCustomLines != null
                    ? new List<RemiFragmentPipelineTestSeedLine>(depthCustomLines)
                    : new List<RemiFragmentPipelineTestSeedLine>();
            return RemiFragmentPipelineDepthFixtures.Build(depthFixture, defaultStoryDay);
        }

        if (specialFixture == RemiFragmentPipelineTestFixtureId.Custom)
            return specialCustomLines != null
                ? new List<RemiFragmentPipelineTestSeedLine>(specialCustomLines)
                : new List<RemiFragmentPipelineTestSeedLine>();

        return RemiFragmentPipelineTestFixtures.Build(specialFixture, defaultStoryDay);
    }

    private static HashSet<int> CollectStoryDays(List<RemiFragmentPipelineTestSeedLine> seeds)
    {
        var set = new HashSet<int>();
        if (seeds == null)
            return set;
        foreach (RemiFragmentPipelineTestSeedLine line in seeds)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.content))
                continue;
            set.Add(Mathf.Max(1, line.storyDay));
        }

        return set;
    }

    private void SeedArchive(
        List<RemiFragmentPipelineTestSeedLine> seeds,
        RemiFragmentPipelineTestReport report)
    {
        RemiDialogueArchive.EnsureExists();
        if (RemiDialogueArchive.Instance == null)
            throw new InvalidOperationException("RemiDialogueArchive missing");

        if (clearArchiveBeforeSeed)
            RemiDialogueArchive.Instance.ClearAll();

        int written = 0;
        if (seeds != null)
        {
            foreach (RemiFragmentPipelineTestSeedLine line in seeds)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.content))
                    continue;

                int day = Mathf.Max(1, line.storyDay > 0 ? line.storyDay : defaultStoryDay);
                string speaker = string.IsNullOrWhiteSpace(line.speaker) ? "player" : line.speaker;
                RemiDialogueArchive.Instance.RecordExplicit(
                    speaker,
                    line.content.Trim(),
                    day,
                    RemiDialogueArchiveSource.FreeChat);

                written++;
                if (report.archiveSamples.Count < 12)
                {
                    report.archiveSamples.Add(new RemiFragmentPipelineTestLineSnap
                    {
                        storyDay = day,
                        speaker = speaker,
                        content = Truncate(line.content.Trim(), 80),
                    });
                }
            }
        }

        report.archiveWrittenCount = written;
        Debug.Log($"[FragmentPipelineTest] Seeded Archive ×{written} (fixture={report.fixtureId})");
    }

    private static void EnsurePipelineStores()
    {
        RemiDialogueArchive.EnsureExists();
        RemiMemoryDaySettlement.EnsureExists();
        RemiMemoryCurator.EnsureExists();
        RemiMemoryCuratorStore.EnsureExists();
        RemiFragmentUnitStore.EnsureExists();
        RemiFragmentAnalyzer.EnsureExists();
        RemiFragmentMemory.EnsureExists();
    }

    private static void ClearDownstreamMemory()
    {
        RemiMemoryCuratorStore.EnsureExists();
        RemiFragmentUnitStore.EnsureExists();
        RemiFragmentMemory.EnsureExists();
        RemiMemoryCuratorStore.Instance?.ClearAll();
        RemiFragmentUnitStore.Instance?.ClearAll();
        RemiFragmentMemory.Instance?.ClearAll();
        Debug.Log("[FragmentPipelineTest] Cleared Curator / Unit / FragmentMemory stores.");
    }

    private IEnumerator CoSettleOneDay(
        int day,
        RemiFragmentPipelineTestDayReport dayReport,
        RemiFragmentPipelineTestReport report)
    {
        // Filter snapshot (pre-curator)
        RemiDialogueCandidateFilter.FilterResult filter =
            RemiDialogueCandidateFilter.FilterStoryDay(day);
        dayReport.filter.keptCount = filter.KeptCount;
        dayReport.filter.rejectedCount = filter.RejectedCount;
        int keepShow = Mathf.Min(5, filter.KeptCount);
        for (int i = 0; i < keepShow; i++)
        {
            RemiDialogueArchiveEntry e = filter.Kept[i].Entry;
            dayReport.filter.keptSamples.Add(Truncate(e?.content, 60));
        }

        int rejectShow = Mathf.Min(8, filter.RejectedCount);
        for (int i = 0; i < rejectShow; i++)
        {
            RemiDialogueCandidateFilter.Rejection r = filter.Rejected[i];
            dayReport.filter.rejectReasons.Add(
                $"{r.Reason}: {Truncate(r.Entry?.content, 40)}");
        }

        // Curator
        var curatorCall = new RemiFragmentPipelineTestLlmCall
        {
            stage = "Curator",
            storyDay = day,
        };
        var curatorSw = Stopwatch.StartNew();
        RemiMemoryCuratorDayResult curatorResult = null;
        RemiMemoryCurator.EnsureExists();
        if (RemiMemoryCurator.Instance == null)
        {
            curatorCall.ok = false;
            curatorCall.error = "Curator missing";
            dayReport.curator.success = false;
            dayReport.curator.error = curatorCall.error;
        }
        else
        {
            yield return RemiMemoryCurator.Instance.CoCurateStoryDay(day, r => curatorResult = r);
            curatorSw.Stop();
            curatorCall.ms = curatorSw.ElapsedMilliseconds;
            curatorCall.ok = curatorResult != null && curatorResult.success;
            curatorCall.error = curatorResult?.error ?? "";
            dayReport.curator.success = curatorCall.ok;
            dayReport.curator.error = curatorCall.error;
            if (curatorResult?.candidates != null)
            {
                dayReport.curator.candidateCount = curatorResult.candidates.Count;
                foreach (RemiMemoryCuratorCandidate c in curatorResult.candidates)
                {
                    if (c != null && !string.IsNullOrWhiteSpace(c.summary))
                        dayReport.curator.summaries.Add(Truncate(c.summary, 80));
                }
            }

            if (curatorCall.ok)
            {
                if (dayReport.curator.candidateCount == 0 && dayReport.filter.keptCount == 0)
                    curatorCall.error = "llm_skipped_empty_filter";
                RemiFragmentUnitBuilder.MaterializeFromCuratorDay(curatorResult);
            }
            else if (curatorResult == null)
            {
                curatorCall.error = string.IsNullOrEmpty(curatorCall.error)
                    ? "curator_null_result"
                    : curatorCall.error;
                dayReport.curator.error = curatorCall.error;
            }
        }

        report.llmCalls.Add(curatorCall);

        // Units snapshot
        RemiFragmentUnitStore.EnsureExists();
        List<RemiFragmentUnit> units = RemiFragmentUnitStore.Instance != null
            ? RemiFragmentUnitStore.Instance.GetUnitsForStoryDay(day)
            : new List<RemiFragmentUnit>();
        dayReport.units.count = units.Count;
        foreach (RemiFragmentUnit u in units)
        {
            if (u != null && !string.IsNullOrWhiteSpace(u.id))
                dayReport.units.ids.Add(u.id);
        }

        // Analyzer
        var analyzerCall = new RemiFragmentPipelineTestLlmCall
        {
            stage = "Analyzer",
            storyDay = day,
        };
        var analyzerSw = Stopwatch.StartNew();
        RemiFragmentAnalyzerDayResult analyzeResult = null;
        RemiFragmentAnalyzer.EnsureExists();
        if (RemiFragmentAnalyzer.Instance == null)
        {
            analyzerCall.ok = false;
            analyzerCall.error = "Analyzer missing";
            dayReport.analyzer.success = false;
            dayReport.analyzer.error = analyzerCall.error;
        }
        else if (units.Count == 0)
        {
            analyzerSw.Stop();
            analyzerCall.ms = analyzerSw.ElapsedMilliseconds;
            analyzerCall.ok = true;
            analyzerCall.error = "no_units";
            dayReport.analyzer.success = true;
            dayReport.analyzer.error = "no_units";
            // Still promote in case leftovers
            dayReport.promotedCount = RemiFragmentMemoryPromoter.PromoteStoryDay(day);
        }
        else
        {
            yield return RemiFragmentAnalyzer.Instance.CoAnalyzeStoryDay(
                day,
                r => analyzeResult = r,
                forceReanalyze: true);
            analyzerSw.Stop();
            analyzerCall.ms = analyzerSw.ElapsedMilliseconds;
            analyzerCall.ok = analyzeResult != null && analyzeResult.success;
            analyzerCall.error = analyzeResult?.error ?? "";
            dayReport.analyzer.success = analyzerCall.ok;
            dayReport.analyzer.error = analyzerCall.error;
            dayReport.analyzer.analyzedCount = analyzeResult?.analyzedCount ?? 0;
            dayReport.promotedCount = RemiFragmentMemoryPromoter.PromoteStoryDay(day);
        }

        report.llmCalls.Add(analyzerCall);

        // Refresh units after analyze for report
        units = RemiFragmentUnitStore.Instance != null
            ? RemiFragmentUnitStore.Instance.GetUnitsForStoryDay(day)
            : new List<RemiFragmentUnit>();
        foreach (RemiFragmentUnit u in units)
        {
            if (u == null)
                continue;
            dayReport.analyzer.units.Add(new RemiFragmentPipelineTestUnitSnap
            {
                id = u.id,
                summary = Truncate(u.summary, 80),
                tags = u.meaningTags != null ? string.Join(",", u.meaningTags) : "",
                intrinsic = u.intrinsicStrength,
                weight = u.weight,
                weightReason = Truncate(u.weightReason, 100),
            });
        }

        Debug.Log(
            $"[FragmentPipelineTest] Day {day}: filter={dayReport.filter.keptCount} " +
            $"curator={dayReport.curator.candidateCount} units={dayReport.units.count} " +
            $"promoted={dayReport.promotedCount}");
    }

    private static void SnapshotFragmentMemory(RemiFragmentPipelineTestReport report)
    {
        RemiFragmentMemory.EnsureExists();
        if (RemiFragmentMemory.Instance == null)
            return;

        foreach (RemiFragmentImpression impression in RemiFragmentMemory.Instance.GetImpressionsOrdered())
        {
            if (impression == null)
                continue;
            report.fragmentMemory.Add(ToImpressionSnap(impression));
        }
    }

    private void BuildSelection(RemiFragmentPipelineTestReport report)
    {
        RemiFragmentMemory.EnsureExists();
        IReadOnlyList<RemiFragmentImpression> all = RemiFragmentMemory.Instance != null
            ? RemiFragmentMemory.Instance.GetImpressionsOrdered()
            : Array.Empty<RemiFragmentImpression>();

        int max = Mathf.Max(1, bondMaxImpressions);
        report.selection.maxSelected = max;

        List<RemiFragmentImpression> selected =
            RemiDemoEndingBondSelection.SelectForBond(all, max);
        report.selection.hasBondPresentation = selected.Count > 0;

        foreach (RemiFragmentImpression impression in selected)
            report.selection.selected.Add(ToImpressionSnap(impression));

        foreach (RemiFragmentImpression impression in all)
        {
            if (impression == null)
                continue;
            if (selected.Exists(s => s != null && s.id == impression.id))
                continue;

            string reason;
            if (string.IsNullOrWhiteSpace(
                    RemiChatFragmentQuotePolicy.ResolvePlayerVisibleLine(impression)))
                reason = "empty_summary";
            else if (!RemiDemoEndingBondSelection.IsEligibleForBond(impression))
                reason = "not_eligible";
            else
                reason = "below_top_k";

            report.selection.rejectedWithReason.Add(
                $"{impression.id} w={impression.weight:0.00} · {reason} · " +
                Truncate(impression.summary, 40));
        }
    }

    private IEnumerator CoComposeBond(RemiFragmentPipelineTestReport report)
    {
        List<RemiFragmentImpression> selected = new List<RemiFragmentImpression>();
        RemiFragmentMemory.EnsureExists();
        IReadOnlyList<RemiFragmentImpression> all = RemiFragmentMemory.Instance != null
            ? RemiFragmentMemory.Instance.GetImpressionsOrdered()
            : Array.Empty<RemiFragmentImpression>();
        List<RemiFragmentImpression> picked =
            RemiDemoEndingBondSelection.SelectForBond(all, Mathf.Max(1, bondMaxImpressions));
        selected.AddRange(picked);

        string brief = RemiDemoEndingBondSelection.BuildComposeSystemContext(selected);
        string fallback = RemiDemoEndingBondSelection.BuildHonestFallbackLine(selected);
        report.bond.brief = brief;

        var bondCall = new RemiFragmentPipelineTestLlmCall
        {
            stage = "Bond",
            storyDay = report.storyDays.Count > 0 ? report.storyDays[report.storyDays.Count - 1] : 0,
        };

        if (promptedAgent == null)
        {
            bondCall.ok = false;
            bondCall.error = "PromptedDialogueAgent missing";
            report.llmCalls.Add(bondCall);
            ApplyHonestBondFallback(report, bondCall.error);
            yield break;
        }

        var sw = Stopwatch.StartNew();
        bool done = false;
        string lastText = null;
        string lastError = null;

        yield return promptedAgent.SendSystem(
            brief,
            (text, expr) =>
            {
                lastText = text;
                done = true;
            },
            err =>
            {
                lastError = err;
                done = true;
            },
            null,
            RemiPromptAssemblyMode.EndingSpeak);

        while (!done)
            yield return null;

        sw.Stop();
        bondCall.ms = sw.ElapsedMilliseconds;

        if (!string.IsNullOrWhiteSpace(lastText))
        {
            bondCall.ok = true;
            report.bond.source = "llm";
            report.bond.line = lastText.Trim();
        }
        else
        {
            bondCall.ok = false;
            bondCall.error = string.IsNullOrWhiteSpace(lastError) ? "empty_response" : lastError;
            report.bond.llmError = bondCall.error;
            report.bond.source = "honest_fallback";
            report.bond.line = fallback;
            report.bond.skipReason = "";
        }

        report.llmCalls.Add(bondCall);
    }

    private static void ApplyHonestBondFallback(RemiFragmentPipelineTestReport report, string reason)
    {
        RemiFragmentMemory.EnsureExists();
        IReadOnlyList<RemiFragmentImpression> all = RemiFragmentMemory.Instance != null
            ? RemiFragmentMemory.Instance.GetImpressionsOrdered()
            : Array.Empty<RemiFragmentImpression>();
        List<RemiFragmentImpression> selected = RemiDemoEndingBondSelection.SelectForBond(
            all,
            report.selection.maxSelected > 0
                ? report.selection.maxSelected
                : RemiDemoEndingBondSelection.DefaultMaxSelected);

        report.bond.line = RemiDemoEndingBondSelection.BuildHonestFallbackLine(selected);
        report.bond.brief = RemiDemoEndingBondSelection.BuildComposeSystemContext(selected);
        if (string.IsNullOrWhiteSpace(report.bond.line))
        {
            report.bond.source = "skipped";
            report.bond.skipReason = reason;
        }
        else
        {
            report.bond.source = "honest_fallback";
            report.bond.skipReason = reason;
        }
    }

    private void FinalizeAndPublish(RemiFragmentPipelineTestReport report)
    {
        _lastReport = report;
        try
        {
            _lastReportJson = LitJson.JsonMapper.ToJson(report);
        }
        catch (Exception ex)
        {
            _lastReportJson = JsonUtility.ToJson(report, true);
            Debug.LogWarning($"[FragmentPipelineTest] LitJson failed, used JsonUtility: {ex.Message}");
        }

        Debug.Log(BuildConsoleSummary(report));
        if (logFullJson)
            Debug.Log("[FragmentPipelineTest] JSON:\n" + _lastReportJson);

        if (copyReportJsonToClipboard && !string.IsNullOrEmpty(_lastReportJson))
        {
            GUIUtility.systemCopyBuffer = _lastReportJson;
            Debug.Log("[FragmentPipelineTest] Report JSON copied to clipboard.");
        }
    }

    private static string BuildConsoleSummary(RemiFragmentPipelineTestReport report)
    {
        var sb = new StringBuilder(512);
        sb.Append("[FragmentPipelineTest] ").Append(report.runId);
        sb.Append(" · ").Append(report.fixtureId);
        sb.Append(" · ").Append(report.success ? "SUCCESS" : "FAIL");
        sb.Append(" · ").Append(report.durationMs.ToString("0")).Append("ms");
        sb.Append(" · ").Append(report.status).Append('\n');
        sb.Append("  archiveWritten=").Append(report.archiveWrittenCount);
        sb.Append(" days=").Append(string.Join(",", report.storyDays));
        sb.Append(" memory=").Append(report.fragmentMemory.Count);
        sb.Append(" selected=").Append(report.selection.selected.Count);
        sb.Append(" bond=").Append(report.bond.source).Append('\n');
        if (!string.IsNullOrWhiteSpace(report.bond.line))
            sb.Append("  bondLine: ").Append(Truncate(report.bond.line, 160)).Append('\n');
        if (!string.IsNullOrWhiteSpace(report.bond.skipReason))
            sb.Append("  skipReason: ").Append(report.bond.skipReason).Append('\n');
        foreach (RemiFragmentPipelineTestLlmCall call in report.llmCalls)
        {
            sb.Append("  LLM ").Append(call.stage).Append(" d").Append(call.storyDay);
            sb.Append(call.ok ? " ok" : " FAIL");
            sb.Append(" ").Append(call.ms.ToString("0")).Append("ms");
            if (!string.IsNullOrWhiteSpace(call.error))
                sb.Append(" · ").Append(call.error);
            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static RemiFragmentPipelineTestImpressionSnap ToImpressionSnap(
        RemiFragmentImpression impression)
    {
        return new RemiFragmentPipelineTestImpressionSnap
        {
            id = impression.id,
            storyDay = impression.storyDay,
            summary = Truncate(impression.summary, 100),
            tags = impression.meaningTags != null ? string.Join(",", impression.meaningTags) : "",
            weight = impression.weight,
            eligibleForBond = RemiDemoEndingBondSelection.IsEligibleForBond(impression),
        };
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text ?? "";
        return text.Substring(0, max) + "…";
    }
}
