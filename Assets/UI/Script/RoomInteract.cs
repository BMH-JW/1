using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoomInteract : MonoBehaviour
{
    public GameObject popupPrefab;
    public string title;
    [TextArea(5, 10)] public string content;
    private GameObject currentPopup;

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(OpenPopup);
    }

    void OpenPopup()
    {
        if (currentPopup != null) Destroy(currentPopup);
        currentPopup = Instantiate(popupPrefab, transform.parent.parent);
        PopupPanel popup = currentPopup.GetComponent<PopupPanel>();
        popup.SetText(title, content);
    }
}