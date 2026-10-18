using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JU.GameData.Editor
{
    [CustomEditor(typeof(GameDataConfig))]
    public class GameDataConfigEditor : UnityEditor.Editor
    {
        private readonly bool[] expanded = new bool[9];
        private static readonly string[][] Groups = {
            new[] { "版本与实现约定", "version|配置版本", "sourceWorkbook|原始表格", "sourceSha256|表格SHA256", "replyDocument|策划答复", "replySha256|答复SHA256", "testConfiguration|测试配置（不等于最终平衡）", "implementationDecisions|已采用的实现约定", "pendingDecisions|仍待补充事项（不阻塞基础测试）" },
            new[] { "每日流程与世界初值", "startDay|起始天数", "minDailyApplicants|每日人数下限", "maxDailyApplicants|每日人数上限", "worlds|世界ID与名称（已确认）", "initialPurity|灵虚初始纯净值", "initialOrder|废墟初始秩序值", "worldValueMin|世界下限0", "worldValueMax|世界上限100", "initialTrust|信任初值", "trustMin|信任下限0", "trustMax|信任上限100" },
            new[] { "记忆准入规则", "dailyRuleCount|每日规则数量", "conflictPolicy|多规则策略", "rules|规则库（本版仅记忆）", "semantics|等级与属性含义（记忆方向相反）", "showImmediateCompliance|即时展示合规（默认关闭，仅回执）" },
            new[] { "人物身份与原籍立绘", "identities|人物身份（姓名同日不重复，类型及动机显式指定）", "portraitPools|原籍对应立绘Key池（首次选定后跨日保持）" },
            new[] { "属性生成与档案资料", "bands|表1 数值到等级", "generationWeights|表6 等级权重", "bondTraceLabel|羁绊牵挂显示名称", "driveAssessmentLabel|驱力等级显示名称", "tags|二维描述50条", "details|一维明细25条", "motives|真实动机资料（不自动竞争优先级）", "documents|文书17条（含策划六类通用文案）", "items|保留的道具资料（不执行）" },
            new[] { "测试公式与结算", "formulas|公式开关（F003与旧机制不可启用）", "impactDivisor|F004/F005记忆除数（测试5）", "rejectionSourcePenalty|拒签来源世界扣除（1）", "complianceReward|F013合规处理奖励（测试3，含拒签）", "violationPenalty|F013违规处理扣除（测试5，含拒签）" },
            new[] { "运行开关与保留阈值", "enableDailyRules|每日规则判定", "enableBondTrace|羁绊牵挂生成", "enableDriveAssessment|驱力等级生成", "enableDocuments|文书生成", "enableWorldChanges|世界影响总开关", "enableDelayedFeedback|次日反馈", "enablePopulationFeedback|权重反馈（未定，不可启用）", "enableThresholdEvents|阈值事件（未定，不可启用）", "lightFeedbackThresholds|45/55轻量阈值资料", "formalEventThresholds|40/60正式事件阈值资料" },
            new[] { "次日反馈模板", "feedbackDelayDays|日结日起延迟天数（1表示次日）", "feedbackTemplates|按真实审批匹配的反馈模板（当前调试事实）" },
            new[] { "开发日志", "debugMode|开发模式", "logRuleResult|输出规则日志（需订阅Diagnostic）" }
        };
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("本轮是可运行的策划答复测试配置。调查已移除、扣留关闭；拒签不会进入收容。修改资产后重新开局。", MessageType.Info);
            var data = serializedObject.FindProperty("data");
            for (int i = 0; i < Groups.Length; i++)
            {
                expanded[i] = EditorGUILayout.Foldout(expanded[i], Groups[i][0], true);
                if (!expanded[i]) continue;
                EditorGUI.indentLevel++;
                foreach (var item in Groups[i].Skip(1))
                {
                    var parts = item.Split('|'); var field = data.FindPropertyRelative(parts[0]);
                    if (field != null) EditorGUILayout.PropertyField(field, new GUIContent(parts[1]), true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("portraitBindings"), new GUIContent("立绘Key到Sprite绑定（队友拖入实际图片）"), true);
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("检查结构")) Report(ConfigValidator.Validate(((GameDataConfig)target).data));
            if (GUILayout.Button("检查测试运行条件")) Report(ConfigValidator.ValidateForPlay(((GameDataConfig)target).data));
            if (GUILayout.Button("检查正式交付条件（含立绘资源）")) Report(GameDataMenus.ReleaseIssues((GameDataConfig)target));
            if (GUILayout.Button("打开数据调试窗口")) GameDataDebugWindow.Open();
            if (GUILayout.Button("打开使用说明")) GameDataMenus.OpenReadme();
        }
        private void Report(List<string> errors)
        { if (errors.Count == 0) Debug.Log("检查通过。", target); else Debug.LogError(string.Join("\n", errors), target); }
    }
    public static class GameDataMenus
    {
        public const string DefaultAssetPath = "Assets/GameData/Config/DefaultGameDataConfig.asset";
        public static List<string> ReleaseIssues(GameDataConfig asset)
        {
            var e = ConfigValidator.ValidateForRelease(asset.data);
            if (asset.data == null || asset.data.portraitPools == null) return e;
            if (asset.portraitBindings == null) { e.Add("Sprite绑定列表不能为空。"); return e; }
            foreach (string key in asset.data.portraitPools.Where(p => p != null && p.portraitKeys != null).SelectMany(p => p.portraitKeys).Distinct())
                if (asset.portraitBindings.Count(b => b != null && b.key == key && b.sprite != null) != 1 || asset.portraitBindings.Count(b => b != null && b.key == key) != 1)
                    e.Add("立绘缺少唯一有效Sprite绑定：" + key);
            return e;
        }
        [MenuItem("Tools/JU/Game Data/创建当前测试基线副本")]
        public static void CreateDefaultCopy()
        {
            string path = EditorUtility.SaveFilePanelInProject("保存测试基线副本", "GameDataConfig", "asset", "使用最新Defaults.json，不覆盖现有资产。");
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) { Debug.LogError("路径已存在，未覆盖。"); return; }
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/GameData/Editor/Defaults.json");
            if (json == null) { Debug.LogError("缺少Defaults.json。"); return; }
            var data = JsonUtility.FromJson<GameConfigData>(json.text); var errors = ConfigValidator.ValidateForPlay(data);
            if (errors.Count > 0) { Debug.LogError(string.Join("\n", errors)); return; }
            var copy = ScriptableObject.CreateInstance<GameDataConfig>(); copy.data = data;
            AssetDatabase.CreateAsset(copy, path); AssetDatabase.SaveAssets(); Selection.activeObject = copy;
        }
        [MenuItem("Tools/JU/Game Data/打开使用说明")]
        public static void OpenReadme() { EditorUtility.OpenWithDefaultApp(System.IO.Path.Combine(Application.dataPath, "GameData/README.md")); }
    }
}
