using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JU.GameData.Tests
{
    public class GameDataTests
    {
        [TestCaseSource(typeof(GameDataScenarios), "Names")]
        public void DataScenario(string name)
        {
            var config = AssetDatabase.LoadAssetAtPath<GameDataConfig>("Assets/GameData/Config/DefaultGameDataConfig.asset");
            Assert.That(config, Is.Not.Null, "默认ScriptableObject配置应能正常导入。");
            GameDataScenarios.Run(name, config.data);
        }
        [Test]
        public void DefaultAssetMatchesSeedJson()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameDataConfig>("Assets/GameData/Config/DefaultGameDataConfig.asset");
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/GameData/Editor/Defaults.json");
            Assert.That(config, Is.Not.Null);
            Assert.That(json, Is.Not.Null);
            var fromJson = JsonUtility.FromJson<GameConfigData>(json.text);
            Assert.That(JsonUtility.ToJson(config.data), Is.EqualTo(JsonUtility.ToJson(fromJson)));
        }
    }
}
