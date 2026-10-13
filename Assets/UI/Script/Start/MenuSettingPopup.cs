using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuSettingPopup : MonoBehaviour
{
    [Header("设置弹窗面板物体")]
    public GameObject panelSetting;
    [Header("菜单上的Setting按钮")]
    public Button btnSetting;
    [Header("弹窗里面的关闭按钮")]
    public Button btnCloseSetting;

    void Start()
    {
        btnSetting.onClick.AddListener(OpenSettingPanel);
        btnCloseSetting.onClick.AddListener(CloseSettingPanel);
    }

    public void OpenSettingPanel()
    {
        panelSetting.SetActive(true);
    }

    public void CloseSettingPanel()
    {
        panelSetting.SetActive(false);
    }

    void OnDestroy()
    {
        btnSetting.onClick.RemoveListener(OpenSettingPanel);
        btnCloseSetting.onClick.RemoveListener(CloseSettingPanel);
    }
}