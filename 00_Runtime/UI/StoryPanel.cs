using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开场剧情面板：显示说话人 + 台词，提供 下一句/跳过/自动播放。
/// Ending 长文时支持滚轮上下滚动（仅超出可视区域时）。
/// 需要在 Resources/UI/ 下创建同名预制体 `StoryPanel` 并把字段拖引用。
/// 黑屏旁白见 <see cref="StoryBlackScreenInterlude"/>。
/// </summary>
public class StoryPanel : BasePanel
{
    [Header("UI引用")]
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button skipButton;
    [Tooltip("可选：开启后由 StoryDirector 在语音结束或按字数延迟后自动「下一句」。")]
    [SerializeField] private Toggle autoPlayToggle;

    [Header("长文滚动")]
    [SerializeField] private float bodyScrollSensitivity = 45f;

    private StoryDirector _director;
    private bool _inputWired;
    private Action _manualAdvance;
    private bool _nextInteractable = true;

    private bool _bodyScrollReady;
    private ScrollRect _bodyScroll;
    private RectTransform _bodyViewport;
    private RectTransform _bodyContent;
    private float _bodyViewportHeight;

    public void Bind(StoryDirector director)
    {
        _director = director;
        _manualAdvance = null;
        WireInputHandlers();
        SyncAutoPlayToggleFromDirector();
    }

    /// <summary>Ending 等脚本驱动：下一句只触发回调，不走 StoryDirector。</summary>
    public void BeginManualSession()
    {
        _director = null;
        _manualAdvance = null;
        _nextInteractable = true;
        WireInputHandlers();
        if (skipButton != null)
            skipButton.gameObject.SetActive(false);
        if (autoPlayToggle != null)
            autoPlayToggle.gameObject.SetActive(false);
        SetNextInteractable(true);
        EnsureBodyScroll();
    }

    public void BindManualAdvance(Action onNext) => _manualAdvance = onNext;

    public void EndManualSession()
    {
        _manualAdvance = null;
        _director = null;
        if (skipButton != null)
            skipButton.gameObject.SetActive(true);
        if (autoPlayToggle != null)
            autoPlayToggle.gameObject.SetActive(true);
        SetNextInteractable(true);
        ResetBodyScrollToTop();
    }

    public override void Init()
    {
        WireInputHandlers();
        SyncAutoPlayToggleFromDirector();
        EnsureBodyScroll();
    }

    public override void ShowMe()
    {
        UiManager.EnsureEventSystem();
        base.ShowMe();

        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    private void WireInputHandlers()
    {
        if (_inputWired)
            return;
        _inputWired = true;

        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextClicked);

        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipClicked);

