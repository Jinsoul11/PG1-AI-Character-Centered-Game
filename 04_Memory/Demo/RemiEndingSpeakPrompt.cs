using System.Collections.Generic;
using System.Text;

/// <summary>
/// Ending 呈现层 Prompt：身份 + 记忆（表达指引写在同一段 director_context 里）。
/// Bond = 根据精选 Fragment 创作对玩家的印象画像（鼓励发挥）；
/// Recap = 根据单条共同经历回忆（偏事实）。
/// 「宁缺毋编」是设计师在 Selection / 是否出页上的取舍，只写在调用方注释里，不进本类发给 AI 的文案。
/// </summary>
public static class RemiEndingSpeakPrompt
{
    public const string IdentityBond =
        "[身份]\n你是女大学生Remi。你和玩家在即将分别的时候，回想起了一些珍贵的回忆。";

    public const string IdentityRecap =
        "[身份]\n你是 Remi。用第一人称，回想你们一起做过的这件事。";

    /// <summary>Bond：initiatorContext = 身份 + 入选记忆摘要。</summary>
    public static string BuildBondContext(IReadOnlyList<RemiFragmentImpression> selected)
    {
        var sb = new StringBuilder(256);
        sb.Append(IdentityBond).Append("\n\n");
        sb.Append("[记忆]\n");
        sb.Append("这些是你最容易想起的、关于玩家的片段：\n");

        int added = 0;
        if (selected != null)
        {
            foreach (RemiFragmentImpression imp in selected)
            {
                string line = RemiChatFragmentQuotePolicy.ResolvePlayerVisibleLine(imp);
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                line = RemiFragmentSummarySanitize.ReplaceAmbiguousOtherParty(line.Trim());
                sb.Append("- ").Append(line).Append('\n');
                added++;
            }
        }

        if (added == 0)
            sb.Append("- （无）\n");

        sb.Append('\n');
        sb.Append("[表达]\n");
        sb.Append("根据上面的记忆，写一段你对玩家的印象画像。\n");
        sb.Append("可以综合、提炼、用你的语气组织；尽量包含所有的记忆内容。\n");

        return sb.ToString().TrimEnd();
    }

    /// <summary>共同经历回顾：initiatorContext = 身份 + 本条事实 + 表达指引。</summary>
    public static string BuildRecapContext(RemiSharedExperienceEntry entry)
    {
        var sb = new StringBuilder(256);
        sb.Append(IdentityRecap).Append("\n\n");
        sb.Append("[记忆]\n");
        if (entry == null)
        {
            sb.Append("我们曾一起：（无）\n");
        }
        else
        {
            string frame = string.IsNullOrWhiteSpace(entry.frame) ? "一段共同经历" : entry.frame.Trim();
            sb.Append("我们曾一起：").Append(frame).Append('\n');
            if (entry.storyDay > 0)
                sb.Append("时间：第 ").Append(entry.storyDay).Append(" 天\n");
            sb.Append("只谈这一段。\n");
        }

        sb.Append('\n');
        sb.Append("[表达]\n");
        sb.Append("根据这段共同经历，说 1～2 句回忆。\n");
        sb.Append("像本人在翻到这一页时随口想起，不要扩写成别的事，也不要提问。");

        return sb.ToString().TrimEnd();
    }
}
