using UnityEngine;

namespace JU.GameData
{
    [CreateAssetMenu(fileName = "GameDataConfig", menuName = "JU/Game Data Config")]
    public class GameDataConfig : ScriptableObject
    {
        [Tooltip("运行时会复制配置；修改此资产不会改变已经开始的游戏。")]
        public GameConfigData data = new GameConfigData();
    }
}
