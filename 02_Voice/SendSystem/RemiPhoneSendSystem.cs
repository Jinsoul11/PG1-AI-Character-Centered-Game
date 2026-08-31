using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 手机通道 SendSystem：SocialChat 开口、落盘、失败回退固定句。
/// 同一 contentId 只生成一次；读档 / 再打开手机只回放已存句子。
/// </summary>
public static class RemiPhoneSendSystem
{
    public const string PrefsLinePrefix = "RemiPhoneSend_Line_";

    private static readonly HashSet<string> InFlightIds = new HashSet<string>();

    public static string LinePrefsKey(string contentId) =>
        PrefsLinePrefix + (contentId ?? string.Empty).Trim();

    public static bool HasPersistedLine(string contentId)
    {
        string line = GetPersistedLine(contentId);
        return !string.IsNullOrWhiteSpace(line);
    }

    public static string GetPersistedLine(string contentId)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            return string.Empty;
        return PlayerPrefs.GetString(LinePrefsKey(contentId), string.Empty);
    }

    public static void SetPersistedLine(string contentId, string line)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            return;
        string trimmed = line != null ? line.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            PlayerPrefs.DeleteKey(LinePrefsKey(contentId));
        else
            PlayerPrefs.SetString(LinePrefsKey(contentId), trimmed);
        PlayerPrefs.Save();
    }

    public static void ClearPersistedLine(string contentId)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            return;
        PlayerPrefs.DeleteKey(LinePrefsKey(contentId));
    }

    /// <summary>已生成或 fallback 的句子写入 prefs + 聊天（去重），不调 LLM。</summary>
    public static void PersistDeliveredLine(string contentId, string line)
    {
        PersistGeneratedLine(contentId, line);
    }

    public static void ClearAll()
    {
        ClearPersistedLine(RemiSendSystemContentIds.Day2PhoneInvite);
        ClearPersistedLine(RemiSendSystemContentIds.Day3PhoneNudge);
        ClearPersistedLine(RemiSendSystemContentIds.Day3PhoneDeviationOffer);
        ClearPersistedLine(RemiSendSystemContentIds.Day3PhoneAccept);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 已有存句：写入聊天（去重）并刷新 UI，不调 LLM。
    /// 尚无存句且 <paramref name="generateIfMissing"/>：SendSystem（SocialChat）；失败用 fallback。
    /// 尚无存句且不允许生成：直接落盘 fallback。
    /// </summary>
    public static IEnumerator CoDeliverOrRestore(
        string contentId,
        string hardcodedFallback,
        bool generateIfMissing)
    {
        string id = contentId != null ? contentId.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            yield break;

        while (InFlightIds.Contains(id))
            yield return null;

        InFlightIds.Add(id);
        try
        {
            string existing = GetPersistedLine(id);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                PersistAndReload(existing);
                yield break;
            }

            string fallback = ResolveFallback(id, hardcodedFallback);
            if (!generateIfMissing)
            {
                PersistGeneratedLine(id, fallback);
                yield break;
            }

            string generated = null;
            yield return CoGenerateSocialLine(id, line => generated = line);

            string finalLine = !string.IsNullOrWhiteSpace(generated) ? generated.Trim() : fallback;
            PersistGeneratedLine(id, finalLine);
        }
        finally
        {
            InFlightIds.Remove(id);
        }
    }

    private static IEnumerator CoGenerateSocialLine(string contentId, System.Action<string> onLine)
    {
        PromptedDialogueAgent agent = PromptedDialogueAgent.Instance != null
            ? PromptedDialogueAgent.Instance
            : Object.FindObjectOfType<PromptedDialogueAgent>();
        if (agent == null)
        {
            Debug.LogWarning("[RemiPhoneSendSystem] 未找到 PromptedDialogueAgent，使用 fallback。");
            onLine?.Invoke(null);
            yield break;
        }

        RemiSendSystemContentManager.EnsureExists();
        string context = RemiSendSystemContentManager.Instance != null
            ? RemiSendSystemContentManager.Instance.GetInitiator(contentId)
            : string.Empty;
        if (string.IsNullOrWhiteSpace(context))
        {
            Debug.LogWarning($"[RemiPhoneSendSystem] 无 director_context：{contentId}，使用 fallback。");
            onLine?.Invoke(null);
            yield break;
        }

        RemiPresenceService presence = RemiPresenceService.Instance;
        RemiInteractionChannel previousChannel = presence != null
            ? presence.CurrentChannel
            : RemiInteractionChannel.FaceToFace;
        presence?.SetInteractionChannel(RemiInteractionChannel.Social);

        string remiLine = null;
        bool hadError = false;

        yield return agent.SendSystemSocial(
            context,
            (text, expr) =>
            {
                remiLine = text;
            },
            err =>
            {
                hadError = true;
                if (!string.IsNullOrWhiteSpace(err))
                    Debug.LogWarning($"[RemiPhoneSendSystem] SendSystem 失败，改用 fallback：{err}");
            });

        presence?.SetInteractionChannel(previousChannel);

        if (hadError || string.IsNullOrWhiteSpace(remiLine))
            onLine?.Invoke(null);
        else
            onLine?.Invoke(remiLine.Trim());
    }

    private static string ResolveFallback(string contentId, string hardcodedFallback)
    {
        RemiSendSystemContentManager.EnsureExists();
        string fromContent = RemiSendSystemContentManager.Instance != null
            ? RemiSendSystemContentManager.Instance.GetPhoneLine(contentId, hardcodedFallback)
            : hardcodedFallback;
        if (!string.IsNullOrWhiteSpace(fromContent))
            return fromContent.Trim();
        return hardcodedFallback != null ? hardcodedFallback.Trim() : string.Empty;
    }

    private static void PersistGeneratedLine(string contentId, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        line = line.Trim();
        SetPersistedLine(contentId, line);
        PersistAndReload(line);
    }

    private static void PersistAndReload(string line)
    {
        PhoneAppPanel.TryPersistRemiMessage(line);
        PhoneAppPanel panel = UiManager.Instance != null
            ? UiManager.Instance.GetPanel<PhoneAppPanel>()
            : null;
        if (panel != null && panel.gameObject.activeInHierarchy)
            panel.ReloadChatFromStorage();
    }
}
