using UnityEngine;

/// <summary>
/// Demo Memory 调试叠层：F9 切换；查看 EndingPayload / RunTelemetry / chat_fragments。
/// 仅开发用，不进正式 UI 流程。
/// </summary>
[DisallowMultipleComponent]
public class RemiDemoMemoryDebugOverlay : MonoBehaviour
{
    public static RemiDemoMemoryDebugOverlay Instance { get; private set; }

    [SerializeField] private KeyCode toggleKey = KeyCode.F9;
    [SerializeField] private bool visibleOnStart;

    private bool _visible;
    private Vector2 _scroll;
    private string _payloadLiveJson = "";
    private string _payloadSavedJson = "";
    private string _telemetryLiveJson = "";
    private string _telemetrySavedJson = "";
    private string _fragmentsJson = "";
    private string _archiveJson = "";
    private string _filterJson = "";
    private string _curatorJson = "";
    private string _unitsJson = "";
    private string _fragmentMemoryJson = "";
    private string _bondSummary = "";
    private string _statusLine = "按 F9 切换；Refresh 重新拉取。";
    private bool _curatorBusy;

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiDemoMemoryDebugOverlay));
        go.AddComponent<RemiDemoMemoryDebugOverlay>();
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
        _visible = visibleOnStart;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            ToggleVisible();
    }

    public void ToggleVisible()
    {
        _visible = !_visible;
        if (_visible)
            RefreshAll();
    }

    private void OnGUI()
    {
        if (!_visible)
            return;

        const float width = 580f;
        const float height = 680f;
        var rect = new Rect(12f, 12f, width, height);
        GUILayout.BeginArea(rect, GUI.skin.window);
        GUILayout.Label("Remi Demo Memory Debug (F9)");
        GUILayout.Label(_statusLine);
        if (!string.IsNullOrEmpty(_bondSummary))
            GUILayout.Label(_bondSummary);

        RemiFragmentPipelineTestRunner.EnsureExists();
        RemiFragmentPipelineTestRunner runner = RemiFragmentPipelineTestRunner.Instance;
        string f10 = runner != null ? runner.SpecialFixtureLabel : "?";
        string f11 = runner != null ? runner.DepthFixtureLabel : "?";
        GUILayout.Label($"F10={f10}  ·  F11={f11}  ·  Shift+F10/F11 切换用例");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
            RefreshAll();
        if (GUILayout.Button("Save Payload", GUILayout.Width(96f)))
            BuildAndSavePayload();
        if (GUILayout.Button("Finalize Telemetry", GUILayout.Width(120f)))
            FinalizeTelemetry();
        GUI.enabled = !_curatorBusy;
        if (GUILayout.Button(_curatorBusy ? "Settling…" : "Settle Day", GUILayout.Width(96f)))
            StartSettleCurrentDay();
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUI.enabled = runner != null && !runner.IsBusy;
        if (GUILayout.Button("◀ F10", GUILayout.Width(56f)) && runner != null)
            runner.CycleSpecialFixture(-1);
        if (GUILayout.Button($"Pipeline F10 · {f10}", GUILayout.Width(220f)))
            StartPipelineTest(depthPlan: false);
        if (GUILayout.Button("F10 ▶", GUILayout.Width(56f)) && runner != null)
            runner.CycleSpecialFixture(+1);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀ F11", GUILayout.Width(56f)) && runner != null)
            runner.CycleDepthFixture(-1);
        if (GUILayout.Button($"Depth F11 · {f11}", GUILayout.Width(220f)))
            StartPipelineTest(depthPlan: true);
        if (GUILayout.Button("F11 ▶", GUILayout.Width(56f)) && runner != null)
            runner.CycleDepthFixture(+1);
        GUI.enabled = true;
        if (GUILayout.Button("Hide", GUILayout.Width(52f)))
            _visible = false;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy Live Payload", GUILayout.Width(130f)))
            CopyText(_payloadLiveJson);
        if (GUILayout.Button("Copy Telemetry", GUILayout.Width(110f)))
            CopyText(_telemetryLiveJson);
        if (GUILayout.Button("Copy Fragments", GUILayout.Width(110f)))
            CopyText(_fragmentsJson);
        if (GUILayout.Button("Copy Filter", GUILayout.Width(90f)))
            CopyText(_filterJson);
        if (GUILayout.Button("Copy Curator", GUILayout.Width(100f)))
            CopyText(_curatorJson);
        if (GUILayout.Button("Copy Units", GUILayout.Width(90f)))
            CopyText(_unitsJson);
        if (GUILayout.Button("Copy FM", GUILayout.Width(70f)))
            CopyText(_fragmentMemoryJson);
        if (GUILayout.Button("Copy Pipeline Report", GUILayout.Width(140f)))
        {
            RemiFragmentPipelineTestRunner.EnsureExists();
            string json = RemiFragmentPipelineTestRunner.Instance != null
                ? RemiFragmentPipelineTestRunner.Instance.LastReportJson
                : "";
            CopyText(string.IsNullOrEmpty(json) ? "(no pipeline report yet)" : json);
        }
        GUILayout.EndHorizontal();

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(height - 150f));
        DrawSection("EndingPayload · Live", _payloadLiveJson);
        DrawSection("EndingPayload · Saved", _payloadSavedJson);
        DrawSection("RunTelemetry · Live", _telemetryLiveJson);
        DrawSection("RunTelemetry · Saved", _telemetrySavedJson);
        DrawSection("chat_fragments · Legacy", _fragmentsJson);
        DrawSection("dialogue_archive · Store", _archiveJson);
        DrawSection("candidate_filter · CurrentDay", _filterJson);
        DrawSection("memory_curator · DayResult", _curatorJson);
        DrawSection("fragment_units · Store", _unitsJson);
        DrawSection("fragment_memory · Impressions", _fragmentMemoryJson);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static void DrawSection(string title, string body)
    {
        GUILayout.Label(title);
        GUILayout.TextArea(string.IsNullOrEmpty(body) ? "(empty)" : body, GUILayout.MinHeight(96f));
        GUILayout.Space(6f);
    }

    public void RefreshAll()
    {
        RemiDemoEndingPayload livePayload = RemiDemoEndingPayloadBuilder.Build();
        _payloadLiveJson = JsonUtility.ToJson(livePayload, true);
        _bondSummary = BuildBondSummary(livePayload);

        RemiDemoEndingPayload savedPayload = RemiDemoEndingPayloadBuilder.LoadSaved();
        _payloadSavedJson = savedPayload != null ? JsonUtility.ToJson(savedPayload, true) : "";

        RemiDemoRunTelemetry.EnsureExists();
        RemiDemoRunTelemetrySnapshot liveTelemetry = RemiDemoRunTelemetry.Instance != null
            ? RemiDemoRunTelemetry.Instance.BuildLivePreview()
            : new RemiDemoRunTelemetrySnapshot();
        _telemetryLiveJson = JsonUtility.ToJson(liveTelemetry, true);

        RemiDemoRunTelemetrySnapshot savedTelemetry = RemiDemoRunTelemetry.LoadSaved();
        _telemetrySavedJson = savedTelemetry != null ? JsonUtility.ToJson(savedTelemetry, true) : "";

        RemiChatFragmentMemory.EnsureExists();
        var store = new RemiChatFragmentStore();
        if (RemiChatFragmentMemory.Instance != null)
        {
            foreach (RemiChatFragmentEntry entry in RemiChatFragmentMemory.Instance.GetEntriesOrdered())
            {
                if (entry != null)
                    store.entries.Add(entry);
            }
        }

        _fragmentsJson = JsonUtility.ToJson(store, true);

        RemiDialogueArchive.EnsureExists();
        var archiveStore = new RemiDialogueArchiveStore();
        if (RemiDialogueArchive.Instance != null)
        {
            foreach (RemiDialogueArchiveEntry entry in RemiDialogueArchive.Instance.GetEntriesOrdered())
            {
                if (entry != null)
                    archiveStore.entries.Add(entry);
            }
        }

        _archiveJson = JsonUtility.ToJson(archiveStore, true);

        int storyDay = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.WorldTime.storyDay
            : 0;
        RemiDialogueCandidateFilter.FilterResult filter =
            RemiDialogueCandidateFilter.FilterStoryDay(storyDay);
        _filterJson = BuildFilterDebugJson(storyDay, filter);

        RemiMemoryCuratorStore.EnsureExists();
        if (RemiMemoryCuratorStore.Instance != null &&
            RemiMemoryCuratorStore.Instance.TryGetDay(storyDay, out RemiMemoryCuratorDayResult dayResult))
            _curatorJson = LitJson.JsonMapper.ToJson(dayResult);
        else
            _curatorJson = $"{{\"storyDay\":{storyDay},\"note\":\"no curator result yet\"}}";

        int curatorCount = 0;
        if (RemiMemoryCuratorStore.Instance != null &&
            RemiMemoryCuratorStore.Instance.TryGetDay(storyDay, out RemiMemoryCuratorDayResult dr) &&
            dr.candidates != null)
            curatorCount = dr.candidates.Count;

        RemiFragmentUnitStore.EnsureExists();
        var unitData = new RemiFragmentUnitStoreData();
        if (RemiFragmentUnitStore.Instance != null)
        {
            foreach (RemiFragmentUnit unit in RemiFragmentUnitStore.Instance.GetUnitsOrdered())
            {
                if (unit != null)
                    unitData.units.Add(unit);
            }
        }

        _unitsJson = LitJson.JsonMapper.ToJson(unitData);
        int unitCount = unitData.units?.Count ?? 0;

        RemiFragmentMemory.EnsureExists();
        var fmData = new RemiFragmentMemoryStoreData();
        if (RemiFragmentMemory.Instance != null)
        {
            foreach (RemiFragmentImpression impression in RemiFragmentMemory.Instance.GetImpressionsOrdered())
            {
                if (impression != null)
                    fmData.impressions.Add(impression);
            }
        }

        _fragmentMemoryJson = LitJson.JsonMapper.ToJson(fmData);
        int fmCount = fmData.impressions?.Count ?? 0;

        _statusLine =
            $"Refreshed {System.DateTime.Now:HH:mm:ss} · fragments(legacy)={store.entries.Count} · " +
            $"archive={archiveStore.entries.Count} · day{storyDay} filter={filter.KeptCount} · " +
            $"curator={curatorCount} · units={unitCount} · memory={fmCount}";
    }

    private void StartSettleCurrentDay()
    {
        if (_curatorBusy)
            return;

        RemiMemoryDaySettlement.EnsureExists();
        int storyDay = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.WorldTime.storyDay
            : 0;
        _curatorBusy = true;
        _statusLine = $"Settling day {storyDay} (Curator→Unit→Analyzer)…";
        StartCoroutine(CoSettleAndRefresh(storyDay));
    }

    private void StartPipelineTest(bool depthPlan)
    {
        RemiFragmentPipelineTestRunner.EnsureExists();
        if (RemiFragmentPipelineTestRunner.Instance == null)
        {
            _statusLine = "Pipeline Test Runner missing.";
            return;
        }

        if (RemiFragmentPipelineTestRunner.Instance.IsBusy)
        {
            _statusLine = "Pipeline Test already running…";
            return;
        }

        _statusLine = depthPlan
            ? "Depth Plan F11 running…"
            : "Special Pipeline F10 running…";
        if (depthPlan)
            RemiFragmentPipelineTestRunner.Instance.RunDepthPlanPipelineTest();
        else
            RemiFragmentPipelineTestRunner.Instance.RunSpecialPipelineTest();
        StartCoroutine(CoWaitPipelineTest());
    }

    private System.Collections.IEnumerator CoWaitPipelineTest()
    {
        while (RemiFragmentPipelineTestRunner.Instance != null &&
               RemiFragmentPipelineTestRunner.Instance.IsBusy)
            yield return null;

        RefreshAll();
        RemiFragmentPipelineTestReport report = RemiFragmentPipelineTestRunner.Instance != null
            ? RemiFragmentPipelineTestRunner.Instance.LastReport
            : null;
        if (report != null)
        {
            _statusLine =
                $"Pipeline Test {report.runId} · {report.status} · bond={report.bond?.source}";
            _bondSummary = report.selection != null && report.selection.hasBondPresentation
                ? $"Bond Test: {Shorten(report.bond?.line, 48)}"
                : "Bond Test: skipped";
        }
        else
        {
            _statusLine = "Pipeline Test finished (no report).";
        }
    }

    private System.Collections.IEnumerator CoSettleAndRefresh(int storyDay)
    {
        RemiMemoryDaySettlement.EnsureExists();
        if (RemiMemoryDaySettlement.Instance != null)
            yield return RemiMemoryDaySettlement.Instance.CoForceSettleDay(storyDay);

        _curatorBusy = false;
        RefreshAll();
        _statusLine = $"Settle day {storyDay} done @ {System.DateTime.Now:HH:mm:ss}";
    }

    private static string BuildFilterDebugJson(int storyDay, RemiDialogueCandidateFilter.FilterResult filter)
    {
        var sb = new System.Text.StringBuilder(512);
        sb.Append("{\n  \"storyDay\": ").Append(storyDay).Append(",\n");
        sb.Append("  \"kept\": ").Append(filter.KeptCount).Append(",\n");
        sb.Append("  \"rejected\": ").Append(filter.RejectedCount).Append(",\n");
        sb.Append("  \"candidates\": [\n");
        for (int i = 0; i < filter.Kept.Count; i++)
        {
            RemiDialogueCandidateFilter.Candidate c = filter.Kept[i];
            string content = c.Entry != null ? c.Entry.content : "";
            content = content.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
            if (content.Length > 80)
                content = content.Substring(0, 80) + "…";
            sb.Append("    {\"reason\":\"").Append(c.KeepReason).Append("\",\"content\":\"")
                .Append(content).Append("\"}");
            if (i < filter.Kept.Count - 1)
                sb.Append(',');
            sb.Append('\n');
        }

        sb.Append("  ],\n  \"rejects_sample\": [\n");
        int rejectShow = Mathf.Min(8, filter.Rejected.Count);
        for (int i = 0; i < rejectShow; i++)
        {
            RemiDialogueCandidateFilter.Rejection r = filter.Rejected[i];
            string content = r.Entry != null ? r.Entry.content : "";
            content = content.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
            if (content.Length > 40)
                content = content.Substring(0, 40) + "…";
            sb.Append("    {\"reason\":\"").Append(r.Reason).Append("\",\"content\":\"")
                .Append(content).Append("\"}");
            if (i < rejectShow - 1)
                sb.Append(',');
            sb.Append('\n');
        }

        sb.Append("  ]\n}");
        return sb.ToString();
    }

    private static string BuildBondSummary(RemiDemoEndingPayload payload)
    {
        if (payload?.bondSlots == null)
            return "";

        RemiDemoEndingBondSlots slots = payload.bondSlots;
        int n = slots.selectedImpressions?.Count ?? 0;
        if (!slots.hasBondPresentation || n == 0)
            return "Bond: skip (no eligible Fragment Memory)";

        RemiFragmentImpression top = slots.selectedImpressions[0];
        string topLine = RemiChatFragmentQuotePolicy.ResolvePlayerVisibleLine(top);
        return $"Bond Mode B: n={n} topW={top?.weight:0.00} | {Shorten(topLine, 40)}";
    }

    private static string Shorten(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "(none)";
        text = text.Trim();
        return text.Length <= max ? text : text.Substring(0, max) + "…";
    }

    private void BuildAndSavePayload()
    {
        RemiDemoEndingPayload payload = RemiDemoEndingPayloadBuilder.Build();
        RemiDemoEndingPayloadBuilder.Save(payload);
        RefreshAll();
        _statusLine = "Payload built + saved.";
    }

    private void FinalizeTelemetry()
    {
        RemiDemoRunTelemetry.EnsureExists();
        RemiDemoRunTelemetry.Instance?.FinalizeAndSave();
        RefreshAll();
        _statusLine = "Telemetry finalized + saved.";
    }

    private static void CopyText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        GUIUtility.systemCopyBuffer = text;
    }

#if UNITY_EDITOR
    [ContextMenu("Toggle Overlay")]
    private void Editor_Toggle() => ToggleVisible();
#endif
}
