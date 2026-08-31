using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>单条聊天气泡行：左侧 Remi / 右侧玩家。</summary>
public class SocialChatMessageRow : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private Image remiAvatar;
    [SerializeField] private Image playerAvatar;
    [SerializeField] private Image remiBubble;
    [SerializeField] private Image playerBubble;
    [SerializeField] private TMP_Text remiText;
    [SerializeField] private TMP_Text playerText;

    public void Setup(
        bool isPlayer,
        string content,
        Sprite remiAvatarSprite,
        Sprite playerAvatarSprite,
        Color remiBubbleColor,
        Color playerBubbleColor)
    {
        if (remiAvatar != null)
        {
            remiAvatar.gameObject.SetActive(!isPlayer);
            if (remiAvatarSprite != null) remiAvatar.sprite = remiAvatarSprite;
        }

        if (playerAvatar != null)
        {
            playerAvatar.gameObject.SetActive(isPlayer);
            if (playerAvatarSprite != null) playerAvatar.sprite = playerAvatarSprite;
        }

        if (remiBubble != null)
        {
            remiBubble.gameObject.SetActive(!isPlayer);
            remiBubble.color = remiBubbleColor;
        }

        if (playerBubble != null)
        {
            playerBubble.gameObject.SetActive(isPlayer);
            playerBubble.color = playerBubbleColor;
        }

        if (remiText != null)
        {
            remiText.gameObject.SetActive(!isPlayer);
            remiText.richText = true;
            remiText.text = isPlayer ? string.Empty : content ?? string.Empty;
            remiText.alignment = TextAlignmentOptions.TopLeft;
        }

        if (playerText != null)
        {
            playerText.gameObject.SetActive(isPlayer);
            playerText.richText = true;
            playerText.text = isPlayer ? content ?? string.Empty : string.Empty;
            playerText.alignment = TextAlignmentOptions.TopRight;
        }

        ApplyRowAlignment(isPlayer);
        LayoutRebuilder.ForceRebuildLayoutImmediate(root != null ? root : (RectTransform)transform);
    }

    /// <summary>
    /// 玩家消息：行宽撑满 + HLG 右对齐，中间 Spacer 把气泡顶到右侧。
    /// Remi 消息：左对齐。
    /// </summary>
    private void ApplyRowAlignment(bool isPlayer)
    {
        RectTransform rowRt = root != null ? root : (RectTransform)transform;
        if (rowRt == null) return;

        HorizontalLayoutGroup hlg = rowRt.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
            hlg.childAlignment = isPlayer ? TextAnchor.UpperRight : TextAnchor.UpperLeft;

        // Content 的 VLG 未开启 Child Control Width 时，子项不会被强制拉满；
        // 这里按父级宽度写入 preferredWidth，让 Spacer 有空间伸展。
        RectTransform parentRt = rowRt.parent as RectTransform;
        float parentWidth = parentRt != null ? parentRt.rect.width : 0f;
        if (parentWidth > 1f)
        {
            LayoutElement le = rowRt.GetComponent<LayoutElement>();
            if (le == null)
                le = rowRt.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredWidth = parentWidth;
            rowRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, parentWidth);
        }

        // 确保 Spacer 仍在中间且可伸展（预制体/旧行也可能缺）
        Transform spacer = rowRt.Find("Spacer");
        if (spacer != null)
        {
            LayoutElement spacerLe = spacer.GetComponent<LayoutElement>();
            if (spacerLe == null)
                spacerLe = spacer.gameObject.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            spacerLe.minWidth = 0f;
            spacer.gameObject.SetActive(true);
        }
    }

    public void Bind(
        RectTransform rowRoot,
        Image remiAv,
        Image playerAv,
        Image remiBub,
        Image playerBub,
        TMP_Text remiTmp,
        TMP_Text playerTmp)
    {
        root = rowRoot;
        remiAvatar = remiAv;
        playerAvatar = playerAv;
        remiBubble = remiBub;
        playerBubble = playerBub;
        remiText = remiTmp;
        playerText = playerTmp;
    }
}
