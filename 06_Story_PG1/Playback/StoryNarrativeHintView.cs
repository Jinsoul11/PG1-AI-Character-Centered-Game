using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 剧情 / 系统 Remi 线外的叙事提示：挂在场景 Canvas 下，拖好 <see cref="CanvasGroup"/> 与 <see cref="TMP_Text"/>。
/// <b>文案与时间</b>在本组件 Inspector 统一配置。
/// 有场景实例时走本 UI；否则回退 <see cref="StoryNarrativeHintOverlay"/>。
/// </summary>
[DisallowMultipleComponent]
public class StoryNarrativeHintView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text hintText;

    [Header("默认时间（各条提示未单独覆盖时使用）")]
    [SerializeField] private float defaultDelaySeconds = 0.35f;
    [Tooltip("alpha=1 时保持的秒数（不含渐入渐出）。")]
    [SerializeField] private float defaultDisplaySeconds = 8f;
    [SerializeField] private float defaultFadeInSeconds = 0.35f;
    [SerializeField] private float defaultFadeOutSeconds = 0.4f;

    [Header("文案：向 Remi 交书致谢后")]
    [SerializeField] private bool playAfterBookSubmitThanks = true;
    [TextArea(2, 5)]
    [SerializeField] private string afterBookSubmitThanksMessage = "Remi 很感激你帮她找到了书。";

    [Header("文案：教室开场后解锁手机联系人")]
    [SerializeField] private bool playAfterPhoneContactsAdded = true;
    [TextArea(1, 3)]
    [SerializeField] private string afterPhoneContactsAddedMessage = "已添加 Remi 和 Ema 为联系人！";

    [Header("文案：Day2 教室 · Remi 图书馆邀请")]
    [SerializeField] private bool playDay2RemiLibraryInvite = true;
    [TextArea(2, 4)]
    [SerializeField] private string day2RemiLibraryInviteMessage =
        "Remi 给你发了消息，邀请你下午去图书馆找她。打开手机看看吧。";

    [Header("文案：Day2 教室 · 前往门口")]
    [SerializeField] private bool playDay2GoToLibraryDoor = true;
    [TextArea(2, 4)]
    [SerializeField] private string day2GoToLibraryDoorMessage =
        "Remi 邀请你去图书馆。走到教室门口，选择「前往图书馆」。";

    [Header("文案：Day3 教室 · Remi 开场短信")]
    [SerializeField] private bool playDay3PhoneNudge = true;
    [TextArea(2, 4)]
    [SerializeField] private string day3PhoneNudgeMessage =
        "Remi 给你发了消息。打开手机，用 Chip 邀请她今晚来宿舍。";

    [Header("文案：Day3 · 手机保底提案（等确认「那走吧」）")]
    [SerializeField] private bool playDay3DeviationOffer = true;
    [TextArea(2, 4)]
    [SerializeField] private string day3DeviationOfferPhoneMessage =
        "Remi 发来消息，想换个地方。打开手机确认吧。";

    [SerializeField] private bool playAfterRhythmStoryDayBegins = true;
    [TextArea(2, 4)]
    [SerializeField] private string afterRhythmStoryDayBeginsMessage =
        "教室里的第一天开始了，你更像是在校园里观察她，而不是打扰她。";

    [Header("调试（需进入 Play）")]
    [TextArea(2, 5)]
    [SerializeField] private string debugPlayMessage = "（调试）叙事提示测试。";

    private const string FallbackOverlayAfterBookSubmit =
        "Remi 很感激你帮她找到了书。";

    private const string FallbackPhoneContactsAdded =
        "已添加 Remi 和 Ema 为联系人！";

    private const string FallbackRhythmStoryDay =
        "教室里的第一天开始了。";

    private const string FallbackDay2RemiLibraryInvite =
        "Remi 给你发了消息，邀请你下午去图书馆找她。打开手机看看吧。";

    private const string FallbackDay2GoToLibraryDoor =
        "Remi 邀请你去图书馆。走到教室门口，选择「前往图书馆」。";

    private const string FallbackDay3PhoneNudge =
        "Remi 给你发了消息。打开手机，用 Chip 邀请她今晚来宿舍。";

    private const string FallbackDay3DeviationOfferPhone =
        "Remi 发来消息，想换个地方。打开手机确认吧。";

    private Coroutine _playRoutine;

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        hintText = GetComponentInChildren<TMP_Text>(true);
    }

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    /// <summary>使用调用方传入的时间参数播放。</summary>
    public void ShowHint(string message, float delaySeconds, float displaySeconds, float fadeInSeconds, float fadeOutSeconds)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        if (hintText == null || canvasGroup == null)
        {
            Debug.LogWarning("[StoryNarrativeHintView] 缺少 hintText 或 canvasGroup，无法显示提示。", this);
            return;
        }

        if (_playRoutine != null)
            StopCoroutine(_playRoutine);

        _playRoutine = StartCoroutine(CoPlay(message.Trim(), delaySeconds, displaySeconds, fadeInSeconds, fadeOutSeconds));
    }

    /// <summary>使用本组件 Inspector 上的默认时间参数。</summary>
    public void ShowHint(string message)
    {
        ShowHint(message, defaultDelaySeconds, defaultDisplaySeconds, defaultFadeInSeconds, defaultFadeOutSeconds);
    }

    /// <summary>交书致谢 SendSystem 完成后播放的提示。</summary>
    public void PlayAfterBookSubmitThanks()
    {
        if (!playAfterBookSubmitThanks || string.IsNullOrWhiteSpace(afterBookSubmitThanksMessage))
            return;
        ShowHint(
            afterBookSubmitThanksMessage.Trim(),
            defaultDelaySeconds,
            defaultDisplaySeconds,
            defaultFadeInSeconds,
            defaultFadeOutSeconds);
    }

    public void PlayAfterPhoneContactsAdded(string overrideMessage = null)
    {
        if (!playAfterPhoneContactsAdded)
            return;
        string msg = string.IsNullOrWhiteSpace(overrideMessage)
            ? afterPhoneContactsAddedMessage
            : overrideMessage.Trim();
        if (string.IsNullOrWhiteSpace(msg))
            return;
        ShowHint(msg, defaultDelaySeconds, defaultDisplaySeconds, defaultFadeInSeconds, defaultFadeOutSeconds);
    }

    /// <summary>全局入口：教室开场后手机联系人提示。</summary>
    public static void TryPlayAfterPhoneContactsAdded(string overrideMessage = null)
    {
        StoryNarrativeHintView view = FindViewInstance();
        if (view != null)
        {
            view.PlayAfterPhoneContactsAdded(overrideMessage);
            return;
        }

        string msg = string.IsNullOrWhiteSpace(overrideMessage)
            ? FallbackPhoneContactsAdded
            : overrideMessage.Trim();
        if (string.IsNullOrWhiteSpace(msg))
            return;

        StoryNarrativeHintOverlay.Show(msg, 0.35f, 8f, 0.35f, 0.4f);
    }

    public void PlayAfterRhythmStoryDayBegins()
    {
        if (!playAfterRhythmStoryDayBegins || string.IsNullOrWhiteSpace(afterRhythmStoryDayBeginsMessage))
            return;
        ShowHint(afterRhythmStoryDayBeginsMessage.Trim());
    }

    public static void TryPlayAfterRhythmStoryDayBegins()
    {
        StoryNarrativeHintView view = FindViewInstance();
        if (view != null)
        {
            view.PlayAfterRhythmStoryDayBegins();
            return;
        }

        StoryNarrativeHintOverlay.Show(FallbackRhythmStoryDay, 0.35f, 8f, 0.35f, 0.4f);
    }

    /// <summary>全局入口：交书致谢后叙事条。</summary>
    public static void TryPlayAfterBookSubmitThanks()
    {
        StoryNarrativeHintView view = FindViewInstance();
        if (view != null)
        {
            view.PlayAfterBookSubmitThanks();
            return;
        }

        StoryNarrativeHintOverlay.Show(
            FallbackOverlayAfterBookSubmit,
            0.35f,
            8f,
            0.35f,
            0.4f);
    }

    /// <summary>任意自定义叙事提示。</summary>
    public static void TryPlayCustomHint(string message, float displaySeconds = 8f)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        StoryNarrativeHintView view = FindViewInstance();
        if (view != null)
        {
            view.ShowHint(message.Trim(), 0.35f, displaySeconds, 0.35f, 0.4f);
            return;
        }

        StoryNarrativeHintOverlay.Show(message.Trim(), 0.35f, displaySeconds, 0.35f, 0.4f);
    }

    /// <summary>Day2 教室开幕：Remi 发来图书馆邀请，提示打开手机。</summary>
    public static void TryPlayDay2RemiLibraryInvite(float displaySeconds = 8f)
    {
        StoryNarrativeHintView view = FindViewInstance();
        if (view != null)
        {
            view.PlayDay2RemiLibraryInvite(displaySeconds);
            return;
        }

        StoryNarrativeHintOverlay.Show(FallbackDay2RemiLibraryInvite, 0.35f, displaySeconds, 0.35f, 0.4f);
    }

    /// <summary>Day2：邀约送达后提示去门口选图书馆。</summary>
    public static void TryPlayDay2GoToLibraryDoor(string overrideMessage = null, float displaySeconds = 8f)
    {
        StoryNarrativeHintView view = FindViewInstance();
        if (view != null)
        {
            view.PlayDay2GoToLibraryDoor(overrideMessage, displaySeconds);
            return;
        }

        string msg = string.IsNullOrWhiteSpace(overrideMessage)
            ? FallbackDay2GoToLibraryDoor
            : overrideMessage.Trim();
        StoryNarrativeHintOverlay.Show(msg, 0.35f, displaySeconds, 0.35f, 0.4f);
    }

    /// <summary>Day3 开场：固定短信已送达，提示打开手机用 Chip 邀宿舍。</summary>
    public static void TryPlayDay3PhoneNudge(float displaySeconds = 7f)
    {
        StoryNarrativeHintView view = FindViewInstance();
        if (view != null)
        {
            view.PlayDay3PhoneNudge(displaySeconds);
            return;
        }

        StoryNarrativeHintOverlay.Show(FallbackDay3PhoneNudge, 0.35f, displaySeconds, 0.35f, 0.4f);
    }

    /// <summary>Day3 保底提案（手机）：提示打开手机用 Chip 确认。</summary>
    public static void TryPlayDay3DeviationOfferPhone(float displaySeconds = 6f)
    {
        StoryNarrativeHintView view = FindViewInstance();
        if (view != null)
        {
            view.PlayDay3DeviationOfferPhone(displaySeconds);
            return;
        }

        StoryNarrativeHintOverlay.Show(FallbackDay3DeviationOfferPhone, 0.35f, displaySeconds, 0.35f, 0.4f);
    }

    public void PlayDay2RemiLibraryInvite(float displaySeconds)
    {
        if (!playDay2RemiLibraryInvite || string.IsNullOrWhiteSpace(day2RemiLibraryInviteMessage))
            return;
        ShowHint(day2RemiLibraryInviteMessage.Trim(), defaultDelaySeconds, displaySeconds, defaultFadeInSeconds, defaultFadeOutSeconds);
    }

    public void PlayDay2GoToLibraryDoor(string overrideMessage, float displaySeconds)
    {
        if (!playDay2GoToLibraryDoor)
            return;
        string msg = string.IsNullOrWhiteSpace(overrideMessage)
            ? day2GoToLibraryDoorMessage
            : overrideMessage.Trim();
        if (string.IsNullOrWhiteSpace(msg))
            return;
        ShowHint(msg.Trim(), defaultDelaySeconds, displaySeconds, defaultFadeInSeconds, defaultFadeOutSeconds);
    }

    public void PlayDay3PhoneNudge(float displaySeconds)
    {
        if (!playDay3PhoneNudge || string.IsNullOrWhiteSpace(day3PhoneNudgeMessage))
            return;
        ShowHint(day3PhoneNudgeMessage.Trim(), defaultDelaySeconds, displaySeconds, defaultFadeInSeconds, defaultFadeOutSeconds);
    }

    public void PlayDay3DeviationOfferPhone(float displaySeconds)
    {
        if (!playDay3DeviationOffer || string.IsNullOrWhiteSpace(day3DeviationOfferPhoneMessage))
            return;
        ShowHint(
            day3DeviationOfferPhoneMessage.Trim(),
            defaultDelaySeconds,
            displaySeconds,
            defaultFadeInSeconds,
            defaultFadeOutSeconds);
    }

    private static StoryNarrativeHintView FindViewInstance()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<StoryNarrativeHintView>(FindObjectsInactive.Exclude);
#else
        return Object.FindObjectOfType<StoryNarrativeHintView>();
#endif
    }

    public void StopHint()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    [ContextMenu("调试/播放一次（需 Play 模式）")]
    private void ContextMenuDebugPlay()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StoryNarrativeHintView] 请在运行模式下使用「调试/播放一次」。", this);
            return;
        }

        ShowHint(debugPlayMessage);
    }

    private IEnumerator CoPlay(string message, float delaySeconds, float displaySeconds, float fadeInSeconds, float fadeOutSeconds)
    {
        hintText.text = message;

        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        fadeInSeconds = Mathf.Max(0.01f, fadeInSeconds);
        fadeOutSeconds = Mathf.Max(0.01f, fadeOutSeconds);
        displaySeconds = Mathf.Max(0f, displaySeconds);

        float t = 0f;
        while (t < fadeInSeconds)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / fadeInSeconds);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        if (displaySeconds > 0f)
            yield return new WaitForSecondsRealtime(displaySeconds);

        t = 0f;
        while (t < fadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutSeconds);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        _playRoutine = null;
    }
}
