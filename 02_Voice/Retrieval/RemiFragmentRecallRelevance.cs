using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using LitJson;
using UnityEngine;

/// <summary>
/// 多 Fragment 别名命中时，LLM 评估与当前玩家句的相关度。
/// 失败或并列时由 RemiFragmentRecallService 以 weight 保底。
/// </summary>
public static class RemiFragmentRecallRelevance
{
    private const float Temperature = 0.15f;

    public sealed class RelevanceScore
    {
        public string fragmentId;
        public float relevance;
    }

    public static IEnumerator CoScoreHits(
        string playerText,
        IReadOnlyList<RemiFragmentImpression> hits,
        Action<List<RelevanceScore>> onSuccess,
        Action<string> onError)
    {
        if (hits == null || hits.Count < 2)
        {
            onError?.Invoke("hits 不足 2 条");
            yield break;
        }

        DeepSeekDialogueManager dialogueManager = DeepSeekDialogueManager.Instance;
        if (dialogueManager == null)
            dialogueManager = UnityEngine.Object.FindObjectOfType<DeepSeekDialogueManager>();

        if (dialogueManager == null)
        {
            onError?.Invoke("DeepSeekDialogueManager 未找到");
            yield break;
        }

        string systemPrompt = BuildSystemPrompt();
        string userPrompt = BuildUserPrompt(playerText, hits);
        string raw = null;
        string error = null;

        yield return dialogueManager.CoCompleteRaw(
            systemPrompt,
            userPrompt,
            text => raw = text,
            err => error = err,
            Temperature);

        if (!string.IsNullOrEmpty(error))
        {
            onError?.Invoke(error);
            yield break;
        }

        if (!TryParseScores(raw, hits, out List<RelevanceScore> scores, out string parseError))
        {
            onError?.Invoke(parseError ?? "JSON 解析失败");
            yield break;
        }

        onSuccess?.Invoke(scores);
    }

    private static string BuildSystemPrompt()
    {
        return
            "你是游戏记忆召回助手。\n" +
            "任务：给定玩家当前一句话，以及若干条已通过话题别名命中的过程印象（Fragment），" +
            "评估每条与当前话语的相关度 relevance（0.0–1.0）。\n" +
            "规则：\n" +
            "- 只评相关度，不编造未给出的内容。\n" +
            "- 必须为每条输入的 fragmentId 各输出一条。\n" +
            "- 只输出 JSON 数组，不要 Markdown，不要其它文字。\n" +
            "格式：[{\"fragmentId\":\"...\",\"relevance\":0.0}]";
    }

    private static string BuildUserPrompt(string playerText, IReadOnlyList<RemiFragmentImpression> hits)
    {
        var sb = new StringBuilder(512);
        sb.Append("player_text: ").Append(playerText?.Trim() ?? "").Append('\n');
        sb.Append("fragments:\n");
        for (int i = 0; i < hits.Count; i++)
        {
            RemiFragmentImpression imp = hits[i];
            if (imp == null)
                continue;
            sb.Append("- id: ").Append(imp.id).Append('\n');
            sb.Append("  summary: ").Append(imp.summary?.Trim() ?? "").Append('\n');
            if (imp.topicAliases != null && imp.topicAliases.Count > 0)
            {
                sb.Append("  topic_aliases: ");
                for (int j = 0; j < imp.topicAliases.Count; j++)
                {
                    if (j > 0)
                        sb.Append(", ");
                    sb.Append(imp.topicAliases[j]);
                }

                sb.Append('\n');
            }
        }

        sb.Append("\n请只返回 JSON 数组。");
        return sb.ToString();
    }

    public static bool TryParseScores(
        string raw,
        IReadOnlyList<RemiFragmentImpression> hits,
        out List<RelevanceScore> scores,
        out string error)
    {
        scores = new List<RelevanceScore>();
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "空响应";
            return false;
        }

        string json = ExtractJsonArray(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "未找到 JSON 数组";
            return false;
        }

        var validIds = new HashSet<string>(StringComparer.Ordinal);
        if (hits != null)
        {
            foreach (RemiFragmentImpression imp in hits)
            {
                if (imp != null && !string.IsNullOrWhiteSpace(imp.id))
                    validIds.Add(imp.id);
            }
        }

        try
        {
            JsonData arr = JsonMapper.ToObject(json);
            if (arr == null || !arr.IsArray || arr.Count == 0)
            {
                error = "scores 不是非空数组";
                return false;
            }

            for (int i = 0; i < arr.Count; i++)
            {
                JsonData item = arr[i];
                if (item == null || !item.IsObject)
                    continue;

                string id = ReadString(item, "fragmentId");
                if (string.IsNullOrWhiteSpace(id))
                    id = ReadString(item, "id");
                if (string.IsNullOrWhiteSpace(id) || !validIds.Contains(id))
                    continue;

                scores.Add(new RelevanceScore
                {
                    fragmentId = id,
                    relevance = ReadFloat(item, "relevance"),
                });
            }

            if (scores.Count == 0)
            {
                error = "无有效 fragmentId";
                return false;
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
        if (obj == null || !obj.IsObject || !obj.ContainsKey(key) || obj[key] == null)
            return "";
        return obj[key].IsString ? (string)obj[key] : obj[key].ToString();
    }

    private static float ReadFloat(JsonData obj, string key)
    {
        if (obj == null || !obj.IsObject || !obj.ContainsKey(key) || obj[key] == null)
            return 0f;
        JsonData v = obj[key];
        if (v.IsDouble || v.IsInt || v.IsLong)
            return (float)(double)v;
        if (v.IsString && float.TryParse((string)v, out float f))
            return f;
        return 0f;
    }

    private static string ExtractJsonArray(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        string trimmed = raw.Trim();
        int start = trimmed.IndexOf('[');
        int end = trimmed.LastIndexOf(']');
        if (start >= 0 && end > start)
            return trimmed.Substring(start, end - start + 1);

        Match m = Regex.Match(trimmed, @"\[[\s\S]*\]");
        return m.Success ? m.Value : "";
    }
}
