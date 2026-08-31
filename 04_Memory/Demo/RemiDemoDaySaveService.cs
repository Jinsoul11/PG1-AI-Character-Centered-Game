using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Demo 日起点存档：仅保存第 2 / 第 3 天进入教室可玩时的状态。
/// 读档保留当面 Conversation1 与 LLM history（接着聊）；新游戏才清空。
/// 读档不清 <see cref="RemiDialogueArchive"/>。
/// </summary>
public static class RemiDemoDaySaveService
{
    public const string IndexKey = "RemiDemoDaySaveIndex";
    public const string SlotKeyDay2 = "RemiDemoDaySave_Day2";
    public const string SlotKeyDay3 = "RemiDemoDaySave_Day3";

    private const string PrefsWorldTime = "RemiWorldTime";
    private const string PrefsRhythmStory = "RemiRhythm_StoryStarted";
    private const string PrefsRhythmDelegations = "RemiRhythm_Delegations";
    private const string PrefsRhythmBeats = "RemiRhythm_PlayedBeats";
    private const string PrefsRhythmDepthStage = "RemiRhythm_DepthStage";
    private const string PrefsRhythmAnchors = "RemiRhythm_StoryAnchors";
    private const string PrefsSpineBeat = "RemiDemoSpine_Beat";
    private const string PrefsBookState = "RemiBookQuest_State";
    private const string PrefsBookHasBook = "RemiBookQuest_HasBook";
    private const string PrefsPhone = "PhoneApp_Unlocked";
    private const string PrefsDay2Chip = "RemiStory_Day2InviteChipAck";
    private const string PrefsDay3Chip = "RemiStory_Day3InviteChipUsed";
    private const string PrefsDay2DoorHint = "RemiDay2_GoToDoorHintShown";
    private const string PrefsLibraryDay2Story = "RemiStory_LibraryDay2CoPresence";
    private const string PrefsApartmentDay3Story = "RemiStory_ApartmentDay3CoPresence";
    private const string PrefsDay2CoPresence = RemiLibraryDay2CoPresenceFlow.PrefsKeyState;

    public static string SlotKeyForDay(int storyDay) =>
        storyDay == 3 ? SlotKeyDay3 : SlotKeyDay2;

    public static bool HasSlot(int storyDay)
    {
        if (storyDay != 2 && storyDay != 3)
            return false;
        RemiDemoDaySaveIndex index = LoadIndex();
        if (storyDay == 3 ? index.hasDay3 : index.hasDay2)
            return true;

        // 索引丢失时回退检查槽文件
        if (JsonMgr.Instance == null)
            return false;
        RemiDemoDaySaveData data = JsonMgr.Instance.LoadData<RemiDemoDaySaveData>(SlotKeyForDay(storyDay));
        return data != null && data.storyDay == storyDay && data.savedUnixMs > 0;
    }

    public static bool HasAnySlot()
    {
        RemiDemoDaySaveIndex index = LoadIndex();
        return index.hasDay2 || index.hasDay3;
    }

    /// <summary>优先最新日（3→2）。</summary>
    public static int ResolveLatestSlotDay()
    {
        if (HasSlot(3)) return 3;
        if (HasSlot(2)) return 2;
        return 0;
    }

    public static RemiDemoDaySaveIndex LoadIndex()
    {
        if (JsonMgr.Instance == null)
            return new RemiDemoDaySaveIndex();
        return JsonMgr.Instance.LoadData<RemiDemoDaySaveIndex>(IndexKey) ?? new RemiDemoDaySaveIndex();
    }

