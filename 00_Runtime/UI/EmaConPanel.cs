using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ema 固定台词会话面板：问候 + 三个话题（各三句，Continue 推进）+「回头见」告别并关面板。
/// 预制体需放在 Resources/UI/EmaConPanel 以便 UiManager 动态加载（若你改为场景内引用可忽略）。
/// </summary>
public class EmaConPanel : BasePanel
{
    private enum Phase
    {
        ChoosingTopic,
        InTopic,
        ShowingFarewell,
    }

    [Header("文案")]
    [SerializeField] private string characterName = "Ema";
    [TextArea(1, 3)]
    [SerializeField] private string greetingLine = "嗨，找我吗？想聊点什么都行～";
    [TextArea(1, 3)]
    [SerializeField] private string farewellLine = "回头见啦，路上小心。";

    [SerializeField] private string goodbyeButtonLabel = "回头见";

    [Tooltip("三个话题：按钮上显示的文字 + 各三句台词（按 Continue 顺序播放）")]
    [SerializeField] private TopicConfig[] topics = new TopicConfig[3];

    [Header("UI 引用")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text contextText;
    [SerializeField] private Button[] topicButtons = new Button[3];
    [SerializeField] private Button goodbyeButton;
    [SerializeField] private Button continueButton;

    [System.Serializable]
    public class TopicConfig
    {
        [Tooltip("显示在对应按钮上的短句")]
        public string buttonCaption = "话题";
        [TextArea(2, 5)]
        public string sentence1 = "";
        [TextArea(2, 5)]
        public string sentence2 = "";
        [TextArea(2, 5)]
        public string sentence3 = "";
    }

    /// <summary>与 StoryDirector 设定一致：好友、小组作业、午餐；新项目可在 Inspector 覆盖。</summary>
    private static TopicConfig[] DefaultTopics()
    {
        return new[]
        {
            new TopicConfig
            {
                buttonCaption = "你和Remi是什么关系……",
                sentence1 = "Remi是我特别好的朋友啦，我们从大一就经常一起选课、一起赶作业。",
                sentence2 = "她人很直，话题也多，有时吵吵闹闹的，但我很吃她这套。",
                sentence3 = "对了，你可别在她面前说我「文静」，她会笑到停不下来。"
            },
            new TopicConfig
            {
                buttonCaption = "小组作业具体要做什么……",
                sentence1 = "这节课的小组作业要做一份十页左右的报告，再加一次课堂展示，分工表我待会发群里。",
                sentence2 = "我打算先搭大纲，Remi负责找资料，你如果有擅长的部分也可以说一声，我们调一调。",
                sentence3 = "截止时间还没最终定，但别拖到最后一周哦——我可是会天天在群里催进度的那种人。"
            },
            new TopicConfig
            {
                buttonCaption = "你对午餐有什么偏好……",
                sentence1 = "午餐我一般在食堂二楼解决，清淡一点的那种窗口我比较常去。",
                sentence2 = "太油太辣的我下午会犯困，所以除非心情特别好，我很少点重口味。",
                sentence3 = "有时候Remi拉我去试试新窗口，她点的总是比我的看起来好吃……我也就跟风尝几口。"
            }
        };
    }

    private static bool HasTopicBody(TopicConfig c)
    {
        if (c == null) return false;
        return !string.IsNullOrWhiteSpace(c.sentence1)
               || !string.IsNullOrWhiteSpace(c.sentence2)
               || !string.IsNullOrWhiteSpace(c.sentence3);
    }

    private static TopicConfig MergeTopicConfig(TopicConfig cur, TopicConfig def)
    {
        if (cur == null)
            return def;
        if (HasTopicBody(cur))
            return cur;
        return new TopicConfig
        {
            buttonCaption = string.IsNullOrWhiteSpace(cur.buttonCaption) ? def.buttonCaption : cur.buttonCaption,
            sentence1 = def.sentence1,
            sentence2 = def.sentence2,
            sentence3 = def.sentence3
        };
    }

    private Phase _phase = Phase.ChoosingTopic;
    private int _activeTopicIndex;
    private int _lineIndexInTopic;

    public override void Init()
    {
        TopicConfig[] defs = DefaultTopics();
        if (topics == null || topics.Length < 3)
        {
            var t = new TopicConfig[3];
            for (int i = 0; i < 3; i++)
            {
                TopicConfig cur = topics != null && i < topics.Length ? topics[i] : null;
                t[i] = MergeTopicConfig(cur, defs[i]);
            }

            topics = t;
        }
        else
        {
            for (int i = 0; i < 3; i++)
                topics[i] = MergeTopicConfig(topics[i], defs[i]);
        }

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);

        for (int i = 0; i < topicButtons.Length && i < 3; i++)
        {
            int idx = i;
            Button b = topicButtons[i];
            if (b == null) continue;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => OnTopicSelected(idx));
        }

