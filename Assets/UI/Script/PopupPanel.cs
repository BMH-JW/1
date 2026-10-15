using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PopupPanel : MonoBehaviour
{
    /// <summary>来源标记：File3=通关文书弹窗，File2=档案弹窗，空=其他</summary>
    public string popupSource = "";

    public TMP_Text titleText;
    public TMP_Text contentText;
    public Button closeBtn;

    void Awake()
    {
        if (closeBtn != null) closeBtn.onClick.AddListener(ClosePanel);
    }

    public void SetSource(string source)
    {
        popupSource = source;
    }

    public void SetText(string title, string content)
    {
        if (titleText != null) titleText.text = title;
        if (contentText != null) contentText.text = content;
    }

    public void ClosePanel()
    {
        RoomInteract.Untrack(gameObject);
        Destroy(gameObject);
    }
}
