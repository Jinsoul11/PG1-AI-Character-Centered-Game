using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 场景传送选择：滚轮切换目的地，F 确认，B 取消（Esc 保留给手机）。
/// 打开期间锁定移动/视角；关闭时立即销毁面板（不用淡出，避免与 BasePanel.Update 冲突导致残留叠层）。
/// </summary>
public class SceneTravelPanel : BasePanel
{
    public static bool IsOpen { get; private set; }

    [Header("输入")]
    [SerializeField] private KeyCode confirmKey = KeyCode.F;
    [SerializeField] private KeyCode cancelKey = KeyCode.B;
    [SerializeField] private float scrollDeadZone = 0.01f;

    [Header("UI（可空，运行时自动生成）")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text optionAText;
    [SerializeField] private TMP_Text optionBText;
    [SerializeField] private TMP_Text hintText;

    private SceneTravelLocation _currentLocation;
    private SceneTravelLocation _optionA;
    private SceneTravelLocation _optionB;
    private int _selectedIndex;
    private SceneTravelDoorTrigger _ownerDoor;
    private PlayerController _player;
    private bool _uiBuilt;
    private bool _leaveOnly;

    public override void Init()
    {
        EnsureUiBuilt();
        RefreshOptionVisuals();
    }

    protected override void Awake()
    {
        base.Awake();
        EnsureUiBuilt();
    }

    public static void OpenFromDoor(SceneTravelLocation from, SceneTravelDoorTrigger owner)
    {
        EnsureConsistentOpenState();
        DestroyOrphanPanels();

        if (IsOpen)
            return;
        if (IsBlockedByOtherUi())
            return;
        if (IsCinematicSequenceRunning())
            return;

        // 先锁移动，避免面板弹出前一帧仍在走动 / 重复踩触发器。
        SceneTravelService.EnsureExists();
        PlayerController player = SceneTravelService.GetPlayerController();
        player?.SetMoveLock(true);
        player?.SetLookLock(true);

        UiManager.EnsureCanvasActive();
        SceneTravelPanel panel = UiManager.Instance.ShowPanel<SceneTravelPanel>();
        if (panel == null)
        {
            Debug.LogError("[SceneTravelPanel] 无法打开传送面板（预制体或脚本缺失）。");
            player?.SetMoveLock(false);
            player?.SetLookLock(false);
            return;
        }

        panel.Configure(from, owner, player);
    }

    public static void TryCloseFromDoor(SceneTravelDoorTrigger owner)
    {
        if (!IsOpen)
            return;

        SceneTravelPanel panel = UiManager.Instance != null
            ? UiManager.Instance.GetPanel<SceneTravelPanel>()
            : null;
        if (panel != null && panel._ownerDoor == owner)
            panel.ClosePanel();
    }

    /// <summary>面板已关但玩家仍被锁时兜底解锁（如异常销毁面板）。</summary>
    public static void EnsurePlayerUnlockedIfClosed()
    {
        if (IsOpen)
            return;
        if (IsCinematicSequenceRunning())
            return;

        SceneTravelService.EnsureExists();
        PlayerController player = SceneTravelService.GetPlayerController();
        if (player == null)
            return;

        if (player.IsMoveLocked || player.IsLookLocked)
        {
            player.SetMoveLock(false);
            player.SetLookLock(false);
        }
    }

    /// <summary>静态 IsOpen 与面板实例不一致时修正（避免重复触发被卡住）。</summary>
    public static void EnsureConsistentOpenState()
    {
        SceneTravelPanel panel = UiManager.Instance != null
            ? UiManager.Instance.GetPanel<SceneTravelPanel>()
            : null;

        bool visible = panel != null && panel.gameObject.activeInHierarchy;
        if (visible)
        {
            IsOpen = true;
            return;
        }

        if (IsOpen)
        {
            IsOpen = false;
            EnsurePlayerUnlockedIfClosed();
        }
    }

    /// <summary>清掉已脱离 UiManager 字典但仍挂在 Canvas 上的残留实例（叠层根因）。</summary>
    private static void DestroyOrphanPanels()
    {
        SceneTravelPanel tracked = UiManager.Instance != null
            ? UiManager.Instance.GetPanel<SceneTravelPanel>()
            : null;

#if UNITY_2023_1_OR_NEWER
        SceneTravelPanel[] all = Object.FindObjectsByType<SceneTravelPanel>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
#else
        SceneTravelPanel[] all = Object.FindObjectsOfType<SceneTravelPanel>(true);
#endif
        if (all == null || all.Length == 0)
            return;

        foreach (SceneTravelPanel panel in all)
        {
            if (panel == null || panel == tracked)
                continue;
            Object.Destroy(panel.gameObject);
        }
    }

    public static bool IsBlockedByOtherUi()
    {
        if (DialoguePanel.IsDialogueOpen())
            return true;
        if (PhoneAppPanel.IsOpen)
            return true;
        if (IsStoryPanelVisible())
            return true;
        return false;
    }

    private static bool IsStoryPanelVisible()
    {
        if (UiManager.Instance == null)
            return false;
        StoryPanel panel = UiManager.Instance.GetPanel<StoryPanel>();
        return panel != null && panel.gameObject.activeInHierarchy;
    }

    private static bool IsCinematicSequenceRunning()
    {
        if (RemiDemoSpineDirector.Instance != null &&
            RemiDemoSpineDirector.Instance.IsSpineSequenceRunning)
            return true;
        if (RemiDemoMemoryRecapEndingFlow.Instance != null &&
            RemiDemoMemoryRecapEndingFlow.Instance.IsSequenceRunning)
            return true;
        return false;
    }

    public override void ShowMe()
    {
        EnsureUiBuilt();
        base.ShowMe();

        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group != null)
            group.alpha = 1f;
    }

