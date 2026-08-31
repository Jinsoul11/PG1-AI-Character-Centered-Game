using UnityEngine;

/// <summary>脊柱 moment 的预设玩家回复（Story Chip）。</summary>
public enum RemiSpineStoryChipId
{
    /// <summary>已停用（进馆不要求 Chip）；仅用于旧存档气泡强调。</summary>
    Day2AcceptLibraryInvite = 1,
    /// <summary>偏离窗口 Open：玩家主动提出改安排（Demo 固定 Chip）。</summary>
    Day3InviteToDorm = 2,
    /// <summary>已停用：旧保底提案确认「那走吧」。</summary>
    Day3ConfirmLetsGo = 4,
}

public readonly struct RemiSpineStoryChipOption
{
    public RemiSpineStoryChipId Id { get; }
    /// <summary>写入存档 / LLM 的纯文本。</summary>
    public string Label { get; }
    /// <summary>带 TMP 强调的展示文案（Chip 按钮、气泡）。</summary>
    public string DisplayLabel { get; }

    public RemiSpineStoryChipOption(RemiSpineStoryChipId id, string label, string displayLabel)
    {
        Id = id;
        Label = label ?? string.Empty;
        DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? Label : displayLabel;
    }
}

/// <summary>Demo 脊柱 Story Chip 文案与消费状态（PlayerPrefs）。</summary>
public static class RemiDemoSpineStoryChips
{
    private const string PrefsDay2Ack = "RemiStory_Day2InviteChipAck";
    private const string PrefsDay3Used = "RemiStory_Day3InviteChipUsed";
    private const string PrefsDay3PendingConfirm = "RemiStory_Day3DeviationPendingConfirm";

    private const string Day2PlayerLine = "好，我下午过去。";
    private const string Day3PlayerLine = "今晚方便来宿舍聊聊吗？";
    private const string Day3ConfirmLetsGoLine = "那走吧。";

    private const string Day2RemiInviteAnchor = "图书馆";
    private const string Day3NudgeExhibitionAnchor = "作品展";
    private const string Day3OfferHomeAnchor = "我家";
    private const string Day3PlayerAnchor = "宿舍";
    private const string Day3RemiReplyAnchor = "破例";
    private const string Day3ConfirmAnchor = "走吧";

    public static bool IsDay2ChipAcknowledged => PlayerPrefs.GetInt(PrefsDay2Ack, 0) == 1;

    public static bool IsDay3ChipUsed => PlayerPrefs.GetInt(PrefsDay3Used, 0) == 1;

    /// <summary>Remi 已主动提出偏离，等待玩家确认「那走吧」。</summary>
    public static bool IsDay3DeviationPendingConfirm =>
        PlayerPrefs.GetInt(PrefsDay3PendingConfirm, 0) == 1;

    public static string GetPlayerLine(RemiSpineStoryChipId id) => id switch
    {
        RemiSpineStoryChipId.Day2AcceptLibraryInvite => Day2PlayerLine,
        RemiSpineStoryChipId.Day3InviteToDorm => Day3PlayerLine,
        RemiSpineStoryChipId.Day3ConfirmLetsGo => Day3ConfirmLetsGoLine,
        _ => string.Empty,
    };

    public static string GetPlayerLineDisplay(RemiSpineStoryChipId id) =>
        Apply(GetPlayerLine(id), GetPlayerEmphasis(id));

