using System;
using System.Collections.Generic;

/// <summary>
/// 闲聊记忆单元（余韵，非正史）。
/// 可含内部 quote 字段，但 Ending 默认只读 summary（见 RemiChatFragmentQuotePolicy）。
/// </summary>
[Serializable]
public class RemiChatFragmentEntry
{
    public string id;
    public string summary;
    /// <summary>逗号分隔标签键（Identity,Moment,...）；JsonUtility 友好。</summary>
    public string tagsCsv = "";
    /// <summary>Atmosphere 语气键（轻松/认真等）；只参与排序与润色，不单独成页。</summary>
    public string atmosphere = "";
    public int channel;
    public int storyDay;
    public int phase;
    public int hitCount;
    public float weight;
    /// <summary>内部圣物原文；默认空。永不作为 Ending 默认填词。</summary>
    public string quote = "";
    /// <summary>须经闸门后才可为 true；当前采集管线不置位。</summary>
    public bool quoteCiteEligible;

    public RemiChatFragmentEntry() { }

    public RemiChatFragmentEntry(
        string id,
        string summary,
        IList<RemiChatFragmentTag> tags,
        string atmosphere,
        RemiInteractionChannel channel,
        RemiWorldTime worldTime,
        int hitCount,
        float weight)
    {
        this.id = id ?? "";
        this.summary = summary ?? "";
        SetTags(tags);
        this.atmosphere = atmosphere ?? "";
        this.channel = (int)channel;
        storyDay = worldTime.storyDay;
        phase = (int)worldTime.phase;
        this.hitCount = hitCount;
        this.weight = weight;
        quote = "";
        quoteCiteEligible = false;
    }

    public void SetTags(IList<RemiChatFragmentTag> tags)
    {
        if (tags == null || tags.Count == 0)
        {
            tagsCsv = "";
            return;
        }

        var parts = new List<string>(tags.Count);
        foreach (RemiChatFragmentTag tag in tags)
        {
            string key = RemiChatFragmentTagRules.ToKey(tag);
            if (!parts.Contains(key))
                parts.Add(key);
        }

        tagsCsv = string.Join(",", parts);
    }

    public List<RemiChatFragmentTag> GetTags()
    {
        var result = new List<RemiChatFragmentTag>();
        if (string.IsNullOrWhiteSpace(tagsCsv))
            return result;

        foreach (string part in tagsCsv.Split(','))
        {
            if (RemiChatFragmentTagRules.TryParse(part, out RemiChatFragmentTag tag) && !result.Contains(tag))
                result.Add(tag);
        }

        return result;
    }

    public bool HasTag(RemiChatFragmentTag tag)
    {
        foreach (RemiChatFragmentTag existing in GetTags())
        {
            if (existing == tag)
                return true;
        }

        return false;
    }

    public bool HasAnyBondPage1Tag()
    {
        foreach (RemiChatFragmentTag tag in GetTags())
        {
            if (RemiChatFragmentTagRules.IsBondPage1Source(tag))
                return true;
        }

        return false;
    }

    public bool HasResonanceTag() => HasTag(RemiChatFragmentTag.Resonance);

    /// <summary>旧存档无 tags 时，从 Catalog 回填。</summary>
    public void EnsureTagsMigrated()
    {
        if (!string.IsNullOrWhiteSpace(tagsCsv))
            return;
        if (!RemiChatFragmentCatalog.TryGetPattern(id, out RemiChatFragmentCatalog.PatternDef pattern))
            return;
        SetTags(pattern.Tags);
        if (string.IsNullOrWhiteSpace(atmosphere))
            atmosphere = pattern.Atmosphere ?? "";
    }
}

[Serializable]
public class RemiChatFragmentStore
{
    public List<RemiChatFragmentEntry> entries = new List<RemiChatFragmentEntry>();
}