        if (autoPlayToggle != null)
            autoPlayToggle.onValueChanged.AddListener(OnAutoPlayToggleChanged);
    }

    private void OnNextClicked()
    {
        if (!_nextInteractable)
            return;
        if (_manualAdvance != null)
        {
            _manualAdvance.Invoke();
            return;
        }

        _director?.Next();
    }

    private void OnSkipClicked() => _director?.Skip();

    private void OnAutoPlayToggleChanged(bool isOn) => _director?.SetAutoPlay(isOn);

    private void SyncAutoPlayToggleFromDirector()
    {
        if (autoPlayToggle == null || _director == null)
            return;

        autoPlayToggle.SetIsOnWithoutNotify(_director.AutoPlayEnabled);
    }

    /// <summary>剧情结束时关闭开关显示，不触发 Director 回调。</summary>
    public void SetAutoPlayToggleVisual(bool isOn)
    {
        if (autoPlayToggle != null)
            autoPlayToggle.SetIsOnWithoutNotify(isOn);
    }

    public void SetLine(string speakerName, string text)
    {
        if (speakerText != null) speakerText.text = speakerName ?? string.Empty;
        if (bodyText != null) bodyText.text = text ?? string.Empty;
        RefreshBodyScrollLayout();
    }

    public void SetNextInteractable(bool interactable)
    {
        _nextInteractable = interactable;
        if (nextButton != null)
            nextButton.interactable = interactable;
    }

    private void Update()
    {
        // 必须跑淡出：否则 HidePanel(isFade:true) 会卡在可见状态且永不 Destroy
        TickCanvasGroupFade();

        HandleBodyMouseWheelScroll();

        if (_manualAdvance == null || !_nextInteractable)
            return;
        if (!gameObject.activeInHierarchy)
            return;
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            _manualAdvance.Invoke();
    }

    /// <summary>
    /// 把 bodyText 包进带 Mask 的 ScrollRect：文案超出可视高度时可用滚轮上下看。
    /// </summary>
    private void EnsureBodyScroll()
    {
        if (_bodyScrollReady || bodyText == null)
            return;

        _bodyContent = bodyText.rectTransform;
        Transform originalParent = _bodyContent.parent;
        if (originalParent == null)
            return;

        // 保留原 TextContent 的屏幕占位作为视口
        Vector2 size = _bodyContent.sizeDelta;
        Vector2 anchored = _bodyContent.anchoredPosition;
        Vector2 anchorMin = _bodyContent.anchorMin;
        Vector2 anchorMax = _bodyContent.anchorMax;
        Vector2 pivot = _bodyContent.pivot;
        int sibling = _bodyContent.GetSiblingIndex();

        GameObject viewportGo = new GameObject("BodyViewport", typeof(RectTransform));
        _bodyViewport = viewportGo.GetComponent<RectTransform>();
        _bodyViewport.SetParent(originalParent, false);
        _bodyViewport.SetSiblingIndex(sibling);
        _bodyViewport.anchorMin = anchorMin;
        _bodyViewport.anchorMax = anchorMax;
        _bodyViewport.pivot = pivot;
        _bodyViewport.sizeDelta = size;
        _bodyViewport.anchoredPosition = anchored;
        viewportGo.AddComponent<RectMask2D>();

        _bodyContent.SetParent(_bodyViewport, false);
        _bodyContent.anchorMin = new Vector2(0f, 1f);
        _bodyContent.anchorMax = new Vector2(1f, 1f);
        _bodyContent.pivot = new Vector2(0.5f, 1f);
        _bodyContent.anchoredPosition = Vector2.zero;
        _bodyContent.sizeDelta = new Vector2(0f, size.y);

        _bodyScroll = viewportGo.AddComponent<ScrollRect>();
        _bodyScroll.content = _bodyContent;
        _bodyScroll.viewport = _bodyViewport;
        _bodyScroll.horizontal = false;
        _bodyScroll.vertical = true;
        _bodyScroll.movementType = ScrollRect.MovementType.Clamped;
        _bodyScroll.scrollSensitivity = bodyScrollSensitivity;
        _bodyScroll.inertia = false;
        _bodyScroll.verticalScrollbar = null;

        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.enableWordWrapping = true;
        bodyText.verticalAlignment = VerticalAlignmentOptions.Top;

        _bodyViewportHeight = Mathf.Max(1f, size.y);
        _bodyScrollReady = true;
        RefreshBodyScrollLayout();
    }

    private void RefreshBodyScrollLayout()
    {
        EnsureBodyScroll();
        if (!_bodyScrollReady || bodyText == null || _bodyContent == null || _bodyScroll == null)
            return;

        bodyText.ForceMeshUpdate(true);
        float preferred = Mathf.Max(_bodyViewportHeight, bodyText.preferredHeight + 4f);
        _bodyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferred);

        bool canScroll = preferred > _bodyViewportHeight + 1f;
        _bodyScroll.enabled = canScroll;
        if (!canScroll || _bodyScroll.verticalNormalizedPosition > 0.99f)
            _bodyScroll.verticalNormalizedPosition = 1f;

        Canvas.ForceUpdateCanvases();
    }

    private void ResetBodyScrollToTop()
    {
        if (_bodyScroll != null)
            _bodyScroll.verticalNormalizedPosition = 1f;
    }

    private void HandleBodyMouseWheelScroll()
    {
        if (!_bodyScrollReady || _bodyScroll == null || !_bodyScroll.enabled)
            return;
        if (!gameObject.activeInHierarchy)
            return;

        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) < 0.01f)
            return;

        // ScrollRect 在指针不在其上时可能收不到滚轮；面板打开时直接吃滚轮
        float delta = wheel * bodyScrollSensitivity * 0.01f;
        _bodyScroll.verticalNormalizedPosition = Mathf.Clamp01(
            _bodyScroll.verticalNormalizedPosition + delta);
    }
}
