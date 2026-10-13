using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PopupPanel : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text contentText;
    public Button closeBtn;

    void Awake()
    {
        closeBtn.onClick.AddListener(ClosePanel);
    }

    public void SetText(string title, string content)
    {
        titleText.text = title;
        contentText.text = content;
    }

    void ClosePanel()
    {
        Destroy(gameObject);
    }
}
