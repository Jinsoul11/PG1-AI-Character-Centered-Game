using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using LitJson;
using UnityEngine;

/// <summary>
/// Fragment Analyzer（Pipeline 第五段）：解释记忆 + 合成 Recall Weight。
/// LLM：Meaning / Intrinsic；系统：Recall Modifier。不与 Curator 合并。
/// </summary>
[DisallowMultipleComponent]
public class RemiFragmentAnalyzer : MonoBehaviour
{
    public static RemiFragmentAnalyzer Instance { get; private set; }

    [SerializeField] private DeepSeekDialogueManager dialogueManager;
    [SerializeField] private float temperature = 0.3f;
    [SerializeField] private bool reanalyzeEvenIfReady;

    private bool _running;
    public bool IsRunning => _running;

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiFragmentAnalyzer));
        go.AddComponent<RemiFragmentAnalyzer>();
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
        RemiFragmentUnitStore.EnsureExists();
        if (dialogueManager == null)
            dialogueManager = DeepSeekDialogueManager.Instance != null
                ? DeepSeekDialogueManager.Instance
                : FindObjectOfType<DeepSeekDialogueManager>();
    }

    public IEnumerator CoAnalyzeStoryDay(
        int storyDay,
        System.Action<RemiFragmentAnalyzerDayResult> onDone = null,
        bool forceReanalyze = false)
    {
        if (_running)
        {
            Debug.LogWarning("[RemiFragmentAnalyzer] 已有分析任务在跑，跳过。");
            yield break;
        }

        _running = true;
        RemiFragmentAnalyzerDayResult result = null;
        try
        {
            yield return CoAnalyzeStoryDayInternal(storyDay, forceReanalyze, r => result = r);
        }
        finally
        {
            _running = false;
        }

        onDone?.Invoke(result);
    }

    private IEnumerator CoAnalyzeStoryDayInternal(
        int storyDay,
        bool forceReanalyze,
        System.Action<RemiFragmentAnalyzerDayResult> onDone)
    {
        var result = new RemiFragmentAnalyzerDayResult
        {
            storyDay = storyDay,
            analyzedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        RemiFragmentUnitStore.EnsureExists();
        List<RemiFragmentUnit> dayUnits = RemiFragmentUnitStore.Instance != null
            ? RemiFragmentUnitStore.Instance.GetUnitsForStoryDay(storyDay)
            : new List<RemiFragmentUnit>();
        result.unitCount = dayUnits.Count;

        var pending = new List<RemiFragmentUnit>();
        bool force = forceReanalyze || reanalyzeEvenIfReady;
        foreach (RemiFragmentUnit u in dayUnits)
        {
            if (u == null)
                continue;
            if (!force && u.meaningReady && u.weightReady)
                continue;
            pending.Add(u);
        }

        if (pending.Count == 0)
        {
            result.success = true;
            result.analyzedCount = 0;
            // 仍尝试晋升已分析 Unit 进 Memory。
            RemiFragmentMemoryPromoter.PromoteStoryDay(storyDay);
            Debug.Log($"[RemiFragmentAnalyzer] Day {storyDay}: 无待分析 Unit。");
            onDone?.Invoke(result);
            yield break;
        }

        if (dialogueManager == null)
            dialogueManager = DeepSeekDialogueManager.Instance != null
                ? DeepSeekDialogueManager.Instance
                : FindObjectOfType<DeepSeekDialogueManager>();

        if (dialogueManager == null)
        {
            result.success = false;
            result.error = "DeepSeekDialogueManager 未找到";
            onDone?.Invoke(result);
            yield break;
        }

        string systemPrompt = BuildSystemPrompt();
        string userPrompt = BuildUserPrompt(storyDay, pending);

        string raw = null;
        string error = null;
        yield return dialogueManager.CoCompleteRaw(
            systemPrompt,
            userPrompt,
            text => raw = text,
            err => error = err,
            temperature);

        result.rawResponse = raw ?? "";
        if (!string.IsNullOrEmpty(error))
        {
            result.success = false;
            result.error = error;
            Debug.LogWarning($"[RemiFragmentAnalyzer] Day {storyDay} LLM 失败: {error}");
            onDone?.Invoke(result);
            yield break;
        }

        if (!TryParseLlmItems(raw, out List<RemiFragmentAnalyzerLlmItem> items, out string parseError))
        {
            result.success = false;
            result.error = parseError ?? "JSON 解析失败";
            Debug.LogWarning($"[RemiFragmentAnalyzer] Day {storyDay} 解析失败: {result.error}\n{raw}");
            onDone?.Invoke(result);
            yield break;
        }

        var itemById = new Dictionary<string, RemiFragmentAnalyzerLlmItem>();
        foreach (RemiFragmentAnalyzerLlmItem item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.unitId))
                continue;
            itemById[item.unitId.Trim()] = item;
        }

        RemiDialogueArchive.EnsureExists();
        IReadOnlyList<RemiDialogueArchiveEntry> archive = RemiDialogueArchive.Instance != null
            ? RemiDialogueArchive.Instance.GetEntriesOrdered()
            : Array.Empty<RemiDialogueArchiveEntry>();

        RemiSharedExperienceMemory.EnsureExists();
        IReadOnlyList<RemiSharedExperienceEntry> experiences =
            RemiSharedExperienceMemory.Instance != null
                ? RemiSharedExperienceMemory.Instance.GetRecordedEntriesOrdered()
                : Array.Empty<RemiSharedExperienceEntry>();

        IReadOnlyList<RemiFragmentUnit> allUnits = RemiFragmentUnitStore.Instance.GetUnitsOrdered();

        int analyzed = 0;
        foreach (RemiFragmentUnit unit in pending)
        {
            if (!itemById.TryGetValue(unit.id, out RemiFragmentAnalyzerLlmItem llm) || llm == null)
            {
                // 无 LLM 条：用 candidateTags 兜底 Meaning，semantic 用 curatorConfidence 弱先验。
                ApplyAnalysis(unit, null, allUnits, archive, experiences);
            }
            else
            {
                ApplyAnalysis(unit, llm, allUnits, archive, experiences);
            }

            RemiFragmentUnitStore.Instance.TryUpdateUnit(unit);
            analyzed++;
        }

        result.analyzedCount = analyzed;
        result.success = true;
        RemiFragmentMemoryPromoter.PromoteStoryDay(storyDay);
        Debug.Log(
            $"[RemiFragmentAnalyzer] Day {storyDay}: analyzed={analyzed}/{pending.Count}");
        onDone?.Invoke(result);
    }

    private static void ApplyAnalysis(
        RemiFragmentUnit unit,
        RemiFragmentAnalyzerLlmItem llm,
        IReadOnlyList<RemiFragmentUnit> allUnits,
        IReadOnlyList<RemiDialogueArchiveEntry> archive,
        IReadOnlyList<RemiSharedExperienceEntry> experiences)
    {
        unit.meaningTags.Clear();
        string semanticReason = "";
        float intrinsic = 0f;

        if (llm != null)
        {
            intrinsic = Mathf.Clamp01(llm.memoryStrength);
            semanticReason = llm.semanticReason ?? "";
            unit.atmosphere = llm.atmosphere ?? "";
            if (llm.meaningTags != null)
            {
                foreach (string tag in llm.meaningTags)
                {
                    if (!RemiChatFragmentTagRules.TryParse(tag, out RemiChatFragmentTag parsed))
                        continue;
                    string key = RemiChatFragmentTagRules.ToKey(parsed);
                    if (!unit.meaningTags.Contains(key))
                        unit.meaningTags.Add(key);
                }
            }
        }

        if (unit.meaningTags.Count == 0 && unit.candidateTags != null)
        {
            foreach (string tag in unit.candidateTags)
            {
                if (!RemiChatFragmentTagRules.TryParse(tag, out RemiChatFragmentTag parsed))
                    continue;
                string key = RemiChatFragmentTagRules.ToKey(parsed);
                if (!unit.meaningTags.Contains(key))
                    unit.meaningTags.Add(key);
            }
        }

        if (intrinsic <= 0f)
            intrinsic = Mathf.Clamp01(unit.curatorConfidence > 0f ? unit.curatorConfidence : 0.5f);

        if (string.IsNullOrWhiteSpace(semanticReason))
            semanticReason = string.IsNullOrWhiteSpace(unit.curatorReason)
                ? "语义强度来自策展先验"
                : unit.curatorReason;

        RemiFragmentWeightBreakdown modifiers = RemiFragmentRecallModifier.ComputeModifiers(
            unit,
            allUnits,
            archive,
            experiences,
            out string modifierSnippet);

        modifiers.semantic = intrinsic;
        float finalWeight = RemiFragmentRecallModifier.CombineFinal(intrinsic, modifiers);

        unit.intrinsicStrength = intrinsic;
        unit.weightBreakdown = modifiers;
        unit.weight = finalWeight;
        unit.weightReason = RemiFragmentRecallModifier.BuildWeightReason(
            semanticReason,
            modifierSnippet,
            finalWeight);
        unit.meaningReady = true;
        unit.weightReady = true;
        unit.weightComputedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static string BuildSystemPrompt()
    {
        return
            "你是关系向游戏的 Fragment Analyzer。\n" +
            "任务：解释已经固化的记忆单元——从哪些维度描述了这个人与这段交流；并给出 Intrinsic 回忆强度。\n" +
            //"你不做策展（不决定记不记得住），不写给玩家看的文学旁白。\n" +
            "原则：\n" +
            "- meaningTags 只能从 Identity, Moment, Resonance, Relation, Atmosphere 中选。\n" +
            "- memoryStrength（0～1）表示：若将来回忆这段旅程，这段记忆本身有多容易被想起（Intrinsic）。\n" +
            "- semanticReason 一句话说明为何有这样的强度。\n" +
            "- atmosphere 可选：轻松/认真/幼稚/暧昧 等短词。\n" +
            "- 只输出一个 JSON 对象，不要 Markdown。\n" +
            "格式：{\"items\":[{\"unitId\":\"fu_d1_001\",\"memoryStrength\":0.0," +
            "\"semanticReason\":\"...\",\"meaningTags\":[\"Identity\"],\"atmosphere\":\"认真\"}]}";
    }

    private static string BuildUserPrompt(int storyDay, List<RemiFragmentUnit> units)
    {
        var sb = new StringBuilder(512);
        sb.Append("storyDay=").Append(storyDay).Append('\n');
        sb.Append("units:\n");
        foreach (RemiFragmentUnit u in units)
        {
            if (u == null)
                continue;
            sb.Append("- id=").Append(u.id).Append('\n');
            sb.Append("  summary=").Append(u.summary).Append('\n');
            sb.Append("  candidateTags=").Append(string.Join(",", u.candidateTags ?? new List<string>())).Append('\n');
            sb.Append("  curatorReason=").Append(u.curatorReason).Append('\n');
            if (u.evidence != null && u.evidence.Count > 0)
                sb.Append("  evidence0=").Append(u.evidence[0]).Append('\n');
        }

        sb.Append("\n请分析并只返回 JSON。");
        return sb.ToString();
    }

    public static bool TryParseLlmItems(
        string raw,
        out List<RemiFragmentAnalyzerLlmItem> items,
        out string error)
    {
        items = new List<RemiFragmentAnalyzerLlmItem>();
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "空响应";
            return false;
        }

        string json = ExtractJsonObject(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "未找到 JSON 对象";
            return false;
        }

        try
        {
            JsonData root = JsonMapper.ToObject(json);
            if (root == null || !root.IsObject || !root.ContainsKey("items") || root["items"] == null)
                return true;

            JsonData arr = root["items"];
            if (!arr.IsArray)
            {
                error = "items 不是数组";
                return false;
            }

            for (int i = 0; i < arr.Count; i++)
            {
                JsonData obj = arr[i];
                if (obj == null || !obj.IsObject)
                    continue;

                var item = new RemiFragmentAnalyzerLlmItem
                {
                    unitId = ReadString(obj, "unitId"),
                    memoryStrength = ReadFloat(obj, "memoryStrength"),
                    semanticReason = ReadString(obj, "semanticReason"),
                    atmosphere = ReadString(obj, "atmosphere"),
                };
                ReadStringArray(obj, "meaningTags", item.meaningTags);
                if (!string.IsNullOrWhiteSpace(item.unitId))
                    items.Add(item);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string ReadString(JsonData obj, string key)
    {
        if (obj == null || !obj.ContainsKey(key) || obj[key] == null)
            return "";
        return obj[key].ToString();
    }

    private static float ReadFloat(JsonData obj, string key)
    {
        string s = ReadString(obj, key);
        return float.TryParse(s, out float v) ? Mathf.Clamp01(v) : 0f;
    }

    private static void ReadStringArray(JsonData obj, string key, List<string> target)
    {
        if (target == null || obj == null || !obj.ContainsKey(key) || obj[key] == null)
            return;
        JsonData arr = obj[key];
        if (!arr.IsArray)
            return;
        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i] == null)
                continue;
            string s = arr[i].ToString();
            if (!string.IsNullOrWhiteSpace(s))
                target.Add(s.Trim());
        }
    }

    private static string ExtractJsonObject(string raw)
    {
        string text = raw.Trim();
        Match fence = Regex.Match(text, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
        if (fence.Success)
            text = fence.Groups[1].Value.Trim();

        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;
        return text.Substring(start, end - start + 1);
    }
}
