using System;
using System.Collections.Generic;

/// <summary>Demo 终局路线摘要（Ending 填槽用；非后台抽象画像）。</summary>
public enum RemiDemoRouteDeviationKind
{
    None = 0,
    Dorm = 1,
}

[Serializable]
public class RemiDemoEndingRouteSnapshot
{
    public int finalSpineBeat;
    public int deviationKind;
    public bool day2LibraryIntroDone;
    public bool day3DeviationAccepted;
    public List<string> missedExperienceIds = new List<string>();
}

[Serializable]
public class RemiDemoEndingRelationshipSnapshot
{
    public int depthStage;
    public int delegationMilestoneCount;
    public bool hasInfluentialGate;
}

/// <summary>
/// Ending Bond 看法页输入：Weight top-K 筛选后的 Impression。
/// 设计师取舍：selected 为空则跳过 Bond 页（宁缺毋编，不进 AI prompt）；偏离事实进 Closing。
/// </summary>
[Serializable]
public class RemiDemoEndingBondSlots
{
    /// <summary>按 Weight 选出的可呈现印象（Mode B 成段源）。</summary>
    public List<RemiFragmentImpression> selectedImpressions = new List<RemiFragmentImpression>();
    /// <summary>是否播放 Bond 看法页（selected 非空）。</summary>
    public bool hasBondPresentation;
    /// <summary>Resonance 提示（进 Mode B brief，不单独成页）。</summary>
    public List<string> insideJokeKeywords = new List<string>();
    /// <summary>遗留字段：旧 Bond 填空槽（见 Memory/废弃）；Mode B 以 selectedImpressions 为准。</summary>
    public string traitPrimary = "";
    /// <summary>遗留：已弃用的 depth 夸夸保底；保持空。</summary>
    public string traitFallback = "";
    /// <summary>偏离事实骨（供 Closing / 调试；不再作为 Bond 模板页）。</summary>
    public string routeReflection = "";
    public string routeReflectionFallback = "";
}

[Serializable]
public class RemiDemoEndingTimeline
{
    public int eventCount;
    public int firstStoryDay;
    public int lastStoryDay;
}

/// <summary>Demo 通关一次性的 Ending 输入包（玩家向路径；可见层不含 raw quote）。</summary>
[Serializable]
public class RemiDemoEndingPayload
{
    public string version = "demo-4-bond-mode-b";
    public List<RemiSharedExperienceEntry> sharedExperiences = new List<RemiSharedExperienceEntry>();
    public RemiDemoEndingRouteSnapshot route = new RemiDemoEndingRouteSnapshot();
    public RemiDemoEndingRelationshipSnapshot relationship = new RemiDemoEndingRelationshipSnapshot();
    public RemiDemoEndingBondSlots bondSlots = new RemiDemoEndingBondSlots();
    public RemiDemoEndingTimeline timeline = new RemiDemoEndingTimeline();
    public string closingTemplateFilled = "";
    /// <summary>遗留快照（关键词管线）；Ending 主路径改读 fragmentMemorySnapshot。</summary>
    public List<RemiChatFragmentEntry> chatFragmentSnapshot = new List<RemiChatFragmentEntry>();
    /// <summary>Pipeline Fragment Memory 印象快照。</summary>
    public List<RemiFragmentImpression> fragmentMemorySnapshot = new List<RemiFragmentImpression>();
}

/// <summary>后台 RunTelemetry（DemoFinale 写入；无 UI、无玩家可见标签）。</summary>
[Serializable]
public class RemiDemoRunTelemetrySnapshot
{
    public string version = "demo-1";
    public int playerFaceMessages;
    public int playerSocialMessages;
    public int sharedExperiencesRecorded;
    public int nightPhasePlayerMessages;
    public int totalPlayerMessages;
    public float nightMessageRatio;
    public int finalSpineBeat;
    public int finalDepthStage;
    public int finalDelegationMilestones;
    public long finalizedUnixMs;
}
