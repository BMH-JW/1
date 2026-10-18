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
        [Test]
        public void PortraitBindingsRejectMissingAndDuplicateKeys()
        {
            var asset = ScriptableObject.CreateInstance<GameDataConfig>();
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => asset.ResolvePortrait("missing"));
                asset.portraitBindings.Add(new PortraitBinding { key = "test", sprite = sprite });
                Assert.That(asset.ResolvePortrait("test"), Is.SameAs(sprite));
                asset.portraitBindings.Add(new PortraitBinding { key = "test", sprite = sprite });
                Assert.Throws<System.InvalidOperationException>(() => asset.ResolvePortrait("test"));
            }
            finally { Object.DestroyImmediate(asset); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }
    }
}