    private void Configure(SceneTravelLocation from, SceneTravelDoorTrigger owner, PlayerController player)
    {
        _currentLocation = from;
        _ownerDoor = owner;
        SceneTravelCatalog.GetDestinationsFrom(from, out _optionA, out _optionB);
        _selectedIndex = 0;
        _leaveOnly = IsApartmentLeaveEndingFlow(from);

        ShowMe();
        IsOpen = true;
        _player = player != null ? player : SceneTravelService.GetPlayerController();
        ApplyPlayerLock(true);

        if (titleText != null)
            titleText.text = GetTitleForCurrentFlow(_leaveOnly);
        if (hintText != null)
            hintText.text = GetHintForCurrentFlow(_leaveOnly);

        ApplyDay2LibraryDefaultSelection();
        RefreshOptionVisuals();
    }

    private void ApplyDay2LibraryDefaultSelection()
    {
        if (_currentLocation != SceneTravelLocation.Classroom)
            return;

        RemiDemoSpineDirector.EnsureExists();
        if (RemiDemoSpineDirector.Instance == null ||
            !RemiDemoSpineDirector.Instance.IsAwaitingDay2LibraryVisit())
            return;

        _selectedIndex = 0;
    }

    private static bool IsApartmentLeaveEndingFlow(SceneTravelLocation from)
    {
        if (from != SceneTravelLocation.Apartment)
            return false;
        RemiDemoSpineDirector.EnsureExists();
        return RemiDemoSpineDirector.Instance != null &&
               RemiDemoSpineDirector.Instance.IsApartmentLeaveEndingReady();
    }

    private static string GetTitleForCurrentFlow(bool leaveOnly)
    {
        if (leaveOnly)
            return "离开公寓";

        RemiDemoSpineDirector.EnsureExists();
        if (RemiDemoSpineDirector.Instance != null &&
            RemiDemoSpineDirector.Instance.IsAwaitingDay2LibraryVisit() &&
            SceneTravelCatalog.ResolveFromActiveScene() == SceneTravelLocation.Classroom)
            return "Remi 在图书馆等你";

        return "选择目的地";
    }