        if (goodbyeButton != null)
        {
            goodbyeButton.onClick.RemoveAllListeners();
            goodbyeButton.onClick.AddListener(OnGoodbyeClicked);
            var gLabel = goodbyeButton.GetComponentInChildren<TMP_Text>(true);
            if (gLabel != null && !string.IsNullOrEmpty(goodbyeButtonLabel))
                gLabel.text = goodbyeButtonLabel;
        }

        ApplyTopicButtonLabels();
        EnterChoosingTopic();
    }

    public override void ShowMe()
    {
        base.ShowMe();
        EnterChoosingTopic();
    }

    private void ApplyTopicButtonLabels()
    {
        for (int i = 0; i < topicButtons.Length && i < topics.Length; i++)
        {
            if (topicButtons[i] == null) continue;
            string cap = topics[i] != null ? topics[i].buttonCaption : $"话题{i + 1}";
            var tmp = topicButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = string.IsNullOrWhiteSpace(cap) ? $"话题{i + 1}" : cap;
        }
    }

    private void EnterChoosingTopic()
    {
        _phase = Phase.ChoosingTopic;
        _lineIndexInTopic = 0;

        if (nameText != null)
            nameText.text = characterName;
        if (contextText != null)
            contextText.text = greetingLine ?? string.Empty;

        SetChoiceButtonsVisible(true);
        SetContinueVisible(false);
    }

    private void OnTopicSelected(int topicIndex)
    {
        if (_phase != Phase.ChoosingTopic) return;
        if (topics == null || topicIndex < 0 || topicIndex >= topics.Length || topics[topicIndex] == null)
            return;

        _activeTopicIndex = topicIndex;
        _phase = Phase.InTopic;
        _lineIndexInTopic = 0;

        SetChoiceButtonsVisible(false);
        SetContinueVisible(true);

        ShowCurrentTopicLine();
    }

    private void ShowCurrentTopicLine()
    {
        if (contextText == null || topics == null || _activeTopicIndex < 0 || _activeTopicIndex >= topics.Length)
            return;

        TopicConfig t = topics[_activeTopicIndex];
        if (t == null) return;

        string line = _lineIndexInTopic switch
        {
            0 => t.sentence1,
            1 => t.sentence2,
            2 => t.sentence3,
            _ => string.Empty
        };

        contextText.text = line ?? string.Empty;
    }

    private void OnContinueClicked()
    {
        if (_phase == Phase.InTopic)
        {
            _lineIndexInTopic++;
            if (_lineIndexInTopic >= 3)
            {
                EnterChoosingTopic();
                return;
            }

            ShowCurrentTopicLine();
            return;
        }

        if (_phase == Phase.ShowingFarewell)
        {
            // 必须走 EmaInteraction.EndDialogue，否则会一直 SetMoveLock(true)
            EmaInteraction emaIx = FindObjectOfType<EmaInteraction>();
            if (emaIx != null)
                emaIx.EndDialogue();
            else
            {
                UiManager.Instance.HidePanel<EmaConPanel>();
                if (UiManager.Instance.canvasObj != null)
                    UiManager.Instance.canvasObj.SetActive(false);
                FindObjectOfType<PlayerController>()?.SetMoveLock(false);
            }
        }
    }

    private void OnGoodbyeClicked()
    {
        if (_phase != Phase.ChoosingTopic) return;

        _phase = Phase.ShowingFarewell;
        SetChoiceButtonsVisible(false);
        SetContinueVisible(true);

        if (contextText != null)
            contextText.text = farewellLine ?? string.Empty;
    }

    private void SetChoiceButtonsVisible(bool visible)
    {
        for (int i = 0; i < topicButtons.Length; i++)
        {
            if (topicButtons[i] != null)
                topicButtons[i].gameObject.SetActive(visible);
        }

        if (goodbyeButton != null)
            goodbyeButton.gameObject.SetActive(visible);
    }

    private void SetContinueVisible(bool visible)
    {
        if (continueButton != null)
            continueButton.gameObject.SetActive(visible);
    }
}
