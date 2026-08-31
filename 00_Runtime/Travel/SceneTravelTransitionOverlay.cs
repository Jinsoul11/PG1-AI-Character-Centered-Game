using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 场景传送黑屏过场：目的地文案 + REMI 字母从左到右蓝色填充（进度=加载进度）。
/// </summary>
[DisallowMultipleComponent]
public class SceneTravelTransitionOverlay : MonoBehaviour
{
    private const float RemiFontSize = 64f;
    private static readonly Color BlueFill = new(0.28f, 0.62f, 1f);

    public static SceneTravelTransitionOverlay Instance { get; private set; }

    private CanvasGroup _canvasGroup;
    private TMP_Text _destinationLine;
    private TMP_Text _subtitleLine;
    private TMP_Text _whiteRemi;
    private TMP_Text _blueRemi;
    private RectTransform _fillMask;
    private float _remiTextWidth;

    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        Transform parent = UiManager.Instance != null ? UiManager.Instance.canvasObj.transform : null;
        var go = new GameObject(nameof(SceneTravelTransitionOverlay), typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);
        go.AddComponent<SceneTravelTransitionOverlay>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUi();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>显示过场并驱动 <paramref name="loadOp"/> 直至场景激活，然后淡出。</summary>
    public IEnumerator PlayLoadTransition(
        SceneTravelLocation destination,
        AsyncOperation loadOp,
        string subtitleOverride = null)
    {
        UiManager.Instance.canvasObj.SetActive(true);
        transform.SetAsLastSibling();

        string placeName = SceneTravelCatalog.GetLocationDisplayName(destination);
        _destinationLine.text = $"正在前往「{placeName}」";
        SetSubtitle(subtitleOverride);
        SetRemiFill(0f);
        SetVisible(true);

        while (loadOp.progress < 0.9f)
        {
            SetRemiFill(Mathf.Clamp01(loadOp.progress / 0.9f));
            yield return null;
        }

        SetRemiFill(1f);
        loadOp.allowSceneActivation = true;

        while (!loadOp.isDone)
            yield return null;

        yield return FadeOut();
        SetVisible(false);
    }

    private void SetSubtitle(string subtitleOverride)
    {
        if (_subtitleLine == null)
            return;

        string text = string.IsNullOrWhiteSpace(subtitleOverride) ? string.Empty : subtitleOverride.Trim();
        _subtitleLine.text = text;
        _subtitleLine.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private void BuildUi()
    {
        RectTransform root = gameObject.GetComponent<RectTransform>();
        if (root == null)
            root = gameObject.AddComponent<RectTransform>();

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 5000;

        gameObject.AddComponent<GraphicRaycaster>();

        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = false;

        Image bg = gameObject.AddComponent<Image>();
        bg.color = Color.black;
        bg.raycastTarget = true;

        _destinationLine = CreateLine("DestinationLine", new Vector2(0f, 48f), 36f, FontStyles.Normal);
        _subtitleLine = CreateLine("SubtitleLine", new Vector2(0f, -8f), 24f, FontStyles.Italic);
        _subtitleLine.color = new Color(0.72f, 0.72f, 0.72f);

        BuildRemiBar();
    }

    private TMP_Text CreateLine(string name, Vector2 anchoredPos, float fontSize, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 64f);
        rt.anchoredPosition = anchoredPos;

        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return tmp;
    }

    private void BuildRemiBar()
    {
        GameObject barRoot = new GameObject("RemiBar", typeof(RectTransform));
        barRoot.transform.SetParent(transform, false);

        RectTransform barRt = barRoot.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0.5f, 0f);
        barRt.anchorMax = new Vector2(0.5f, 0f);
        barRt.pivot = new Vector2(0.5f, 0f);
        barRt.anchoredPosition = new Vector2(0f, 72f);
        barRt.sizeDelta = new Vector2(400f, 80f);

        GameObject whiteGo = new GameObject("WhiteRemi", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        whiteGo.transform.SetParent(barRoot.transform, false);
        RectTransform whiteRt = whiteGo.GetComponent<RectTransform>();
        whiteRt.anchorMin = new Vector2(0.5f, 0.5f);
        whiteRt.anchorMax = new Vector2(0.5f, 0.5f);
        whiteRt.pivot = new Vector2(0.5f, 0.5f);
        whiteRt.anchoredPosition = Vector2.zero;
        whiteRt.sizeDelta = new Vector2(360f, 80f);

        _whiteRemi = whiteGo.GetComponent<TextMeshProUGUI>();
        ConfigureRemiText(_whiteRemi, Color.white);

        GameObject maskGo = new GameObject("FillMask", typeof(RectTransform), typeof(RectMask2D));
        maskGo.transform.SetParent(barRoot.transform, false);
        _fillMask = maskGo.GetComponent<RectTransform>();
        _fillMask.anchorMin = new Vector2(0.5f, 0.5f);
        _fillMask.anchorMax = new Vector2(0.5f, 0.5f);
        _fillMask.pivot = new Vector2(0f, 0.5f);
        _fillMask.anchoredPosition = Vector2.zero;
        _fillMask.sizeDelta = new Vector2(0f, 80f);

        GameObject blueGo = new GameObject("BlueRemi", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        blueGo.transform.SetParent(maskGo.transform, false);
        RectTransform blueRt = blueGo.GetComponent<RectTransform>();
        blueRt.anchorMin = new Vector2(0f, 0.5f);
        blueRt.anchorMax = new Vector2(0f, 0.5f);
        blueRt.pivot = new Vector2(0.5f, 0.5f);
        blueRt.sizeDelta = new Vector2(360f, 80f);

        _blueRemi = blueGo.GetComponent<TextMeshProUGUI>();
        ConfigureRemiText(_blueRemi, BlueFill);

        _whiteRemi.ForceMeshUpdate();
        _remiTextWidth = _whiteRemi.preferredWidth;
        _fillMask.anchoredPosition = new Vector2(-_remiTextWidth * 0.5f, 0f);
        blueRt.anchoredPosition = new Vector2(_remiTextWidth * 0.5f, 0f);
    }

    private static void ConfigureRemiText(TMP_Text tmp, Color color)
    {
        tmp.text = "REMI";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = RemiFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 8f;
        tmp.color = color;
        tmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
    }

    private void SetRemiFill(float progress)
    {
        if (_fillMask == null)
            return;

        progress = Mathf.Clamp01(progress);
        if (_remiTextWidth <= 0f && _whiteRemi != null)
        {
            _whiteRemi.ForceMeshUpdate();
            _remiTextWidth = _whiteRemi.preferredWidth;
            _fillMask.anchoredPosition = new Vector2(-_remiTextWidth * 0.5f, 0f);
        }

        _fillMask.sizeDelta = new Vector2(_remiTextWidth * progress, 80f);
    }

    private void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (_canvasGroup != null)
            _canvasGroup.alpha = visible ? 1f : 0f;
    }

    private IEnumerator FadeOut()
    {
        if (_canvasGroup == null)
            yield break;

        const float duration = 0.25f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = 1f - Mathf.Clamp01(t / duration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }
}
