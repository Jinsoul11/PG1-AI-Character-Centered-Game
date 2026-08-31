using System;

/// <summary>
/// Demo 日起点存档（仅 Day2 / Day3）。
/// 含当面 Conversation1 与 LLM history；读档接着聊。不含 RemiDialogueArchive。
/// </summary>
[Serializable]
public class RemiDemoDaySaveData
{
    public int storyDay;
    public long savedUnixMs;

    public string worldTimeJson = "";
    public int rhythmStoryStarted;
    public string rhythmDelegations = "";
    public int rhythmBeats;
    public int rhythmDepthStage;
    public int rhythmAnchors;

    public int spineBeat;
    public int bookQuestState;
    public int bookQuestHasBook;
    public int phoneUnlocked;

    public int storyClassroomOpening;
    public int storyLibraryDay2;
    public int storyApartmentDay3;
    /// <summary><see cref="RemiLibraryDay2CoPresenceFlow"/> Prefs 状态。</summary>
    public int day2CoPresenceState;
    public int day2InviteChipAck;
    public int day3InviteChipUsed;
    public int day2GoToDoorHintShown;

    public int dayBlockSlot;
    public int dayBlockKind;
    public int dayBlockInAnchor;

    public string sharedExperienceJson = "";

    /// <summary>Fragment 管线文件快照（原文）；可空。</summary>
    public string curatorStoreJson = "";
    public string unitStoreJson = "";
    public string fragmentMemoryJson = "";
    public string socialConversationJson = "";
    public string phoneSendDay2InviteLine = "";
    public string phoneSendDay3NudgeLine = "";

    /// <summary>当面历史面板 Conversation1 原文；读档还原 UI。</summary>
    public string faceConversationJson = "";

    /// <summary>合流 LLM messageHistory 原文；读档还原发给模型的上下文。</summary>
    public string llmMessageHistoryJson = "";
}

/// <summary>可用日起点槽索引。</summary>
[Serializable]
public class RemiDemoDaySaveIndex
{
    public bool hasDay2;
    public bool hasDay3;
    public long day2SavedUnixMs;
    public long day3SavedUnixMs;
}
