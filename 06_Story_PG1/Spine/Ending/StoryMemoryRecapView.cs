using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Demo Ending 中间段：全屏背景图 + 底部 Remi 自述字幕（SendSystem onRevealText 驱动）。
/// </summary>
public static class StoryMemoryRecapView
{
    public class Page
    {
        public Sprite Illustration;
        public Color PlaceholderColor = new Color(0.25f, 0.27f, 0.32f, 1f);
        public string WhenLabel;
        public int PageIndex;
        public int PageCount;
    }

    public class SessionOptions
    {
        public int SortingOrder = 520;
    }

    private class Session
    {
        public GameObject Root;
        public Image BackgroundImage;
        public Image IllustrationImage;
        public TMP_Text WhenLabelText;
        public TMP_Text NarrationText;
        public TMP_Text PageCounterText;
        public Button NextButton;
        public bool AdvanceRequested;
    }

    private static Session _activeSession;

    public static bool IsActive => _activeSession != null;

    public static void Show(Page page, SessionOptions options = null)
    {
        options ??= new SessionOptions();
        Hide();

        _activeSession = CreateSession(options);
        ApplyPage(_activeSession, page);
    }

    public static void Hide()
    {
        if (_activeSession?.Root != null)
            UnityEngine.Object.Destroy(_activeSession.Root);
        _activeSession = null;
    }

    public static System.Action<string> CreateRevealCallback()
    {
        return text =>
        {
            if (_activeSession?.NarrationText == null)
                return;
            _activeSession.NarrationText.text = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        };
    }

    public static IEnumerator WaitForAdvance(bool allowSkip = true)
    {
        if (_activeSession == null)
            yield break;

        _activeSession.AdvanceRequested = false;
        if (_activeSession.NextButton != null)
            _activeSession.NextButton.gameObject.SetActive(true);

        while (!_activeSession.AdvanceRequested)
            yield return null;
    }

    private static void ApplyPage(Session session, Page page)
    {
        if (session == null || page == null)
            return;

        session.AdvanceRequested = false;

        if (session.WhenLabelText != null)
            session.WhenLabelText.text = string.IsNullOrWhiteSpace(page.WhenLabel) ? string.Empty : page.WhenLabel.Trim();

        if (session.PageCounterText != null)
        {
            session.PageCounterText.text = page.PageCount > 0
                ? $"{Mathf.Max(1, page.PageIndex)} / {page.PageCount}"
                : string.Empty;
        }

        if (session.NarrationText != null)
            session.NarrationText.text = string.Empty;

        if (session.IllustrationImage != null)
        {
            if (page.Illustration != null)
            {
                session.IllustrationImage.sprite = page.Illustration;
                session.IllustrationImage.color = Color.white;
                session.IllustrationImage.preserveAspect = true;
            }
            else
            {
                session.IllustrationImage.sprite = null;
                session.IllustrationImage.color = page.PlaceholderColor;
            }
        }

        if (session.BackgroundImage != null)
            session.BackgroundImage.color = Color.black;
    }

    private static Session CreateSession(SessionOptions options)
    {
        var session = new Session();
        session.Root = new GameObject(nameof(StoryMemoryRecapView) + "_Canvas");

        var canvas = session.Root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = options.SortingOrder;
        session.Root.AddComponent<GraphicRaycaster>();

        var scaler = session.Root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        session.BackgroundImage = CreateStretchImage(session.Root.transform, "Background", Color.black);

        var illustrationGo = new GameObject("Illustration", typeof(RectTransform));
        illustrationGo.transform.SetParent(session.Root.transform, false);
        var illustrationRt = illustrationGo.GetComponent<RectTransform>();
        illustrationRt.anchorMin = new Vector2(0.08f, 0.22f);
        illustrationRt.anchorMax = new Vector2(0.92f, 0.88f);
        illustrationRt.offsetMin = Vector2.zero;
        illustrationRt.offsetMax = Vector2.zero;
        session.IllustrationImage = illustrationGo.AddComponent<Image>();
        session.IllustrationImage.raycastTarget = false;

        session.WhenLabelText = CreateText(session.Root.transform, "WhenLabel", 22f, TextAlignmentOptions.MidlineLeft,
            new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.95f));

        session.PageCounterText = CreateText(session.Root.transform, "PageCounter", 20f, TextAlignmentOptions.MidlineRight,
            new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.95f));

        var narrationPanel = CreateStretchImage(session.Root.transform, "NarrationPanel",
            new Color(0f, 0f, 0f, 0.72f));
        var narrationPanelRt = narrationPanel.GetComponent<RectTransform>();
        narrationPanelRt.anchorMin = new Vector2(0f, 0f);
        narrationPanelRt.anchorMax = new Vector2(1f, 0f);
        narrationPanelRt.pivot = new Vector2(0.5f, 0f);
        narrationPanelRt.sizeDelta = new Vector2(0f, 220f);
        narrationPanelRt.anchoredPosition = Vector2.zero;

        session.NarrationText = CreateText(narrationPanel.transform, "Narration", 24f, TextAlignmentOptions.TopLeft,
            new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.82f));
        session.NarrationText.color = new Color(0.95f, 0.95f, 0.96f, 1f);

        var speakerLabel = CreateText(narrationPanel.transform, "Speaker", 20f, TextAlignmentOptions.TopLeft,
            new Vector2(0.04f, 0.82f), new Vector2(0.3f, 0.96f));
        speakerLabel.text = "Remi";
        speakerLabel.fontStyle = FontStyles.Bold;

        session.NextButton = CreateFooterButton(session.Root.transform, "下一段", new Vector2(-48f, 236f),
            () => session.AdvanceRequested = true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return session;
    }

    private static Image CreateStretchImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        StretchFill(go.GetComponent<RectTransform>());
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private static Button CreateFooterButton(Transform parent, string label, Vector2 anchoredPos, Action onClick)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.sizeDelta = new Vector2(120f, 44f);
        rt.anchoredPosition = anchoredPos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.22f, 0.92f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        StretchFill(textGo.GetComponent<RectTransform>());
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        return btn;
    }

    private static void StretchFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