    /// <summary>在 Day2 / Day3 日起点可玩时调用：快照当前进度（不含 Archive）。</summary>
    public static void SaveDayStart(int storyDay)
    {
        if (storyDay != 2 && storyDay != 3)
        {
            Debug.LogWarning($"[RemiDemoDaySave] 仅支持 Day2/Day3，忽略 day={storyDay}");
            return;
        }

        if (JsonMgr.Instance == null)
        {
            Debug.LogWarning("[RemiDemoDaySave] JsonMgr 不可用，跳过存档");
            return;
        }

        // 先落盘 Presence，保证 prefs 与内存一致
        RemiPresenceService.Instance?.FlushPersistedStateForSave();

        // 日起点快照：尚未发生的当日/后续场景内容一律记为未完成，保证读档可重走触发链
        NormalizeLivePrefsForDayStartSnapshot(storyDay);

        var data = new RemiDemoDaySaveData
        {
            storyDay = storyDay,
            savedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            worldTimeJson = PlayerPrefs.GetString(PrefsWorldTime, ""),
            rhythmStoryStarted = PlayerPrefs.GetInt(PrefsRhythmStory, 0),
            rhythmDelegations = PlayerPrefs.GetString(PrefsRhythmDelegations, ""),
            rhythmBeats = PlayerPrefs.GetInt(PrefsRhythmBeats, 0),
            rhythmDepthStage = PlayerPrefs.GetInt(PrefsRhythmDepthStage, 0),
            rhythmAnchors = PlayerPrefs.GetInt(PrefsRhythmAnchors, 0),
            spineBeat = PlayerPrefs.GetInt(PrefsSpineBeat, 0),
            bookQuestState = PlayerPrefs.GetInt(PrefsBookState, 0),
            bookQuestHasBook = PlayerPrefs.GetInt(PrefsBookHasBook, 0),
            phoneUnlocked = PlayerPrefs.GetInt(PrefsPhone, 0),
            storyClassroomOpening = PlayerPrefs.GetInt(StoryDirector.PrefsClassroomOpeningPlayed, 0),
            storyLibraryDay2 = PlayerPrefs.GetInt(PrefsLibraryDay2Story, 0),
            storyApartmentDay3 = PlayerPrefs.GetInt(PrefsApartmentDay3Story, 0),
            day2CoPresenceState = PlayerPrefs.GetInt(PrefsDay2CoPresence, 0),
            day2InviteChipAck = PlayerPrefs.GetInt(PrefsDay2Chip, 0),
            day3InviteChipUsed = PlayerPrefs.GetInt(PrefsDay3Chip, 0),
            day2GoToDoorHintShown = PlayerPrefs.GetInt(PrefsDay2DoorHint, 0),
            dayBlockSlot = PlayerPrefs.GetInt("RemiDayBlock_Slot", 0),
            dayBlockKind = PlayerPrefs.GetInt("RemiDayBlock_Kind", 0),
            dayBlockInAnchor = PlayerPrefs.GetInt("RemiDayBlock_InAnchor", 0),
            sharedExperienceJson = PlayerPrefs.GetString(RemiSharedExperienceMemory.PrefsStoreKey, ""),
            curatorStoreJson = ReadPersistentJson(RemiMemoryCuratorStore.JsonSaveKey),
            unitStoreJson = ReadPersistentJson(RemiFragmentUnitStore.JsonSaveKey),
            fragmentMemoryJson = ReadPersistentJson(RemiFragmentMemory.JsonSaveKey),
            socialConversationJson = ReadPersistentJson(PhoneAppPanel.SaveKey),
            phoneSendDay2InviteLine = RemiPhoneSendSystem.GetPersistedLine(RemiSendSystemContentIds.Day2PhoneInvite),
            phoneSendDay3NudgeLine = RemiPhoneSendSystem.GetPersistedLine(RemiSendSystemContentIds.Day3PhoneNudge),
            faceConversationJson = ReadPersistentJson("Conversation1"),
            llmMessageHistoryJson = SnapshotLlmMessageHistoryJson(),
        };

        JsonMgr.Instance.SaveData(data, SlotKeyForDay(storyDay));

        RemiDemoDaySaveIndex index = LoadIndex();
        if (storyDay == 2)
        {
            index.hasDay2 = true;
            index.day2SavedUnixMs = data.savedUnixMs;
        }
        else
        {
            index.hasDay3 = true;
            index.day3SavedUnixMs = data.savedUnixMs;
        }

        JsonMgr.Instance.SaveData(index, IndexKey);
        Debug.Log($"[RemiDemoDaySave] 已保存 Day{storyDay} 起点档");
    }

