using UnityEngine;

/// <summary>
/// 对话呈现模式：挂在场景 System 上，Inspector 切换面对面 / 手机各自的表现方式。
/// <see cref="DeepSeekDialogueManager"/> 按当前交互通道读取本组件配置。
/// Demo 仅文字；TTS / 语音同步见 <c>99_Archived/TTS</c>。
/// </summary>
[DisallowMultipleComponent]
public class DialogueSequenceDirector : MonoBehaviour
{
    public static DialogueSequenceDirector Instance { get; private set; }

    [Header("面对面（F 对话 / DialoguePanel）")]
    [SerializeField] private RemiDialoguePresentationMode faceToFaceMode = RemiDialoguePresentationMode.TextTypewriterNoVoice;

    [Header("手机（PhoneAppPanel）")]
    [SerializeField] private RemiDialoguePresentationMode phoneMode = RemiDialoguePresentationMode.TextInstantNoVoice;

    [Header("文字打字机速度（TextTypewriterNoVoice）")]
    [Min(0.1f)]
    [SerializeField] private float typewriterCharsPerSecond = 18f;

    public float TypewriterCharsPerSecond => typewriterCharsPerSecond;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DialogueSequenceDirector] 场景中存在多个实例，后者将覆盖 Instance。");
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public RemiDialoguePresentationMode ResolveMode(RemiInteractionChannel? channel = null)
    {
        RemiInteractionChannel ch = channel
                                    ?? RemiPresenceService.Instance?.CurrentChannel
                                    ?? RemiInteractionChannel.FaceToFace;
        return NormalizeForDemo(ch == RemiInteractionChannel.Social ? phoneMode : faceToFaceMode);
    }

    /// <summary>Demo 不播 TTS：旧 Voice 模式回落到打字机。</summary>
    public static RemiDialoguePresentationMode NormalizeForDemo(RemiDialoguePresentationMode mode)
    {
        switch (mode)
        {
            case RemiDialoguePresentationMode.VoiceOnlyNoText:
            case RemiDialoguePresentationMode.LegacyVoiceTextSync:
                return RemiDialoguePresentationMode.TextTypewriterNoVoice;
            default:
                return mode;
        }
    }

    public static bool ShowsResponseText(RemiDialoguePresentationMode mode) =>
        NormalizeForDemo(mode) != RemiDialoguePresentationMode.VoiceOnlyNoText;

    public static bool PlaysVoice(RemiDialoguePresentationMode mode) => false;

    public static bool UsesTypewriter(RemiDialoguePresentationMode mode) =>
        NormalizeForDemo(mode) == RemiDialoguePresentationMode.TextTypewriterNoVoice;
}

/// <summary>Remi 回复的呈现方式。2/3 为归档枚举值，Demo 会 Normalize 为打字机。</summary>
public enum RemiDialoguePresentationMode
{
    /// <summary>仅文字打字机（面对面默认）。</summary>
    TextTypewriterNoVoice = 0,
    /// <summary>整段文字一次显示（手机默认）。</summary>
    TextInstantNoVoice = 1,
    /// <summary>归档：仅播 TTS。Demo 视为 TextTypewriterNoVoice。</summary>
    VoiceOnlyNoText = 2,
    /// <summary>归档：TTS 与字幕同步。Demo 视为 TextTypewriterNoVoice。</summary>
    LegacyVoiceTextSync = 3,
}