using System;
using System.Collections.Generic;

/// <summary>Voice 按需注入的世界知识卡（Remi 视角，非百科）。</summary>
[Serializable]
public class RemiKnowledgeCard
{
    public string id;
    public string[] aliases = Array.Empty<string>();
    /// <summary>场景弱触发标签（location/activity/episode 名的小写 key）。</summary>
    public string[] sceneTags = Array.Empty<string>();
    /// <summary>进行中 episode 强挂接（如 commission）。</summary>
    public string[] episodeKinds = Array.Empty<string>();
    public string remiView = "";
    public int priority = 1;
}

public sealed class RemiActiveContextResult
{
    public readonly List<RemiKnowledgeCard> Knowledge = new List<RemiKnowledgeCard>();
    public readonly List<RemiFragmentImpression> FragmentMemories = new List<RemiFragmentImpression>();
    public readonly List<string> SelectedKnowledgeIds = new List<string>();

    public string BuildKnowledgeBlock()
    {
        if (Knowledge.Count == 0)
            return "";

        var sb = new System.Text.StringBuilder();
        sb.Append("[ACTIVE_KNOWLEDGE]\n");
        sb.Append("rule: 仅在自然相关时引用下方 remi_view；勿主动展开未问到的细节。\n");
        foreach (RemiKnowledgeCard card in Knowledge)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.remiView))
                continue;
            sb.Append("- id: ").Append(card.id).Append('\n');
            sb.Append("  remi_view: ").Append(card.remiView.Trim()).Append('\n');
        }

        return sb.ToString().TrimEnd();
    }

    public string BuildMemoryBlock()
    {
        if (FragmentMemories.Count == 0)
            return "";

        var sb = new System.Text.StringBuilder();
        sb.Append("[ACTIVE_MEMORY]\n");
        sb.Append("rule: 可能相关的过程印象；仅在自然相关时偶尔回响，勿编造未列出的经历。\n");
        foreach (RemiFragmentImpression imp in FragmentMemories)
        {
            if (imp == null || string.IsNullOrWhiteSpace(imp.summary))
                continue;
            sb.Append("- id: ").Append(imp.id).Append('\n');
            sb.Append("  summary: ").Append(imp.summary.Trim()).Append('\n');
            if (!string.IsNullOrWhiteSpace(imp.atmosphere))
                sb.Append("  atmosphere: ").Append(imp.atmosphere.Trim()).Append('\n');
        }

        return sb.ToString().TrimEnd();
    }
}