    /// <summary>
    /// 日结完成后：把当前 disk 上的 Curator / Unit / FragmentMemory
    /// 写回已有的 Day2/Day3 起点档，避免读档用「日结前空快照」覆盖终库。
    /// </summary>
    public static void RefreshPipelineSnapshotInDayStartSlots()
    {
        if (JsonMgr.Instance == null)
            return;

        string curator = ReadPersistentJson(RemiMemoryCuratorStore.JsonSaveKey);
        string units = ReadPersistentJson(RemiFragmentUnitStore.JsonSaveKey);
        string fragments = ReadPersistentJson(RemiFragmentMemory.JsonSaveKey);

        TryPatchSlotPipeline(2, curator, units, fragments);
        TryPatchSlotPipeline(3, curator, units, fragments);
    }

    private static void TryPatchSlotPipeline(
        int storyDay,
        string curatorJson,
        string unitJson,
        string fragmentJson)
    {
        if (!HasSlot(storyDay))
            return;

        RemiDemoDaySaveData data = JsonMgr.Instance.LoadData<RemiDemoDaySaveData>(SlotKeyForDay(storyDay));
        if (data == null || data.storyDay != storyDay)
            return;

        data.curatorStoreJson = curatorJson ?? "";
        data.unitStoreJson = unitJson ?? "";
        data.fragmentMemoryJson = fragmentJson ?? "";
        JsonMgr.Instance.SaveData(data, SlotKeyForDay(storyDay));
        Debug.Log($"[RemiDemoDaySave] 已刷新 Day{storyDay} 起点档中的 Fragment pipeline 快照");
    }

    /// <summary>
    /// 读档：还原日起点状态并前往教室。保留当面会话与 LLM 上下文；不清 RemiDialogueArchive。
    /// </summary>
    public static bool TryLoadDayStart(int storyDay, out string error)
    {
        error = null;
        if (storyDay != 2 && storyDay != 3)
        {
            error = "仅支持载入第 2 / 第 3 天";
            return false;
        }

        if (JsonMgr.Instance == null)
        {
            error = "JsonMgr 不可用";
            return false;
        }

        RemiDemoDaySaveData data = JsonMgr.Instance.LoadData<RemiDemoDaySaveData>(SlotKeyForDay(storyDay));
        if (data == null || data.storyDay != storyDay || data.savedUnixMs <= 0)
        {
            error = $"没有 Day{storyDay} 存档";
            return false;
        }

        ApplySlot(data);
        NormalizeDayStartProgressAfterLoad(storyDay);
        // 明确：不触碰 RemiDialogueArchive
        BeginTravelAfterLoad(storyDay);
        Debug.Log($"[RemiDemoDaySave] 已载入 Day{storyDay} 起点（Archive 保留；当日后续触发可重走）");
        return true;
    }

    public static void ClearAllSlots()
    {
        if (JsonMgr.Instance == null)
            return;
        JsonMgr.Instance.DeleteData(SlotKeyDay2);
        JsonMgr.Instance.DeleteData(SlotKeyDay3);
        JsonMgr.Instance.DeleteData(IndexKey);
    }

