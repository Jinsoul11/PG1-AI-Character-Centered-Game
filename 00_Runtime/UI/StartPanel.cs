using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏开始界面：开始 → Travel 黑屏过场 → 加载教室（或配置的首场景）。
/// 挂在 <c>StartPanel</c> 上，按钮名默认为 Start / Setting / Quit；可选 Load。
/// </summary>
public class StartPanel : BasePanel
{
    [Header("按钮（可留空，按子物体名自动查找）")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("新游戏")]
    [SerializeField] private bool resetProgressOnNewGame = true;
    [SerializeField] private SceneTravelLocation startDestination = SceneTravelLocation.Classroom;

    [Header("读档")]
    [Tooltip("有多个日起点档时优先载入更大的一天（3→2）。")]
    [SerializeField] private bool loadLatestDaySlot = true;
    [Tooltip("loadLatestDaySlot=false 时强制载入的天数（2 或 3）。")]
    [SerializeField] private int fixedLoadStoryDay = 2;

    [Header("点击开始后 · Travel 过场副标题")]
    [TextArea(2, 4)]
    [SerializeField] private string travelSubtitle = "转学第一天。你收拾好东西，朝教室走去。";

    [TextArea(1, 2)]
    [SerializeField] private string settingsNotReadyMessage = "设置功能尚未开放。";

    [TextArea(1, 2)]
    [SerializeField] private string noSaveMessage = "没有可载入的存档（需先到达第 2 / 第 3 天起点）。";

    private bool _sequenceRunning;

