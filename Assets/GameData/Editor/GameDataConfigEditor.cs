using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JU.GameData.Editor
{
    [CustomEditor(typeof(GameDataConfig))]
    public class GameDataConfigEditor : UnityEditor.Editor
    {
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "startDay", "起始天数" }, { "dailyApplicants", "每日新申请人数" }, { "initialTrust", "初始信任" },
            { "initialPurity", "初始纯净值" }, { "initialOrder", "初始秩序值" },
            { "recoveryCeiling", "自然恢复上限" }, { "dailyRecovery", "每日自然恢复" },
            { "blackSwanThreshold", "黑天鹅触发阈值（≤）" }, { "worldFailureThreshold", "世界崩溃阈值（≤）" },
            { "blackSwanDays", "黑天鹅持续次数" }, { "blackSwanDamage", "黑天鹅每日两界扣值" },
            { "complianceReward", "合规放行信任奖励" }, { "violationPenalty", "违规放行信任扣值" },
            { "dailyTrustBonus", "每日信任固定增加" }, { "memoryDecay", "收容记忆每日减少" },
            { "relationshipDecay", "收容关系每日减少" }, { "desireGrowth", "收容欲望每日增加" },
            { "willGrowth", "收容意志每日增加" }, { "blackSwanExtraDesire", "黑天鹅额外欲望增加" },
            { "blackSwanExtraWill", "黑天鹅额外意志增加" }, { "honestMaxDeviation", "诚实偏差上限" },
            { "embellishedMaxDeviation", "粉饰偏差上限" }, { "bands", "属性区间与权重（5条）" },
            { "tags", "二维标签（50条）" }, { "details", "一维明细（25条）" }, { "motives", "真实动机（9条）" },
            { "documents", "通关文书与补充模板" }, { "items", "道具设定（未实现效果）" },
            { "names", "姓名池（新增示例）" }, { "origins", "原籍池（新增示例）" }
        };
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("运行时复制配置。修改后重新开局才生效；道具只保留设定。", MessageType.Info);
            var data = serializedObject.FindProperty("data");
            var iterator = data.Copy();
            var end = data.GetEndProperty();
            if (iterator.NextVisible(true))
                do
                {
                    if (SerializedProperty.EqualContents(iterator, end)) break;
                    string label;
                    Labels.TryGetValue(iterator.name, out label);
                    EditorGUILayout.PropertyField(iterator, new GUIContent(label ?? iterator.displayName), true);
                } while (iterator.NextVisible(false));
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("检查配置完整性"))
            {
                var errors = ConfigValidator.Validate(((GameDataConfig)target).data);
                if (errors.Count == 0) Debug.Log("游戏数据配置检查通过。", target);
                else Debug.LogError(string.Join("\n", errors), target);
            }
        }
    }

    public static class GameDataMenus
    {
        public const string DefaultAssetPath = "Assets/GameData/Config/DefaultGameDataConfig.asset";
        [MenuItem("Tools/JU/Game Data/创建策划默认配置副本")]
        public static void CreateDefaultCopy()
        {
            string path = EditorUtility.SaveFilePanelInProject("保存默认配置副本", "GameDataConfig", "asset", "选择新配置的保存位置");
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            { EditorUtility.DisplayDialog("路径已存在", "请选择新文件名，避免覆盖已有资产。", "确定"); return; }
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/GameData/Editor/Defaults.json");
            if (json == null) { Debug.LogError("未找到 Defaults.json。"); return; }
            var asset = ScriptableObject.CreateInstance<GameDataConfig>();
            asset.data = JsonUtility.FromJson<GameConfigData>(json.text);
            var errors = ConfigValidator.Validate(asset.data);
            if (errors.Count > 0) { Object.DestroyImmediate(asset); Debug.LogError(string.Join("\n", errors)); return; }
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }
    }
}
