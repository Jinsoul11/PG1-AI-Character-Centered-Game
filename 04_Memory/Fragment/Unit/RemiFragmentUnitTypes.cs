using System;
using System.Collections.Generic;

/// <summary>
/// Final Weight 的分项（回忆概率，非重要性）。
/// semantic = Analyzer LLM Intrinsic；其余为系统 Recall Modifier。
/// </summary>
[Serializable]
public class RemiFragmentWeightBreakdown
{
    /// <summary>Intrinsic：语义上「值得被想起」的强度（Analyzer LLM）。</summary>
    public float semantic;
    public float repetition;
    public float novelty;
    public float crossDay;
    public float eventAffinity;
    public float endingProximity;
}

/// <summary>
/// Fragment Unit：标准化记忆单元（系统层数据库对象）。
/// Pipeline：Curator 候选 → Unit 固化 → Analyzer（Meaning + Recall Weight）→ Fragment Memory。
/// Unit 阶段尚无 Final Weight；weight 由 Analyzer 填写。
/// </summary>
[Serializable]
public class RemiFragmentUnit
{
    public string id = "";
    public string summary = "";
    public int storyDay;
    /// <summary>默认 player（策展主证据来自玩家句）。</summary>
    public string speaker = "player";
    public List<string> evidence = new List<string>();
    /// <summary>Curator 初步标签；最终 Meaning 由 Analyzer 写出。</summary>
    public List<string> candidateTags = new List<string>();
    /// <summary>Analyzer 最终 Meaning 标签（Identity/Moment/...）。</summary>
    public List<string> meaningTags = new List<string>();
    /// <summary>Analyzer 给出的相处气质（内部；不单独成 Ending 页）。</summary>
    public string atmosphere = "";
    /// <summary>Analyzer 是否已完成语义分析（Meaning）；Weight 见 weightReady。</summary>
    public bool meaningReady;
    /// <summary>内部圣物候选（通常取 evidence[0]）；默认不可引用。</summary>
    public string quoteCandidate = "";
    public bool quoteCiteEligible;

    /// <summary>Curator 策展理由（内部；非 weightReason）。</summary>
    public string curatorReason = "";
    /// <summary>Curator 自报自信（元数据；不是 Final Weight，不参与系统筛选）。</summary>
    public float curatorConfidence;

    /// <summary>Analyzer 是否已写出 Final Weight（Recall Probability）。</summary>
    public bool weightReady;
    /// <summary>Final Weight：若 Remi 回忆这段旅程，最先想起它的相对概率。</summary>
    public float weight;
    /// <summary>人话解释：为何容易被想起（Debug / 策划）。</summary>
    public string weightReason = "";
    public RemiFragmentWeightBreakdown weightBreakdown = new RemiFragmentWeightBreakdown();
    /// <summary>Analyzer LLM 的 Intrinsic（memoryStrength）；可与 breakdown.semantic 对齐。</summary>
    public float intrinsicStrength;

    public long createdUnixMs;
    public long weightComputedUnixMs;
    /// <summary>来源策展日结果标记，便于追溯。</summary>
    public int sourceCuratorStoryDay;
}

[Serializable]
public class RemiFragmentUnitStoreData
{
    public List<RemiFragmentUnit> units = new List<RemiFragmentUnit>();
    public int nextSerial = 1;
}
