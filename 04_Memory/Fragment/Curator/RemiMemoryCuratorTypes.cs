using System;
using System.Collections.Generic;

/// <summary>
/// Memory Curator 输出的「记忆候选」（非最终 Fragment）。
/// 只回答：今天哪些交流值得成为未来的回忆？记住什么？
/// confidence 为 LLM 自报元数据，系统入库时不设置信门槛（仅受条数上限约束）。
/// 最终分类与 Recall Weight 由 Fragment Analyzer 负责；固化由 Fragment Unit 负责。
/// </summary>
[Serializable]
public class RemiMemoryCuratorCandidate
{
    public string summary = "";
    public string reason = "";
    /// <summary>LLM 自报策展自信（元数据；非 Final Weight）。</summary>
    public float confidence;
    /// <summary>内部证据句（可来自 Archive）；不进玩家可见 Ending。</summary>
    public List<string> evidence = new List<string>();
    /// <summary>初步标签键（Identity/Moment/...）；非最终 Meaning。</summary>
    public List<string> candidateTags = new List<string>();
}

/// <summary>某一叙事日的 Curator 批处理结果（供后续 Unit / Analyzer 消费）。</summary>
[Serializable]
public class RemiMemoryCuratorDayResult
{
    public int storyDay;
    public bool success;
    public string error = "";
    public string rawResponse = "";
    public long curatedUnixMs;
    public int inputCandidateCount;
    public List<RemiMemoryCuratorCandidate> candidates = new List<RemiMemoryCuratorCandidate>();
}

[Serializable]
public class RemiMemoryCuratorStoreData
{
    public List<RemiMemoryCuratorDayResult> days = new List<RemiMemoryCuratorDayResult>();
}
