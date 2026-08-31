using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 剧情转场用：屏幕左侧简短叙事/目标提示（不依赖预制体，运行时生成独立 Canvas）。
/// 优先在场景挂 <see cref="StoryNarrativeHintView"/> 并在其上改文案；无实例时本类作为后备。
/// </summary>
public static class StoryNarrativeHintOverlay
{
    private static CoroutineRunner _runner;

    private class CoroutineRunner : MonoBehaviour
    {
    }

    private static void EnsureRunner()
    {
        if (_runner != null) return;
        var go = new GameObject(nameof(StoryNarrativeHintOverlay) + "_Runner");
        Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<CoroutineRunner>();
    }

    public static void Show(string message, float delaySeconds, float displaySeconds, float fadeInSeconds, float fadeOutSeconds)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        EnsureRunner();
        _runner.StartCoroutine(CoShow(message.Trim(), delaySeconds, displaySeconds, fadeInSeconds, fadeOutSeconds));
    }

    private static IEnumerator CoShow(string message, float delaySeconds, float displaySeconds, float fadeInSeconds, float fadeOutSeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        var root = new GameObject("NarrativeHintCanvas");
        Object.DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        root.AddComponent<GraphicRaycaster>();

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("HintPanel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 0.5f);
        panelRt.anchorMax = new Vector2(0f, 0.5f);
        panelRt.pivot = new Vector2(0f, 0.5f);
        panelRt.anchoredPosition = new Vector2(48f, 0f);
        panelRt.sizeDelta = new Vector2(560f, 200f);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.55f);
        bg.raycastTarget = false;

        var cg = panel.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        var textGo = new GameObject("HintText", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = message;
        tmp.fontSize = 26f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        //tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(20f, 18f);
        textRt.offsetMax = new Vector2(-20f, -18f);

        fadeInSeconds = Mathf.Max(0.01f, fadeInSeconds);
        fadeOutSeconds = Mathf.Max(0.01f, fadeOutSeconds);
        displaySeconds = Mathf.Max(0f, displaySeconds);

        float t = 0f;
        while (t < fadeInSeconds)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / fadeInSeconds);
            yield return null;
        }

        cg.alpha = 1f;
        if (displaySeconds > 0f)
            yield return new WaitForSecondsRealtime(displaySeconds);

        t = 0f;
        while (t < fadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = 1f - Mathf.Clamp01(t / fadeOutSeconds);
            yield return null;
        }

        Object.Destroy(root);
    }
}