    private static void ApplySlot(RemiDemoDaySaveData data)
    {
        WritePrefString(PrefsWorldTime, data.worldTimeJson);
        PlayerPrefs.SetInt(PrefsRhythmStory, data.rhythmStoryStarted);
        WritePrefString(PrefsRhythmDelegations, data.rhythmDelegations);
        PlayerPrefs.SetInt(PrefsRhythmBeats, data.rhythmBeats);
        PlayerPrefs.SetInt(PrefsRhythmDepthStage, data.rhythmDepthStage);
        PlayerPrefs.SetInt(PrefsRhythmAnchors, data.rhythmAnchors);
        PlayerPrefs.SetInt(PrefsSpineBeat, data.spineBeat);
        PlayerPrefs.SetInt(PrefsBookState, data.bookQuestState);
        PlayerPrefs.SetInt(PrefsBookHasBook, data.bookQuestHasBook);
        PlayerPrefs.SetInt(PrefsPhone, data.phoneUnlocked);
        PlayerPrefs.SetInt(StoryDirector.PrefsClassroomOpeningPlayed, data.storyClassroomOpening);
        PlayerPrefs.SetInt(PrefsLibraryDay2Story, data.storyLibraryDay2);
        PlayerPrefs.SetInt(PrefsApartmentDay3Story, data.storyApartmentDay3);
        PlayerPrefs.SetInt(PrefsDay2CoPresence, data.day2CoPresenceState);
        PlayerPrefs.SetInt(PrefsDay2Chip, data.day2InviteChipAck);
        PlayerPrefs.SetInt(PrefsDay3Chip, data.day3InviteChipUsed);
        PlayerPrefs.SetInt(PrefsDay2DoorHint, data.day2GoToDoorHintShown);
        PlayerPrefs.SetInt("RemiDayBlock_Slot", data.dayBlockSlot);
        PlayerPrefs.SetInt("RemiDayBlock_Kind", data.dayBlockKind);
        PlayerPrefs.SetInt("RemiDayBlock_InAnchor", data.dayBlockInAnchor);
        WritePrefString(RemiSharedExperienceMemory.PrefsStoreKey, data.sharedExperienceJson);
        PlayerPrefs.Save();

        // 日起点档可能早于日结落盘而快照到空 pipeline；勿用空串抹掉盘上已有终库。
        WritePersistentJsonIfPresent(RemiMemoryCuratorStore.JsonSaveKey, data.curatorStoreJson);
        WritePersistentJsonIfPresent(RemiFragmentUnitStore.JsonSaveKey, data.unitStoreJson);
        WritePersistentJsonIfPresent(RemiFragmentMemory.JsonSaveKey, data.fragmentMemoryJson);
        WritePersistentJson(PhoneAppPanel.SaveKey, data.socialConversationJson);
        RemiPhoneSendSystem.SetPersistedLine(
            RemiSendSystemContentIds.Day2PhoneInvite,
            data.phoneSendDay2InviteLine);
        // Day3 起点：邀约每次读档重打 SendSystem，不回放槽里的旧句
        if (data.storyDay == 3)
        {
            RemiPhoneSendSystem.ClearPersistedLine(RemiSendSystemContentIds.Day3PhoneNudge);
            RemiPhoneSendSystem.ClearPersistedLine(RemiSendSystemContentIds.Day3PhoneDeviationOffer);
            RemiPhoneSendSystem.ClearPersistedLine(RemiSendSystemContentIds.Day3PhoneAccept);
        }
        else
        {
            RemiPhoneSendSystem.SetPersistedLine(
                RemiSendSystemContentIds.Day3PhoneNudge,
                data.phoneSendDay3NudgeLine);
        }

        // 当面会话：读档接着聊（旧档无字段则保留磁盘现有，不强制清空）
        WritePersistentJsonIfPresent("Conversation1", data.faceConversationJson);
        WritePersistentJsonIfPresent(DeepSeekDialogueManager.MessageHistorySaveKey, data.llmMessageHistoryJson);
        RestoreFaceConversationUiAndLlmHistory();

        PhoneAppAccess.ReloadFromPrefs();

        // DDOL 若已存在则刷内存；首次进教室由各组件 Awake 从盘加载
        RemiPresenceService.Instance?.ReloadPersistedState();
        RemiSharedExperienceMemory.EnsureExists();
        RemiSharedExperienceMemory.Instance?.ReloadFromDisk();
        RemiMemoryCuratorStore.EnsureExists();
        RemiMemoryCuratorStore.Instance?.ReloadFromDisk();
        RemiFragmentUnitStore.EnsureExists();
        RemiFragmentUnitStore.Instance?.ReloadFromDisk();
        RemiFragmentMemory.EnsureExists();
        RemiFragmentMemory.Instance?.ReloadFromDisk();

        RemiDemoSpineDirector.EnsureExists();
        RemiLibraryDay2CoPresenceFlow.Instance?.ReloadStateFromPrefs();
    }