    private static string GetHintForCurrentFlow(bool leaveOnly)
    {
        if (leaveOnly)
            return "F 确认离开 · B 取消";

        RemiDemoSpineDirector.EnsureExists();
        if (RemiDemoSpineDirector.Instance != null &&
            RemiDemoSpineDirector.Instance.IsAwaitingDay2LibraryVisit() &&
            SceneTravelCatalog.ResolveFromActiveScene() == SceneTravelLocation.Classroom)
            return "滚轮选择 · F 确认前往图书馆 · B 取消";

        return "滚轮选择 · F 确认 · B 取消 · Esc 打开手机";
    }

    private void OnDisable()
    {
        if (IsOpen)
            ForceClose(unlockPlayer: true);
    }

    private void Update()
    {
        SyncOpenStateFromVisibility();

        if (!IsOpen)
            return;

        // 面板打开期间持续禁止移动（防止其他系统误解锁）。
        ApplyPlayerLock(true);

        if (!_leaveOnly)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > scrollDeadZone)
                ChangeSelection(-1);
            else if (scroll < -scrollDeadZone)
                ChangeSelection(1);
        }

        if (Input.GetKeyDown(confirmKey))
            ConfirmSelection();
        else if (Input.GetKeyDown(cancelKey))
            ClosePanel();
    }

    /// <summary>场景切换后面板仍可见但 IsOpen 被清掉时，恢复输入响应。</summary>
    private void SyncOpenStateFromVisibility()
    {
        if (IsOpen)
            return;

        if (!gameObject.activeInHierarchy)
            return;

        CanvasGroup group = GetComponent<CanvasGroup>();
        bool visiblyPresent = group == null || group.alpha > 0.05f;
        if (isShow || visiblyPresent)
            IsOpen = true;
    }

    private void ChangeSelection(int delta)
    {
        _selectedIndex = (_selectedIndex + delta + 2) % 2;
        RefreshOptionVisuals();
    }

    private void ConfirmSelection()
    {
        RemiDemoSpineDirector.EnsureExists();
        if (_leaveOnly &&
            RemiDemoSpineDirector.Instance != null &&
            RemiDemoSpineDirector.Instance.IsApartmentLeaveEndingReady())
        {
            ForceClose(unlockPlayer: false);
            if (UiManager.Instance != null)
                UiManager.Instance.HidePanel<SceneTravelPanel>(isFade: false);
            RemiDemoSpineDirector.Instance.TryPlayDay3ApartmentEnding();
            return;
        }

        SceneTravelLocation destination = _selectedIndex == 0 ? _optionA : _optionB;

        if (RemiDemoSpineDirector.Instance != null &&
            RemiDemoSpineDirector.Instance.IsAwaitingDay2LibraryVisit() &&
            _currentLocation == SceneTravelLocation.Classroom &&
            destination == SceneTravelLocation.Library)
        {
            SceneTravelService.SetPendingTravelSubtitle("Remi 在图书馆等你。");
        }
        else if (RemiDemoSpineDirector.Instance != null &&
                 RemiDemoSpineDirector.Instance.CanPlayDay2Ending() &&
                 _currentLocation == SceneTravelLocation.Library)
        {
            // 实际目的地由日切过场强制为教室；副标题仅作提示。
            SceneTravelService.SetPendingTravelSubtitle("第二天结束，返回教室。");
        }

        // 确认后交给 Travel 流程接管锁；此处不要解锁，避免空隙可走动。
        ForceClose(unlockPlayer: false);
        if (UiManager.Instance != null)
            UiManager.Instance.HidePanel<SceneTravelPanel>(isFade: false);

        SceneTravelService.EnsureExists();
        SceneTravelService.Instance?.TravelTo(destination);
    }

    private void ClosePanel()
    {
        ForceClose(unlockPlayer: true);
        if (UiManager.Instance != null)
            UiManager.Instance.HidePanel<SceneTravelPanel>(isFade: false);
    }

    private void ForceClose(bool unlockPlayer)
    {
        IsOpen = false;
        isShow = false;
        _ownerDoor = null;

        if (unlockPlayer)
            ApplyPlayerLock(false);

        _player = null;
    }

    private void ApplyPlayerLock(bool locked)
    {
        PlayerController player = _player != null ? _player : SceneTravelService.GetPlayerController();
        if (player == null)
            return;

        _player = player;
        player.SetMoveLock(locked);
        player.SetLookLock(locked);
    }

    private void RefreshOptionVisuals()
    {
        if (_leaveOnly)
        {
            SetPlainOptionLine(optionAText, "离开", selected: true);
            if (optionBText != null)
            {
                optionBText.text = string.Empty;
                optionBText.gameObject.SetActive(false);
            }

            return;
        }

        if (optionBText != null)
            optionBText.gameObject.SetActive(true);

        SetOptionLine(optionAText, _optionA, _selectedIndex == 0);
        SetOptionLine(optionBText, _optionB, _selectedIndex == 1);
    }

    private static void SetPlainOptionLine(TMP_Text text, string label, bool selected)
    {
        if (text == null)
            return;

        text.gameObject.SetActive(true);
        string prefix = selected ? "> " : "  ";
        text.text = prefix + label;
        text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        text.color = selected ? new Color(1f, 0.95f, 0.75f) : new Color(0.92f, 0.92f, 0.92f);
    }

    private static void SetOptionLine(TMP_Text text, SceneTravelLocation location, bool selected)
    {
        if (text == null)
            return;

        string prefix = selected ? "> " : "  ";
        text.text = prefix + SceneTravelCatalog.GetDestinationLabel(location);
        text.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        text.color = selected ? new Color(1f, 0.95f, 0.75f) : new Color(0.92f, 0.92f, 0.92f);
    }

    private void EnsureUiBuilt()
    {
        if (_uiBuilt)
            return;
        _uiBuilt = true;

        RectTransform root = transform as RectTransform;
        if (root == null)
            return;

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image dim = GetComponent<Image>();
        if (dim == null)
        {
            dim = gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.45f);
            dim.raycastTarget = true;
        }

        if (titleText != null && optionAText != null && optionBText != null && hintText != null)
            return;

        // 避免重复生成 Box（例如 Init/Awake 竞态把 _uiBuilt 绕开后再次调用）。
        Transform existingBox = transform.Find("Box");
        if (existingBox != null)
        {
            if (titleText == null)
                titleText = existingBox.Find("Title")?.GetComponent<TMP_Text>();
            if (optionAText == null)
                optionAText = existingBox.Find("OptionA")?.GetComponent<TMP_Text>();
            if (optionBText == null)
                optionBText = existingBox.Find("OptionB")?.GetComponent<TMP_Text>();
            if (hintText == null)
                hintText = existingBox.Find("Hint")?.GetComponent<TMP_Text>();
            if (titleText != null && optionAText != null && optionBText != null && hintText != null)
                return;
        }

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(transform, false);
        RectTransform boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(520f, 280f);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.92f);

        titleText = CreateText(box.transform, "Title", new Vector2(0f, 90f), 34f, FontStyles.Bold);
        optionAText = CreateText(box.transform, "OptionA", new Vector2(0f, 20f), 30f, FontStyles.Normal);
        optionBText = CreateText(box.transform, "OptionB", new Vector2(0f, -30f), 30f, FontStyles.Normal);
        hintText = CreateText(box.transform, "Hint", new Vector2(0f, -95f), 22f, FontStyles.Italic);
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        float fontSize,
        FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(460f, 56f);
        rt.anchoredPosition = anchoredPos;

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }
}
