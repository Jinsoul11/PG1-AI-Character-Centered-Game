using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 黑屏文字过场：全屏黑底 + 居中旁白，点「下一句」或自动播放推进。
/// 供教室开场、Day1 图书馆一瞥、Day3 作品展尾声等复用。
/// </summary>
public static class StoryBlackScreenInterlude
{
    public class PlayOptions
    {
        public bool AllowSkip = true;
        public bool AutoPlay = false;
        public float AutoPlayMinDelay = 1.8f;
        public float AutoPlaySecondsPerChar = 0.04f;
        public float AutoPlayMaxDelay = 12f;
        public int SortingOrder = 550;
    }

    private class Session
    {
        public GameObject Root;
        public TMP_Text BodyText;
        public Button NextButton;
        public Button SkipButton;
        public bool AdvanceRequested;
        public bool SkipRequested;
    }

    /// <summary>逐句播放黑屏旁白；无有效台词时立即结束。</summary>
    public static IEnumerator Play(IReadOnlyList<string> lines, PlayOptions options = null)
    {
        options ??= new PlayOptions();
        if (lines == null || lines.Count == 0)
            yield break;

        Session session = CreateSession(options);
        try
        {
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                session.BodyText.text = line.Trim();
                session.AdvanceRequested = false;
                session.SkipRequested = false;

                float delay = ReadDelay(line, options);
                yield return WaitForAdvance(session, delay, options.AutoPlay);
                if (session.SkipRequested)
                    break;
            }
        }
        finally
        {
            DestroySession(session);
        }
    }

    /// <summary>便捷重载。</summary>
    public static IEnumerator Play(params string[] lines)
    {
        return Play(lines, null);
    }

    /// <summary>在宿主 MonoBehaviour 上启动过场并在结束时回调。</summary>
    public static void PlayOn(MonoBehaviour host, IReadOnlyList<string> lines, Action onComplete = null, PlayOptions options = null)
    {
        if (host == null)
        {
            onComplete?.Invoke();
            return;
        }

        host.StartCoroutine(CoPlayAndCallback(host, lines, onComplete, options));
    }

    private static IEnumerator CoPlayAndCallback(
        MonoBehaviour host,
        IReadOnlyList<string> lines,
        Action onComplete,
        PlayOptions options)
    {
        yield return Play(lines, options);
        if (host != null && host.isActiveAndEnabled)
            onComplete?.Invoke();
    }

    private static float ReadDelay(string text, PlayOptions options)
    {
        if (string.IsNullOrEmpty(text))
            return options.AutoPlayMinDelay;
        float t = options.AutoPlayMinDelay + text.Length * options.AutoPlaySecondsPerChar;
        return Mathf.Clamp(t, options.AutoPlayMinDelay, options.AutoPlayMaxDelay);
    }

    private static IEnumerator WaitForAdvance(Session session, float autoDelay, bool autoPlay)
    {
        if (autoPlay && autoDelay > 0f)
        {
            float elapsed = 0f;
            while (elapsed < autoDelay && !session.AdvanceRequested && !session.SkipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else
        {
            while (!session.AdvanceRequested && !session.SkipRequested)
                yield return null;
        }
    }

    private static Session CreateSession(PlayOptions options)
    {
        var session = new Session();
        session.Root = new GameObject(nameof(StoryBlackScreenInterlude) + "_Canvas");

        var canvas = session.Root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = options.SortingOrder;

        session.Root.AddComponent<GraphicRaycaster>();

        var scaler = session.Root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var bgGo = new GameObject("Background", typeof(RectTransform));
        bgGo.transform.SetParent(session.Root.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        StretchFill(bgRt);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = Color.black;
        bgImg.raycastTarget = true;

        var textGo = new GameObject("Body", typeof(RectTransform));
        textGo.transform.SetParent(session.Root.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(120f, 180f);
        textRt.offsetMax = new Vector2(-120f, -180f);

        session.BodyText = textGo.AddComponent<TextMeshProUGUI>();
        session.BodyText.text = string.Empty;
        session.BodyText.fontSize = 28f;
        session.BodyText.color = Color.white;
        session.BodyText.alignment = TextAlignmentOptions.Center;
        session.BodyText.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            session.BodyText.font = TMP_Settings.defaultFontAsset;

        session.NextButton = CreateFooterButton(session.Root.transform, "下一句", new Vector2(-140f, 48f), () => session.AdvanceRequested = true);

        if (options.AllowSkip)
        {
            session.SkipButton = CreateFooterButton(session.Root.transform, "跳过", new Vector2(-48f, 48f), () =>
            {
                session.SkipRequested = true;
                session.AdvanceRequested = true;
            });
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return session;
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
        var textRt = textGo.GetComponent<RectTransform>();
        StretchFill(textRt);
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

    private static void DestroySession(Session session)
    {
        if (session?.Root != null)
            UnityEngine.Object.Destroy(session.Root);
    }
}