    protected override void Awake()
    {
        base.Awake();
        // 主菜单常驻显示：BasePanel 默认 isShow=false 会在 Update 里把 alpha 渐变为 0
        isShow = true;
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null)
            cg.alpha = 1f;
    }

    /// <summary>主菜单不使用 BasePanel 的渐入/渐出。</summary>
    private void Update() { }

    public override void Init()
    {
        ResolveButtons();
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
            startButton.onClick.AddListener(OnStartClicked);
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(OnLoadClicked);
            loadButton.onClick.AddListener(OnLoadClicked);
            RefreshLoadButtonInteractable();
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitClicked);
            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    private void OnEnable()
    {
        RefreshLoadButtonInteractable();
    }

    private void ResolveButtons()
    {
        if (startButton == null)
            startButton = transform.Find("Start")?.GetComponent<Button>();
        if (loadButton == null)
            loadButton = transform.Find("Load")?.GetComponent<Button>();
        if (settingsButton == null)
            settingsButton = transform.Find("Setting")?.GetComponent<Button>();
        if (quitButton == null)
            quitButton = transform.Find("Quit")?.GetComponent<Button>();
    }

    private void RefreshLoadButtonInteractable()
    {
        if (loadButton == null)
            return;
        loadButton.interactable = !_sequenceRunning && RemiDemoDaySaveService.HasAnySlot();
    }

    private void OnStartClicked()
    {
        if (_sequenceRunning)
            return;

        _sequenceRunning = true;
        SetButtonsInteractable(false);

        if (resetProgressOnNewGame)
            ResetDemoProgress();

        SceneTravelService.EnsureExists();
        SceneTravelService.SetPendingSpawnPointName(
            SceneTravelCatalog.GetNewGameSpawnPointName(startDestination));
        SceneTravelService.SetPendingTravelSubtitle(travelSubtitle);
        SceneTravelService.Instance.TravelTo(startDestination);
    }

    private void OnLoadClicked()
    {
        if (_sequenceRunning)
            return;

        int day = loadLatestDaySlot
            ? RemiDemoDaySaveService.ResolveLatestSlotDay()
            : Mathf.Clamp(fixedLoadStoryDay, 2, 3);

        if (day <= 0 || !RemiDemoDaySaveService.HasSlot(day))
        {
            if (!string.IsNullOrWhiteSpace(noSaveMessage))
                Debug.Log(noSaveMessage.Trim());
            RefreshLoadButtonInteractable();
            return;
        }

        _sequenceRunning = true;
        SetButtonsInteractable(false);

        // 读档：不清 RemiDialogueArchive
        if (!RemiDemoDaySaveService.TryLoadDayStart(day, out string error))
        {
            _sequenceRunning = false;
            SetButtonsInteractable(true);
            RefreshLoadButtonInteractable();
            Debug.LogWarning($"[StartPanel] 读档失败: {error}");
        }
    }

    private void OnSettingsClicked()
    {
        if (!string.IsNullOrWhiteSpace(settingsNotReadyMessage))
            Debug.Log(settingsNotReadyMessage.Trim());
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetButtonsInteractable(bool on)
    {
        if (startButton != null) startButton.interactable = on;
        if (loadButton != null) loadButton.interactable = on && RemiDemoDaySaveService.HasAnySlot();
        if (settingsButton != null) settingsButton.interactable = on;
        if (quitButton != null) quitButton.interactable = on;
    }

    private static void ResetDemoProgress()
    {
        PhoneAppAccess.ResetForNewGame();
        PlayerPrefs.DeleteKey("RemiDemoSpine_Beat");
        PlayerPrefs.DeleteKey("RemiBookQuest_State");
        PlayerPrefs.DeleteKey("RemiBookQuest_HasBook");
        PlayerPrefs.DeleteKey("RemiBookQuest_Entry");
        PlayerPrefs.DeleteKey("RemiStory_LibraryDay2CoPresence");
        PlayerPrefs.DeleteKey("RemiStory_ApartmentDay3CoPresence");
        PlayerPrefs.DeleteKey(RemiLibraryDay2CoPresenceFlow.PrefsKeyState);
        PlayerPrefs.DeleteKey(StoryDirector.PrefsClassroomOpeningPlayed);
        RemiDemoDay2ClassroomGuide.ResetProgressFlags();
        RemiDemoSpineStoryChips.ResetProgress();
        RemiPhoneSendSystem.ClearAll();
        PlayerPrefs.DeleteKey("RemiRhythm_StoryStarted");
        PlayerPrefs.DeleteKey("RemiRhythm_Delegations");
        PlayerPrefs.DeleteKey("RemiRhythm_BookDone");
        PlayerPrefs.DeleteKey("RemiRhythm_PlayedBeats");
        PlayerPrefs.DeleteKey("RemiRhythm_DepthStage");
        PlayerPrefs.DeleteKey("RemiRhythm_StoryAnchors");
        PlayerPrefs.DeleteKey("RemiWorldTime");
        PlayerPrefs.DeleteKey("RemiDayBlock_Slot");
        PlayerPrefs.DeleteKey("RemiDayBlock_Kind");
        PlayerPrefs.DeleteKey("RemiDayBlock_InAnchor");
        PlayerPrefs.DeleteKey(RemiSharedExperienceMemory.PrefsStoreKey);
        PlayerPrefs.DeleteKey(RemiDemoEndingPayloadBuilder.PrefsPayloadKey);
        PlayerPrefs.DeleteKey(RemiDemoRunTelemetry.PrefsSnapshotKey);
        PlayerPrefs.DeleteKey(RemiChatFragmentMemory.PrefsStoreKey);
        RemiChatFragmentMemory.ResetProgress();
        RemiDialogueArchive.ResetProgress();
        RemiMemoryCuratorStore.ResetProgress();
        RemiFragmentUnitStore.ResetProgress();
        RemiFragmentMemory.ResetProgress();
        RemiDemoDaySaveService.ClearAllSlots();
        PlayerPrefs.DeleteKey("RemiImpressionStore");
        PlayerPrefs.DeleteKey("RemiResidueStore");
        JsonMgr.Instance.DeleteData(PhoneAppPanel.SaveKey);
        JsonMgr.Instance.DeleteData(RemiDialogueArchive.JsonSaveKey);
        JsonMgr.Instance.DeleteData(RemiMemoryCuratorStore.JsonSaveKey);
        JsonMgr.Instance.DeleteData(RemiFragmentUnitStore.JsonSaveKey);
        JsonMgr.Instance.DeleteData(RemiFragmentMemory.JsonSaveKey);
        JsonMgr.Instance.DeleteData("Conversation1");
        JsonMgr.Instance.DeleteData(DeepSeekDialogueManager.MessageHistorySaveKey);
        DeepSeekDialogueManager.Instance?.ClearMessageHistory();
        UiManager.Instance?.GetPanel<ChatHistoryPanel>()?.ClearChatHistory();
        PlayerPrefs.Save();

        RemiDemoRunTelemetry.EnsureExists();
        RemiDemoRunTelemetry.Instance?.ClearSession();
    }
}