    /// <summary>
    /// 写入日起点档前：把「本日及之后才应发生」的场景进度清成未完成，
    /// 避免当前会话已玩过后续内容时把 Finished 写进日起点档。
    /// </summary>
    private static void NormalizeLivePrefsForDayStartSnapshot(int storyDay)
    {
        if (storyDay == 2)
        {
            PlayerPrefs.SetInt(PrefsLibraryDay2Story, 0);
            PlayerPrefs.SetInt(PrefsDay2CoPresence, 0);
            PlayerPrefs.SetInt(PrefsApartmentDay3Story, 0);
            PlayerPrefs.SetInt(PrefsDay3Chip, 0);
            // 日起点应为邀请已送达、尚未进馆 intro
            int beat = PlayerPrefs.GetInt(PrefsSpineBeat, 0);
            if (beat < (int)RemiDemoSpineBeat.Day2InviteDelivered)
                PlayerPrefs.SetInt(PrefsSpineBeat, (int)RemiDemoSpineBeat.Day2InviteDelivered);
            else if (beat > (int)RemiDemoSpineBeat.Day2InviteDelivered)
                PlayerPrefs.SetInt(PrefsSpineBeat, (int)RemiDemoSpineBeat.Day2InviteDelivered);
        }
        else if (storyDay == 3)
        {
            PlayerPrefs.SetInt(PrefsApartmentDay3Story, 0);
            PlayerPrefs.SetInt(PrefsDay3Chip, 0);
            RemiDemoSpineStoryChips.ClearDay3PendingConfirm();
            // Day2 馆内共现视为已完成
            PlayerPrefs.SetInt(PrefsLibraryDay2Story, 1);
            if (!PlayerPrefs.HasKey(PrefsDay2CoPresence) ||
                PlayerPrefs.GetInt(PrefsDay2CoPresence, 0) < (int)RemiLibraryDay2CoPresenceFlow.FlowState.Finished)
                PlayerPrefs.SetInt(PrefsDay2CoPresence, (int)RemiLibraryDay2CoPresenceFlow.FlowState.Finished);

            int beat = PlayerPrefs.GetInt(PrefsSpineBeat, 0);
            if (beat < (int)RemiDemoSpineBeat.Day3InviteReady)
                PlayerPrefs.SetInt(PrefsSpineBeat, (int)RemiDemoSpineBeat.Day3InviteReady);
            else if (beat > (int)RemiDemoSpineBeat.Day3InviteReady)
                PlayerPrefs.SetInt(PrefsSpineBeat, (int)RemiDemoSpineBeat.Day3InviteReady);
        }

        PlayerPrefs.Save();
    }

    /// <summary>
    /// 读档后再次规范化：覆盖旧档缺字段 / 当前会话残留，确保后续触发链可走。
    /// </summary>
    private static void NormalizeDayStartProgressAfterLoad(int storyDay)
    {
        NormalizeLivePrefsForDayStartSnapshot(storyDay);

        RemiWorldPlacement.ClearDay3ApartmentRelocationFlag();
        if (storyDay == 2 || storyDay == 3)
            RemiWorldPlacement.PrepareRemiAbsentFromClassroomForDay2();

        if (storyDay == 3)
        {
            PhoneAppPanel.KeepPersistedChatBeforeStoryDay(3);
            RemiPhoneSendSystem.ClearPersistedLine(RemiSendSystemContentIds.Day3PhoneNudge);
            RemiPhoneSendSystem.ClearPersistedLine(RemiSendSystemContentIds.Day3PhoneDeviationOffer);
            RemiPhoneSendSystem.ClearPersistedLine(RemiSendSystemContentIds.Day3PhoneAccept);
        }
        else if (storyDay == 2)
            RemiPhoneSendSystem.ClearPersistedLine(RemiSendSystemContentIds.Day3PhoneNudge);

        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector.Instance?.ForceApplyBeatFromPrefs();
        RemiLibraryDay2CoPresenceFlow.Instance?.ReloadStateFromPrefs();
    }

