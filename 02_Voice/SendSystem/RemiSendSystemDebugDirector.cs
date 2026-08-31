using System.Collections;
using UnityEngine;

/// <summary>
/// 开发用：全局覆盖下一次 / 所有 <see cref="PromptedDialogueAgent.SendSystem"/> 的
/// initiator（director_context），用来快速试角色主动开口表现。
/// 拦截挂在 <see cref="PromptedDialogueAgent"/> 入口，新增 SendSystem 调用无需改此脚本。
/// F8 切换面板。
/// SendSystem 不再接受独立 narrative-intent 文案段。
/// </summary>
[DisallowMultipleComponent]
public class RemiSendSystemDebugDirector : MonoBehaviour
{
    public enum OverrideMode
    {
        /// <summary>不改写，仍用调用方原文。</summary>
        Off = 0,
        /// <summary>整段替换 initiator context。</summary>
        ReplaceInitiator = 1,
        /// <summary>已废弃：等同 ReplaceInitiator（SendSystem 不再带独立 intent 段）。</summary>
        ReplaceBoth = 2,
        /// <summary>把调试文本加在原始 initiator 前面（换行拼接）。</summary>
        PrefixInitiator = 3,
    }

    public static RemiSendSystemDebugDirector Instance { get; private set; }

    [Header("快捷键")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F8;
    [SerializeField] private bool visibleOnStart;

    [Header("覆盖开关")]
    [SerializeField] private OverrideMode mode = OverrideMode.Off;
    [SerializeField] private bool oneShot;

    [Header("调试 Director 文本")]
    [TextArea(4, 12)]
    [SerializeField]
    private string debugInitiatorContext =
        "（调试）你刚和玩家聊完。你忽然想起教室里有本《AI游戏入门》，想请对方帮忙找一下。";

    [Header("立刻试一轮（不依赖剧情 beat）")]
    [SerializeField] private bool fireOpensDialogueIfNeeded = true;

    private bool _visible;
    private Vector2 _scroll;
    private string _lastOriginalInitiator = "";
    private string _lastAppliedInitiator = "";
    private string _status = "F8 开面板。Mode=Off 时不改动任何 SendSystem。";
    private bool _fireRoutineRunning;

    public OverrideMode Mode => mode;
    public bool IsOverrideActive => mode != OverrideMode.Off;

    public static void EnsureExists()
    {
        if (Instance != null)
            return;
        var go = new GameObject(nameof(RemiSendSystemDebugDirector));
        go.AddComponent<RemiSendSystemDebugDirector>();
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
        _visible = visibleOnStart;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            ToggleVisible();
    }

    public void ToggleVisible() => _visible = !_visible;

    /// <summary>
    /// 由 <see cref="PromptedDialogueAgent"/> 在每次 CharacterTriggered 写入前调用。
    /// 返回是否应用了覆盖（仅用于日志）。
    /// </summary>
    public bool TryResolve(string originalInitiator, out string initiator)
    {
        initiator = originalInitiator ?? string.Empty;
        _lastOriginalInitiator = initiator;

        if (mode == OverrideMode.Off)
        {
            _lastAppliedInitiator = initiator;
            return false;
        }

        switch (mode)
        {
            case OverrideMode.ReplaceInitiator:
            case OverrideMode.ReplaceBoth:
                initiator = debugInitiatorContext ?? string.Empty;
                break;
            case OverrideMode.PrefixInitiator:
            {
                string prefix = (debugInitiatorContext ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(prefix))
                {
                    string body = initiator.Trim();
                    initiator = string.IsNullOrEmpty(body) ? prefix : prefix + "\n" + body;
                }

                break;
            }
        }

        _lastAppliedInitiator = initiator;
        _status =
            $"已覆盖 SendSystem · mode={mode} oneShot={oneShot} · " +
            $"{System.DateTime.Now:HH:mm:ss}";

        if (oneShot)
            mode = OverrideMode.Off;

        Debug.Log(
            $"[RemiSendSystemDebug] Override applied.\n--- original initiator ---\n{_lastOriginalInitiator}\n" +
            $"--- applied initiator ---\n{_lastAppliedInitiator}");
        return true;
    }

    /// <summary>立刻发一轮 SendSystem，用当前调试文本（可在面板点 Fire）。</summary>
    public void FireTestSendSystemNow()
    {
        if (_fireRoutineRunning)
            return;
        StartCoroutine(CoFireTestSendSystem());
    }

    private IEnumerator CoFireTestSendSystem()
    {
        _fireRoutineRunning = true;
        OverrideMode savedMode = mode;
        bool savedOneShot = oneShot;
        try
        {
            PromptedDialogueAgent agent = PromptedDialogueAgent.Instance != null
                ? PromptedDialogueAgent.Instance
                : FindObjectOfType<PromptedDialogueAgent>();
            if (agent == null)
            {
                _status = "未找到 PromptedDialogueAgent，无法 Fire。";
                Debug.LogWarning("[RemiSendSystemDebug] " + _status);
                yield break;
            }

            if (fireOpensDialogueIfNeeded)
            {
                RemiInteraction interaction = FindObjectOfType<RemiInteraction>();
                if (interaction != null && !interaction.IsInDialogue)
                    interaction.StartDialogue(bypassOpenGates: true);
            }

            // Fire 本身就是试当前框里的字，临时强制 ReplaceInitiator；不消费 oneShot。
            mode = OverrideMode.ReplaceInitiator;
            oneShot = false;

            bool done = false;
            string reply = null;
            string err = null;
            System.Action<string> reveal = DialoguePanel.CreateWorldRevealCallbackIfOpen();

            yield return agent.SendSystem(
                debugInitiatorContext,
                (text, expr) =>
                {
                    reply = text;
                    DialoguePanel.OnScriptedUtteranceComplete(text, expr);
                    done = true;
                },
                e =>
                {
                    err = e;
                    done = true;
                },
                reveal);

            while (!done)
                yield return null;

            if (string.IsNullOrEmpty(err))
            {
                string preview = string.IsNullOrEmpty(reply)
                    ? "(empty)"
                    : (reply.Length <= 48 ? reply : reply.Substring(0, 48) + "…");
                _status = "Fire OK · reply=" + preview;
            }
            else
                _status = "Fire failed · " + err;

            Debug.Log($"[RemiSendSystemDebug] {_status}");
        }
        finally
        {
            mode = savedMode;
            oneShot = savedOneShot;
            _fireRoutineRunning = false;
        }
    }

    private void OnGUI()
    {
        if (!_visible)
            return;

        const float width = 560f;
        const float height = 620f;
        var rect = new Rect(Screen.width - width - 12f, 12f, width, height);
        GUILayout.BeginArea(rect, GUI.skin.window);
        GUILayout.Label("Remi SendSystem Director Debug (F8)");
        GUILayout.Label(_status);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Off", GUILayout.Width(44f)))
            mode = OverrideMode.Off;
        if (GUILayout.Button("Replace Init", GUILayout.Width(90f)))
            mode = OverrideMode.ReplaceInitiator;
        if (GUILayout.Button("Prefix", GUILayout.Width(56f)))
            mode = OverrideMode.PrefixInitiator;
        GUILayout.EndHorizontal();

        oneShot = GUILayout.Toggle(oneShot, "One-shot（下次 SendSystem 用完自动 Off）");
        fireOpensDialogueIfNeeded = GUILayout.Toggle(fireOpensDialogueIfNeeded, "Fire 时自动打开面对面面板");

        GUILayout.Label($"Mode = {mode}");

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(height - 220f));

        GUILayout.Label("debugInitiatorContext（director_context）");
        debugInitiatorContext = GUILayout.TextArea(debugInitiatorContext ?? "", GUILayout.MinHeight(120f));

        GUILayout.Space(8f);
        GUILayout.Label("Last original initiator");
        GUILayout.TextArea(_lastOriginalInitiator ?? "", GUILayout.MinHeight(48f));
        GUILayout.Label("Last applied initiator");
        GUILayout.TextArea(_lastAppliedInitiator ?? "", GUILayout.MinHeight(48f));

        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fire Test SendSystem", GUILayout.Height(28f)))
            FireTestSendSystemNow();
        if (GUILayout.Button("Copy Applied", GUILayout.Width(100f), GUILayout.Height(28f)))
            GUIUtility.systemCopyBuffer = _lastAppliedInitiator ?? "";
        if (GUILayout.Button("Hide", GUILayout.Width(52f), GUILayout.Height(28f)))
            _visible = false;
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Fire Test SendSystem")]
    private void Editor_Fire() => FireTestSendSystemNow();

    [ContextMenu("Debug/Set Mode ReplaceInitiator")]
    private void Editor_ReplaceInitiator() => mode = OverrideMode.ReplaceInitiator;
#endif
}
