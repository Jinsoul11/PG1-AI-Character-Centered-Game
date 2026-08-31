using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Ema : MonoBehaviour
{
    [Serializable]
    public class DialogueOption
    {
        [Tooltip("按钮上显示的选项文本")]
        public string label;

        [Tooltip("Ema 选中该选项后的固定回复")]
        [TextArea(2, 6)]
        public string emaReply;

        [Tooltip("选中后跳转到的节点索引；-1 表示对话结束/停留当前节点")]
        public int nextNodeIndex = -1;

        [Tooltip("选中该选项时触发的事件（推进任务、给道具、切换场景等）")]
        public UnityEvent onSelected;
    }

    [Serializable]
    public class DialogueNode
    {
        [Tooltip("进入该节点时 Ema 说的固定台词")]
        [TextArea(2, 6)]
        public string emaLine;

        [Tooltip("该节点可供玩家选择的选项")]
        public List<DialogueOption> options = new List<DialogueOption>();
    }

    [Header("对话配置")]
    [SerializeField] private int startNodeIndex = 0;
    [SerializeField] private List<DialogueNode> nodes = new List<DialogueNode>();

    public int CurrentNodeIndex { get; private set; } = -1;

    public void ResetDialogue()
    {
        CurrentNodeIndex = Mathf.Clamp(startNodeIndex, 0, Mathf.Max(0, nodes.Count - 1));
    }

    public DialogueNode GetCurrentNode()
    {
        if (nodes == null || nodes.Count == 0) return null;
        if (CurrentNodeIndex < 0 || CurrentNodeIndex >= nodes.Count) return null;
        return nodes[CurrentNodeIndex];
    }

    public bool TrySelectOption(int optionIndex, out string playerLine, out string emaReply)
    {
        playerLine = string.Empty;
        emaReply = string.Empty;

        var node = GetCurrentNode();
        if (node == null || node.options == null) return false;
        if (optionIndex < 0 || optionIndex >= node.options.Count) return false;

        var opt = node.options[optionIndex];
        playerLine = opt.label ?? string.Empty;
        emaReply = opt.emaReply ?? string.Empty;

        opt.onSelected?.Invoke();

        if (opt.nextNodeIndex >= 0 && opt.nextNodeIndex < nodes.Count)
        {
            CurrentNodeIndex = opt.nextNodeIndex;
        }

        return true;
    }

    private void Awake()
    {
        ResetDialogue();
    }
}
