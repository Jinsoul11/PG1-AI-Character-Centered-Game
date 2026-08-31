using System;
using System.Collections.Generic;

/// <summary>
/// 脊柱 / Progress 句的视觉强调（TMP &lt;b&gt;）。闲聊问候不强调；导演锚点与 LLM emphasis 字段合并。
/// </summary>
public readonly struct RemiDialogueEmphasisSpec
{
    public bool WholeLine { get; }
    public IReadOnlyList<string> Anchors { get; }

    public RemiDialogueEmphasisSpec(bool wholeLine, IReadOnlyList<string> anchors)
    {
        WholeLine = wholeLine;
        Anchors = anchors ?? Array.Empty<string>();
    }

    public static RemiDialogueEmphasisSpec None => new RemiDialogueEmphasisSpec(false, null);

    public static RemiDialogueEmphasisSpec Whole => new RemiDialogueEmphasisSpec(true, null);

    public static RemiDialogueEmphasisSpec WithAnchors(params string[] anchors) =>
        new RemiDialogueEmphasisSpec(false, anchors);

    public bool IsEmpty => !WholeLine && (Anchors == null || Anchors.Count == 0);
}

public static class RemiDialogueEmphasis
{
    public const string WholeLineToken = "*";

    public static string Apply(string plain, RemiDialogueEmphasisSpec spec)
    {
        if (string.IsNullOrWhiteSpace(plain) || spec.IsEmpty)
            return plain ?? string.Empty;

        plain = StripRichText(plain.Trim());
        if (spec.WholeLine)
            return $"<b>{plain}</b>";

        if (spec.Anchors == null || spec.Anchors.Count == 0)
            return plain;

        var ordered = new List<string>();
        foreach (string anchor in spec.Anchors)
        {
            if (string.IsNullOrWhiteSpace(anchor))
                continue;
            string trimmed = anchor.Trim();
            if (trimmed.Length == 0 || ordered.Contains(trimmed))
                continue;
            ordered.Add(trimmed);
        }

        ordered.Sort((a, b) => b.Length.CompareTo(a.Length));

        string result = plain;
        foreach (string anchor in ordered)
            result = BoldAllOccurrences(result, anchor);

        return result;
    }

    public static RemiDialogueEmphasisSpec ParseEmphasisField(string emphasisField)
    {
        if (string.IsNullOrWhiteSpace(emphasisField))
            return RemiDialogueEmphasisSpec.None;

        string trimmed = emphasisField.Trim();
        if (trimmed == WholeLineToken)
            return RemiDialogueEmphasisSpec.Whole;

        string[] parts = trimmed.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return RemiDialogueEmphasisSpec.None;

        var anchors = new List<string>();
        foreach (string part in parts)
        {
            string a = part.Trim();
            if (a.Length > 0)
                anchors.Add(a);
        }

        return anchors.Count == 0
            ? RemiDialogueEmphasisSpec.None
            : new RemiDialogueEmphasisSpec(false, anchors);
    }

    public static RemiDialogueEmphasisSpec Merge(RemiDialogueEmphasisSpec primary, RemiDialogueEmphasisSpec fallback)
    {
        if (!primary.IsEmpty)
            return primary;
        return fallback;
    }

    /// <summary>CharacterTriggered：合并 JSON emphasis、导演锚点；PlayerChat 仅返回原文。</summary>
    public static string FormatSpeechForTurn(
        string speech,
        RemiPromptTurnKind turnKind,
        string payloadEmphasisField,
        RemiDialogueEmphasisSpec directorSpec)
    {
        if (string.IsNullOrWhiteSpace(speech))
            return speech ?? string.Empty;

        if (turnKind != RemiPromptTurnKind.CharacterTriggered)
            return StripRichText(speech.Trim());

        RemiDialogueEmphasisSpec payloadSpec = ParseEmphasisField(payloadEmphasisField);
        RemiDialogueEmphasisSpec spec = Merge(payloadSpec, directorSpec);
        return Apply(speech, spec);
    }

    public static string StripRichText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        return text
            .Replace("<b>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("</b>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("<i>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("</i>", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string BoldAllOccurrences(string text, string anchor)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(anchor))
            return text;

        int searchFrom = 0;
        while (searchFrom < text.Length)
        {
            int idx = text.IndexOf(anchor, searchFrom, StringComparison.Ordinal);
            if (idx < 0)
                break;

            text = text.Insert(idx + anchor.Length, "</b>").Insert(idx, "<b>");
            searchFrom = idx + 3 + anchor.Length + 4;
        }

        return text;
    }
}
