using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JU.GameData
{
    [CreateAssetMenu(fileName = "GameDataConfig", menuName = "JU/Game Data Config")]
    public class GameDataConfig : ScriptableObject
    {
        [Tooltip("运行时会复制配置；修改此资产不会改变已经开始的游戏。")]
        public GameConfigData data = new GameConfigData();
        [Tooltip("Key与数据中的portraitKey一致。占位Key没有对应图片时，调试窗口会明确提示。")]
        public List<PortraitBinding> portraitBindings = new List<PortraitBinding>();

        public Sprite ResolvePortrait(string key)
        {
            var matches = portraitBindings.Where(p => p != null && p.key == key).ToList();
            if (matches.Count != 1 || matches[0].sprite == null)
                throw new InvalidOperationException("立绘Key缺少唯一有效的Sprite绑定：" + key);
            return matches[0].sprite;
        }
    }
    [Serializable] public class PortraitBinding { public string key; public Sprite sprite; }
}
