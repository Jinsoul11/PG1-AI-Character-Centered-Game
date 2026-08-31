using System;
using System.Collections.Generic;

/// <summary>Dialogue Archive 语料来源（生产管线，非说话人）。</summary>
public enum RemiDialogueArchiveSource
{
    /// <summary>玩家/角色自由对话（LLM 闲聊）。</summary>
    FreeChat = 0,
    /// <summary>剧本 / SendSystem / 固定剧情句。</summary>
    Scripted = 1,
    /// <summary>系统提示等（默认不进日结提取）。</summary>
    System = 2,
}

/// <summary>
/// 对话真源一条记录（不做理解）。
/// Demo 不含 phase；日结/提取用 storyDay + 元数据。
/// </summary>
[Serializable]
public class RemiDialogueArchiveEntry
{
    public string content = "";
    /// <summary>规范化：player / Remi（其它角色原样保留，提取时可跳过）。</summary>
    public string speaker = "";
    public int storyDay;
    /// <summary><see cref="RemiDialogueDepthStage"/> 写入快照。</summary>
    public int depthStage;
    /// <summary><see cref="RemiInteractionChannel"/>。</summary>
    public int channel;
    /// <summary><see cref="RemiDialogueArchiveSource"/>。</summary>
    public int source;
    /// <summary>写入时 Unix 毫秒（调试用；非叙事钟）。</summary>
    public long recordedUnixMs;

    public RemiDialogueArchiveEntry() { }

    public RemiDialogueArchiveEntry(
        string content,
        string speaker,
        int storyDay,
        RemiDialogueDepthStage depthStage,
        RemiInteractionChannel channel,
        RemiDialogueArchiveSource source)
    {
        this.content = content ?? "";
        this.speaker = speaker ?? "";
        this.storyDay = storyDay;
        this.depthStage = (int)depthStage;
        this.channel = (int)channel;
        this.source = (int)source;
        recordedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public RemiDialogueArchiveSource SourceKind =>
        (RemiDialogueArchiveSource)Math.Clamp(source, 0, 2);

    public RemiInteractionChannel ChannelKind =>
        (RemiInteractionChannel)Math.Clamp(channel, 0, 1);

    public RemiDialogueDepthStage DepthStageKind =>
        (RemiDialogueDepthStage)Math.Clamp(depthStage, 0, 2);
}

[Serializable]
public class RemiDialogueArchiveStore
{
    public List<RemiDialogueArchiveEntry> entries = new List<RemiDialogueArchiveEntry>();
}