    public static bool IsDay3ConfirmLetsGoLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        string plain = RemiDialogueEmphasis.StripRichText(text.Trim());
        return string.Equals(plain, Day3ConfirmLetsGoLine, System.StringComparison.Ordinal);
    }

    public static bool IsDay3InviteToDormLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        string plain = RemiDialogueEmphasis.StripRichText(text.Trim());
        return string.Equals(plain, Day3PlayerLine, System.StringComparison.Ordinal);
    }

    /// <summary>当面 Chip 是否为当前可用的 Day3 邀约/确认类按钮。</summary>
    public static bool IsDay3FaceStoryChipLine(string text) =>
        IsDay3InviteToDormLine(text) || IsDay3ConfirmLetsGoLine(text);

    public static RemiSpineStoryChipOption Day2AcceptLibraryInvite => BuildChip(
        RemiSpineStoryChipId.Day2AcceptLibraryInvite,
        Day2PlayerLine,
        RemiDialogueEmphasisSpec.Whole);

    public static RemiSpineStoryChipOption Day3InviteToDorm => BuildChip(
        RemiSpineStoryChipId.Day3InviteToDorm,
        Day3PlayerLine,
        RemiDialogueEmphasisSpec.WithAnchors(Day3PlayerAnchor));

    public static RemiSpineStoryChipOption Day3ConfirmLetsGo => BuildChip(
        RemiSpineStoryChipId.Day3ConfirmLetsGo,
        Day3ConfirmLetsGoLine,
        RemiDialogueEmphasisSpec.WithAnchors(Day3ConfirmAnchor));

    public static RemiSpineStoryChipOption GetChipOption(RemiSpineStoryChipId id) => id switch
    {
        RemiSpineStoryChipId.Day2AcceptLibraryInvite => Day2AcceptLibraryInvite,
        RemiSpineStoryChipId.Day3InviteToDorm => Day3InviteToDorm,
        RemiSpineStoryChipId.Day3ConfirmLetsGo => Day3ConfirmLetsGo,
        _ => default,
    };

    /// <summary>Day2 Remi 共现邀请（手机固定消息）强调地点。</summary>
    public static string FormatDay2InviteMessageDisplay(string plainInvite) =>
        Apply(plainInvite, RemiDialogueEmphasisSpec.WithAnchors(Day2RemiInviteAnchor));

    /// <summary>Day3 开场 nudge（仍在图书馆轨道）强调地点 / 作品展。</summary>
    public static string FormatDay3NudgeMessageDisplay(string plainNudge)
    {
        if (string.IsNullOrWhiteSpace(plainNudge))
            return plainNudge;
        if (plainNudge.Contains(Day3NudgeExhibitionAnchor))
            return Apply(plainNudge, RemiDialogueEmphasisSpec.WithAnchors(Day2RemiInviteAnchor, Day3NudgeExhibitionAnchor));
        return Apply(plainNudge, RemiDialogueEmphasisSpec.WithAnchors(Day2RemiInviteAnchor));
    }

    /// <summary>Day3 保底提案强调「去她家」。</summary>
    public static string FormatDay3OfferMessageDisplay(string plainOffer)
    {
        if (string.IsNullOrWhiteSpace(plainOffer))
            return plainOffer;
        if (plainOffer.Contains(Day3OfferHomeAnchor))
            return Apply(plainOffer, RemiDialogueEmphasisSpec.WithAnchors(Day3OfferHomeAnchor));
        if (plainOffer.Contains("参观"))
            return Apply(plainOffer, RemiDialogueEmphasisSpec.WithAnchors("参观"));
        return plainOffer;
    }

    /// <summary>Day3 Remi 破例答应（手机/overlay）强调关键词。</summary>
    public static string FormatDay3RemiAcceptDisplay(string plainReply) =>
        Apply(plainReply, RemiDialogueEmphasisSpec.WithAnchors(Day3RemiReplyAnchor));

    /// <summary>手机聊天加载时：为已存的脊柱纯文本补上强调。</summary>
    public static string TryFormatPersistedPhoneLine(string role, string plain)
    {
        if (string.IsNullOrWhiteSpace(plain))
            return plain;

        string trimmed = plain.Trim();
        bool isPlayer = string.Equals(role, "user", System.StringComparison.OrdinalIgnoreCase);

        if (isPlayer)
        {
            if (trimmed == Day2PlayerLine)
                return GetPlayerLineDisplay(RemiSpineStoryChipId.Day2AcceptLibraryInvite);
            if (trimmed == Day3PlayerLine)
                return GetPlayerLineDisplay(RemiSpineStoryChipId.Day3InviteToDorm);
            if (trimmed == Day3ConfirmLetsGoLine)
                return GetPlayerLineDisplay(RemiSpineStoryChipId.Day3ConfirmLetsGo);
            return trimmed;
        }

        if (trimmed.Contains(Day3RemiReplyAnchor))
            return FormatDay3RemiAcceptDisplay(trimmed);
        if (trimmed.Contains(Day3OfferHomeAnchor) || trimmed.Contains("参观"))
            return FormatDay3OfferMessageDisplay(trimmed);
        if (trimmed.Contains(Day2RemiInviteAnchor) && trimmed.Contains(Day3NudgeExhibitionAnchor))
            return FormatDay3NudgeMessageDisplay(trimmed);
        if (trimmed.Contains(Day2RemiInviteAnchor) && trimmed.Contains("下午"))
            return FormatDay2InviteMessageDisplay(trimmed);

        return trimmed;
    }

    /// <summary>Day2 邀约已送达后玩家一次性短信确认（不触发 Remi 回信；进馆不依赖此标记）。</summary>
    public static void MarkDay2Acknowledged()
    {
        PlayerPrefs.SetInt(PrefsDay2Ack, 1);
        PlayerPrefs.Save();
    }

    public static void MarkDay3Used()
    {
        PlayerPrefs.SetInt(PrefsDay3Used, 1);
        ClearDay3PendingConfirm();
        PlayerPrefs.Save();
    }

    public static void MarkDay3PendingConfirm()
    {
        PlayerPrefs.SetInt(PrefsDay3PendingConfirm, 1);
        PlayerPrefs.Save();
    }

    public static void ClearDay3PendingConfirm()
    {
        PlayerPrefs.DeleteKey(PrefsDay3PendingConfirm);
    }

    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(PrefsDay2Ack);
        PlayerPrefs.DeleteKey(PrefsDay3Used);
        PlayerPrefs.DeleteKey(PrefsDay3PendingConfirm);
    }

    private static RemiSpineStoryChipOption BuildChip(
        RemiSpineStoryChipId id,
        string plain,
        RemiDialogueEmphasisSpec spec) =>
        new RemiSpineStoryChipOption(id, plain, Apply(plain, spec));

    private static RemiDialogueEmphasisSpec GetPlayerEmphasis(RemiSpineStoryChipId id) => id switch
    {
        RemiSpineStoryChipId.Day2AcceptLibraryInvite => RemiDialogueEmphasisSpec.Whole,
        RemiSpineStoryChipId.Day3InviteToDorm => RemiDialogueEmphasisSpec.WithAnchors(Day3PlayerAnchor),
        RemiSpineStoryChipId.Day3ConfirmLetsGo => RemiDialogueEmphasisSpec.WithAnchors(Day3ConfirmAnchor),
        _ => RemiDialogueEmphasisSpec.None,
    };

    private static string Apply(string plain, RemiDialogueEmphasisSpec spec) =>
        RemiDialogueEmphasis.Apply(plain, spec);
}