    private static void BeginTravelAfterLoad(int storyDay)
    {
        // Day2/Day3 教室开场 Remi 都不在教室
        RemiWorldPlacement.PrepareRemiAbsentFromClassroomForDay2();

        SceneTravelService.EnsureExists();
        SceneTravelService.SetPendingSpawnPointName(
            SceneTravelCatalog.GetSpawnPointName(SceneTravelLocation.Classroom));
        SceneTravelService.SetPendingTravelSubtitle(
            storyDay == 3
                ? "第三天。你又来到教室。"
                : "第二天。你来到教室。");
        SceneTravelService.Instance.TravelTo(SceneTravelLocation.Classroom);
    }

    private static void WritePrefString(string key, string value)
    {
        if (string.IsNullOrEmpty(value))
            PlayerPrefs.DeleteKey(key);
        else
            PlayerPrefs.SetString(key, value);
    }

    /// <summary>
    /// 退出前：把当前当面 Conversation1 + LLM history 写回最新日起点档，
    /// 保证读档能接到退出前的上下文（日起点快照之后又聊过的内容）。
    /// </summary>
    public static void FlushLiveConversationIntoLatestSlot()
    {
        int day = ResolveLatestSlotDay();
        if (day <= 0 || JsonMgr.Instance == null)
            return;

        DeepSeekDialogueManager.Instance?.PersistMessageHistoryToDisk();

        RemiDemoDaySaveData data = JsonMgr.Instance.LoadData<RemiDemoDaySaveData>(SlotKeyForDay(day));
        if (data == null || data.storyDay != day)
            return;

        data.faceConversationJson = ReadPersistentJson("Conversation1");
        data.llmMessageHistoryJson = SnapshotLlmMessageHistoryJson();
        JsonMgr.Instance.SaveData(data, SlotKeyForDay(day));
        Debug.Log($"[RemiDemoDaySave] 已把当面会话刷入 Day{day} 起点档");
    }

    private static string SnapshotLlmMessageHistoryJson()
    {
        DeepSeekDialogueManager.Instance?.PersistMessageHistoryToDisk();
        return ReadPersistentJson(DeepSeekDialogueManager.MessageHistorySaveKey);
    }

    private static void RestoreFaceConversationUiAndLlmHistory()
    {
        if (DeepSeekDialogueManager.Instance != null)
            DeepSeekDialogueManager.Instance.LoadMessageHistoryFromDisk();

        if (UiManager.Instance == null)
            return;

        ChatHistoryPanel panel = UiManager.Instance.GetPanel<ChatHistoryPanel>();
        if (panel != null)
            panel.ReloadChatFromStorage();
    }

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static string ReadPersistentJson(string fileKey)
    {
        string path = Path.Combine(Application.persistentDataPath, fileKey + ".json");
        if (!File.Exists(path))
            return "";
        try
        {
            return File.ReadAllText(path, Utf8NoBom);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RemiDemoDaySave] 读 {fileKey} 失败: {ex.Message}");
            return "";
        }
    }

    private static void WritePersistentJson(string fileKey, string json)
    {
        string path = Path.Combine(Application.persistentDataPath, fileKey + ".json");
        try
        {
            if (string.IsNullOrEmpty(json))
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }

            File.WriteAllText(path, json, Utf8NoBom);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RemiDemoDaySave] 写 {fileKey} 失败: {ex.Message}");
        }
    }

    /// <summary>槽内 pipeline 为空时保留磁盘已有文件（避免日结前空快照覆盖终库）。</summary>
    private static void WritePersistentJsonIfPresent(string fileKey, string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.Log(
                $"[RemiDemoDaySave] 起点档 {fileKey} 为空，保留磁盘现有文件（若有）");
            return;
        }

        WritePersistentJson(fileKey, json);
    }
}
