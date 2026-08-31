using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>微信朋友圈式动态流（挂在社媒 App 的「动态」页）。</summary>
public class SocialMomentsView : MonoBehaviour
{
    [SerializeField] private RectTransform momentsPageRoot;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Sprite remiAvatarSprite;
    [SerializeField] private Color rowBackground = new Color(0.1f, 0.1f, 0.1f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color metaColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color likeActiveColor = new Color(0.95f, 0.55f, 0.2f, 1f);
    [SerializeField] private float imageHeight = 160f;

    private readonly List<RectTransform> _rows = new List<RectTransform>();
    private RectTransform _coverHeader;
    private RectTransform _momentsPage;
    private PhoneAppPanel _host;

    public void BindHost(PhoneAppPanel host) => _host = host;

    /// <summary>手动 UI：请绑定 Moments Page Root / Scroll / Content；不再自动建页。</summary>
    public void EnsureBuilt(RectTransform parent, Sprite avatar)
    {
        if (avatar != null)
            remiAvatarSprite = avatar;
        if (momentsPageRoot != null)
            _momentsPage = momentsPageRoot;
        if (scrollRect == null)
            Debug.LogError("[SocialMomentsView] 请绑定 Scroll Rect 与 Content Root（已取消自动生成动态页）。", this);
    }

    public void SetVisible(bool visible)
    {
        RectTransform page = momentsPageRoot != null ? momentsPageRoot : _momentsPage;
        if (page != null)
            page.gameObject.SetActive(visible);
        if (visible)
            Refresh();
    }

    private void OnEnable()
    {
        if (RemiMomentsService.Instance != null)
            RemiMomentsService.Instance.FeedChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (RemiMomentsService.Instance != null)
            RemiMomentsService.Instance.FeedChanged -= Refresh;
    }

    public void Refresh()
    {
        if (contentRoot == null) return;
        RemiMomentsService.Instance?.SyncForCurrentStage();

        ClearPostRows();

        IReadOnlyList<RemiMomentsPublishedPost> feed = RemiMomentsService.Instance?.Feed;
        if (feed == null || feed.Count == 0)
        {
            RectTransform empty = SocialChatUiFactory.CreateMomentsEmptyHint(contentRoot, "还没有动态，关系加深后她会发更多。");
            _rows.Add(empty);
            return;
        }

        foreach (RemiMomentsPublishedPost post in feed)
        {
            if (post?.Definition == null) continue;
            RectTransform row = BuildPostRow(post);
            _rows.Add(row);
        }

        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void ClearPostRows()
    {
        _rows.Clear();
        if (contentRoot == null) return;
        for (int c = contentRoot.childCount - 1; c >= 0; c--)
        {
            Transform ch = contentRoot.GetChild(c);
            if (_coverHeader != null && ch == _coverHeader)
                continue;
            Destroy(ch.gameObject);
        }
    }

    private RectTransform BuildPostRow(RemiMomentsPublishedPost post)
    {
        RemiMomentsPostDefinition def = post.Definition;
        RectTransform row = SocialChatUiFactory.CreateChild(contentRoot, "Moment_" + def.postId);
        LayoutElement rowLe = row.gameObject.AddComponent<LayoutElement>();
        rowLe.minHeight = 80;

        Image rowBg = row.gameObject.AddComponent<Image>();
        rowBg.color = rowBackground;

        VerticalLayoutGroup vlg = row.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 12, 12);
        vlg.spacing = 8;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // 头像 + 名 + 时间
        RectTransform head = SocialChatUiFactory.CreateChild(row, "Head");
        HorizontalLayoutGroup hlg = head.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childControlHeight = true;
        hlg.childControlWidth = false;
        LayoutElement headLe = head.gameObject.AddComponent<LayoutElement>();
        headLe.preferredHeight = 44;

        Image av = SocialChatUiFactory.CreateAvatarImage(head, 40, remiAvatarSprite);
        RectTransform metaCol = SocialChatUiFactory.CreateChild(head, "Meta");
        VerticalLayoutGroup metaV = metaCol.gameObject.AddComponent<VerticalLayoutGroup>();
        metaV.spacing = 2;
        metaV.childControlWidth = true;
        TMP_Text nameTmp = SocialChatUiFactory.CreateTmpText(metaCol, "Remi", 17, TextAlignmentOptions.Left);
        nameTmp.color = new Color(0.75f, 0.85f, 1f, 1f);
        nameTmp.fontStyle = FontStyles.Bold;

        RemiWorldTime now = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.CaptureWorldTime()
            : RemiWorldTime.BeforeStory;
        string timeLabel = post.PublishedAt.IsStoryStarted
            ? RemiWorldTimeFormat.FormatRelative(post.PublishedAt, now)
            : (string.IsNullOrEmpty(def.timeLabel) ? "刚刚" : def.timeLabel);
        TMP_Text timeTmp = SocialChatUiFactory.CreateTmpText(metaCol, timeLabel, 13, TextAlignmentOptions.Left);
        timeTmp.color = metaColor;

        TMP_Text body = SocialChatUiFactory.CreateTmpText(row, def.body, 18, TextAlignmentOptions.TopLeft);
        body.color = textColor;
        body.enableWordWrapping = true;

        if (def.hasImage)
        {
            RectTransform imgRt = SocialChatUiFactory.CreateChild(row, "Image");
            LayoutElement imgLe = imgRt.gameObject.AddComponent<LayoutElement>();
            imgLe.preferredHeight = imageHeight;
            Image img = imgRt.gameObject.AddComponent<Image>();
            if (def.imageSprite != null)
            {
                img.sprite = def.imageSprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = def.imagePlaceholderColor;
            }
        }

        // 赞 / 评论
        RectTransform actions = SocialChatUiFactory.CreateChild(row, "Actions");
        HorizontalLayoutGroup actH = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        actH.spacing = 24;
        LayoutElement actLe = actions.gameObject.AddComponent<LayoutElement>();
        actLe.preferredHeight = 32;

        string postId = def.postId;
        Button likeBtn = SocialChatUiFactory.CreateTextButton(actions, post.LikedByPlayer ? "已赞" : "赞",
            post.LikedByPlayer ? likeActiveColor : metaColor);
        likeBtn.onClick.AddListener(() =>
        {
            RemiMomentsService.Instance?.TryToggleLike(postId);
        });

        Button commentBtn = SocialChatUiFactory.CreateTextButton(actions, "评论", metaColor);
        commentBtn.onClick.AddListener(() => _host?.BeginCommentOnMoment(postId));

        if (post.Comments != null && post.Comments.Count > 0)
        {
            RectTransform commentBox = SocialChatUiFactory.CreateChild(row, "Comments");
            Image boxBg = commentBox.gameObject.AddComponent<Image>();
            boxBg.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            VerticalLayoutGroup cv = commentBox.gameObject.AddComponent<VerticalLayoutGroup>();
            cv.padding = new RectOffset(8, 8, 6, 6);
            cv.spacing = 4;
            foreach (RemiMomentsPlayerComment c in post.Comments)
            {
                if (c == null || string.IsNullOrEmpty(c.text)) continue;
                TMP_Text line = SocialChatUiFactory.CreateTmpText(commentBox, "你：" + c.text, 15, TextAlignmentOptions.TopLeft);
                line.color = textColor;
            }
        }

        return row;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
