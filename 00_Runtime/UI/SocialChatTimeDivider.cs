using TMPro;
using UnityEngine;

public class SocialChatTimeDivider : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    public void SetTimeText(string text)
    {
        if (label != null)
            label.text = text ?? string.Empty;
    }

    public void Bind(TMP_Text textComponent) => label = textComponent;
}
