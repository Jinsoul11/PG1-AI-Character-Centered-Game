using System;
using System.Collections.Generic;

/// <summary>
/// Fragment Memory 印象条目：过程记忆终库（人格印象，非聊天原文、非正史）。
/// Pipeline：Analyzer 完成后晋升至此；Ending 只读本库。
/// </summary>
[Serializable]
public class RemiFragmentImpression
{
    public string id = "";
    public string summary = "";
    public int storyDay;
    /// <summary>Meaning 标签键（Identity/Moment/...）。</summary>
    public List<string> meaningTags = new List<string>();
    public string atmosphere = "";
    /// <summary>Final Recall Probability。</summary>
    public float weight;
    public string weightReason = "";
    public RemiFragmentWeightBreakdown weightBreakdown = new RemiFragmentWeightBreakdown();
    public float intrinsicStrength;
    /// <summary>内部圣物；Ending 默认不露出。</summary>
    public string quote = "";
    public bool quoteCiteEligible;
    /// <summary>Resonance 润色用短提示（非类名）。</summary>
    public string resonanceHint = "";
    public long promotedUnixMs;
    public string sourceUnitId = "";
    /// <summary>日结固化的话题别名（Recall 主路径；8–15 条）。</summary>
    public List<string> topicAliases = new List<string>();
    /// <summary>是否具备有效 topicAliases，可参与 ACTIVE_MEMORY 召回。</summary>
    public bool recallEligible;

    public bool HasMeaningTag(RemiChatFragmentTag tag)
    {
        string key = RemiChatFragmentTagRules.ToKey(tag);
        if (meaningTags == null)
            return false;
        foreach (string t in meaningTags)
        {
            if (string.Equals(t, key, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public bool HasAnyBondPage1Tag() =>
        HasMeaningTag(RemiChatFragmentTag.Identity) || HasMeaningTag(RemiChatFragmentTag.Moment);

    public bool HasResonanceTag() => HasMeaningTag(RemiChatFragmentTag.Resonance);
}

[Serializable]
public class RemiFragmentMemoryStoreData
{
    public List<RemiFragmentImpression> impressions = new List<RemiFragmentImpression>();
}
