using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Memory Formation 日结：
/// Filter → Curator → Unit → Analyzer（Meaning + Recall Weight）→ Fragment Memory。
/// 已有对应日终库 / 完整中间库时跳过，避免重载重复跑 LLM。
/// </summary>
[DisallowMultipleComponent]
public class RemiMemoryDaySettlement : MonoBehaviour
{
    public static RemiMemoryDaySettlement Instance { get; private set; }

    [Tooltip("若该日已有完整日结结果，则跳过（避免重复耗 LLM）。")]
    [SerializeField] private bool skipIfFullySettled = true;

    private readonly Queue<int> _pendingClosedDays = new Queue<int>();
    private bool _drainRunning;

    public bool IsBusy => _drainRunning || _pendingClosedDays.Count > 0;

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiMemoryDaySettlement));
        go.AddComponent<RemiMemoryDaySettlement>();
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
        RemiMemoryCurator.EnsureExists();
        RemiMemoryCuratorStore.EnsureExists();
        RemiFragmentUnitStore.EnsureExists();
        RemiFragmentAnalyzer.EnsureExists();
        RemiFragmentMemory.EnsureExists();
    }

    public static void NotifyStoryDayClosed(int closedStoryDay)
    {
        if (closedStoryDay <= 0)
            return;
        EnsureExists();
        Instance?.EnqueueClosedDay(closedStoryDay);
    }

    public static void NotifyFlushCurrentStoryDay()
    {
        EnsureExists();
        int day = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.WorldTime.storyDay
            : 0;
        if (day <= 0)
            return;
        Instance?.EnqueueClosedDay(day);
    }

    /// <summary>等待队列日结跑完（日起点存档前调用，避免快照到空库）。</summary>
    public static IEnumerator CoWaitUntilIdle(float timeoutSeconds = 180f)
    {
        EnsureExists();
        if (Instance == null)
            yield break;

        float wait = 0f;
        while (Instance.IsBusy && wait < timeoutSeconds)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        if (Instance.IsBusy)
            Debug.LogWarning("[RemiMemoryDaySettlement] 等待日结空闲超时。");
    }

    public static IEnumerator CoFlushBeforeEnding()
    {
        EnsureExists();
        if (Instance == null)
            yield break;

        NotifyFlushCurrentStoryDay();
        yield return CoWaitUntilIdle(180f);
    }

    private void EnqueueClosedDay(int storyDay)
    {
        if (storyDay <= 0)
            return;

        EnsurePipelineStoresLoaded();

        if (skipIfFullySettled && IsDayFullySettled(storyDay))
        {
            // Unit 已分析但终库尚未晋升时只补写，不重跑 LLM。
            if (CountFragmentImpressionsForDay(storyDay) == 0)
                RemiFragmentMemoryPromoter.PromoteStoryDay(storyDay);
            Debug.Log($"[RemiMemoryDaySettlement] Day {storyDay} 已有日结结果，跳过。");
            return;
        }

        if (_pendingClosedDays.Contains(storyDay))
            return;

        _pendingClosedDays.Enqueue(storyDay);
        Debug.Log($"[RemiMemoryDaySettlement] 排队日结 Day {storyDay}（队列={_pendingClosedDays.Count}）");

        if (!_drainRunning)
            StartCoroutine(CoDrainQueue());
    }

    /// <summary>从磁盘刷新中间库 / 终库，避免 DDOL 旧内存与存档不一致。</summary>
    private static void EnsurePipelineStoresLoaded()
    {
        RemiMemoryCuratorStore.EnsureExists();
        RemiFragmentUnitStore.EnsureExists();
        RemiFragmentMemory.EnsureExists();
        RemiMemoryCuratorStore.Instance?.ReloadFromDisk();
        RemiFragmentUnitStore.Instance?.ReloadFromDisk();
        RemiFragmentMemory.Instance?.ReloadFromDisk();
    }

    private static bool IsDaySuccessfullyCurated(int storyDay)
    {
        RemiMemoryCuratorStore.EnsureExists();
        if (RemiMemoryCuratorStore.Instance == null)
            return false;
        if (!RemiMemoryCuratorStore.Instance.TryGetDay(storyDay, out RemiMemoryCuratorDayResult result))
            return false;
        return result != null && result.success;
    }

    private static int CountFragmentImpressionsForDay(int storyDay)
    {
        RemiFragmentMemory.EnsureExists();
        if (RemiFragmentMemory.Instance == null)
            return 0;
        return RemiFragmentMemory.Instance.GetImpressionsForStoryDay(storyDay).Count;
    }

    /// <summary>
    /// 该日已结算：终库已有印象，或策展成功且 Unit 均已分析
    /// （含策展成功但 0 候选 / 0 Unit 的空日）。
    /// </summary>
    public static bool IsDayFullySettled(int storyDay)
    {
        if (storyDay <= 0)
            return false;

        if (CountFragmentImpressionsForDay(storyDay) > 0)
            return true;

        if (!IsDaySuccessfullyCurated(storyDay))
            return false;

        RemiFragmentUnitStore.EnsureExists();
        if (RemiFragmentUnitStore.Instance == null)
            return false;

        List<RemiFragmentUnit> units = RemiFragmentUnitStore.Instance.GetUnitsForStoryDay(storyDay);
        if (units.Count == 0)
        {
            // 策展成功且无候选 → 合法空日，视为已结。
            if (RemiMemoryCuratorStore.Instance.TryGetDay(storyDay, out RemiMemoryCuratorDayResult day) &&
                day != null &&
                (day.candidates == null || day.candidates.Count == 0))
                return true;
            return false;
        }

        return !RemiFragmentUnitStore.Instance.HasUnanalyzedUnitsForStoryDay(storyDay);
    }

    /// <summary>调试 / 手动：强制跑完一日管线。</summary>
    public IEnumerator CoForceSettleDay(int day)
    {
        if (day <= 0)
            yield break;
        yield return CoSettleDay(day, force: true);
    }

    private IEnumerator CoDrainQueue()
    {
        _drainRunning = true;
        try
        {
            while (_pendingClosedDays.Count > 0)
            {
                int day = _pendingClosedDays.Dequeue();
                yield return CoSettleDay(day, force: false);
            }
        }
        finally
        {
            _drainRunning = false;
        }
    }

    private IEnumerator CoSettleDay(int day, bool force)
    {
        EnsurePipelineStoresLoaded();

        if (!force && skipIfFullySettled && IsDayFullySettled(day))
        {
            if (CountFragmentImpressionsForDay(day) == 0)
                RemiFragmentMemoryPromoter.PromoteStoryDay(day);
            Debug.Log($"[RemiMemoryDaySettlement] Day {day} 出队时已完整，跳过。");
            yield break;
        }

        // 1) Curator（若尚未成功；已策展则只补 Unit）
        if (!IsDaySuccessfullyCurated(day))
        {
            RemiMemoryCurator.EnsureExists();
            if (RemiMemoryCurator.Instance == null)
            {
                Debug.LogWarning("[RemiMemoryDaySettlement] Curator 缺失。");
                yield break;
            }

            float wait = 0f;
            while (RemiMemoryCurator.Instance.IsRunning && wait < 60f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            RemiMemoryCuratorDayResult curatorResult = null;
            yield return RemiMemoryCurator.Instance.CoCurateStoryDay(day, r => curatorResult = r);
            if (curatorResult == null || !curatorResult.success)
            {
                Debug.LogWarning(
                    $"[RemiMemoryDaySettlement] Day {day} Curator 失败: {curatorResult?.error}");
                yield break;
            }

            int unitCount = RemiFragmentUnitBuilder.MaterializeFromCuratorDay(curatorResult);
            Debug.Log(
                $"[RemiMemoryDaySettlement] Day {day} Curator+Unit · " +
                $"out={curatorResult.candidates?.Count ?? 0} · units={unitCount}");
        }
        else
        {
            int ensured = RemiFragmentUnitBuilder.EnsureMaterializedForStoryDay(day);
            if (ensured > 0)
                Debug.Log($"[RemiMemoryDaySettlement] Day {day} 补固化 Unit ×{ensured}");
        }

        // 空日（策展成功 0 Unit）：无需 Analyzer，直接记为完成。
        RemiFragmentUnitStore.EnsureExists();
        List<RemiFragmentUnit> dayUnits = RemiFragmentUnitStore.Instance != null
            ? RemiFragmentUnitStore.Instance.GetUnitsForStoryDay(day)
            : new List<RemiFragmentUnit>();
        if (dayUnits.Count == 0)
        {
            Debug.Log($"[RemiMemoryDaySettlement] Day {day} 无 Unit，日结完成（空日）。");
            RemiDemoDaySaveService.RefreshPipelineSnapshotInDayStartSlots();
            yield break;
        }

        // 2) Analyzer
        RemiFragmentAnalyzer.EnsureExists();
        if (RemiFragmentAnalyzer.Instance == null)
        {
            Debug.LogWarning("[RemiMemoryDaySettlement] Analyzer 缺失。");
            yield break;
        }

        float waitA = 0f;
        while (RemiFragmentAnalyzer.Instance.IsRunning && waitA < 60f)
        {
            waitA += Time.unscaledDeltaTime;
            yield return null;
        }

        RemiFragmentAnalyzerDayResult analyzeResult = null;
        yield return RemiFragmentAnalyzer.Instance.CoAnalyzeStoryDay(
            day,
            r => analyzeResult = r,
            forceReanalyze: force);

        if (analyzeResult == null || !analyzeResult.success)
        {
            Debug.LogWarning(
                $"[RemiMemoryDaySettlement] Day {day} Analyzer 失败: {analyzeResult?.error}");
            yield break;
        }

        Debug.Log(
            $"[RemiMemoryDaySettlement] Day {day} Analyzer 完成 · analyzed={analyzeResult.analyzedCount}");

        // 日起点档可能早于日结落盘；补写 pipeline JSON，避免下次读档清空终库。
        RemiDemoDaySaveService.RefreshPipelineSnapshotInDayStartSlots();
    }
}
