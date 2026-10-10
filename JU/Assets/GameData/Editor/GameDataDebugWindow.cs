using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JU.GameData.Editor
{
    public class GameDataDebugWindow : EditorWindow
    {
        private GameDataConfig config;
        private GameSession session;
        private int seed = 42, selectedIndex;
        private Vector2 scroll;
        private string lastMessage = "选择配置并点击开始新局。";
        [MenuItem("Tools/JU/Game Data/数据调试窗口")]
        public static void Open() { GetWindow<GameDataDebugWindow>("游戏数据调试"); }
        private void OnEnable()
        {
            config = AssetDatabase.LoadAssetAtPath<GameDataConfig>(GameDataMenus.DefaultAssetPath);
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("独立数据模拟，不修改场景、不接管现有时钟。窗口或脚本重载后需要重新开局。", MessageType.Info);
            config = (GameDataConfig)EditorGUILayout.ObjectField("配置资产", config, typeof(GameDataConfig), false);
            seed = EditorGUILayout.IntField("随机种子", seed);
            using (new EditorGUI.DisabledScope(config == null))
                if (GUILayout.Button("开始新局"))
                {
                    try { session = GameSession.StartNewGame(config.data, seed); selectedIndex = 0; lastMessage = "新局已生成。"; }
                    catch (Exception e) { lastMessage = e.Message; }
                }
            if (session == null) { EditorGUILayout.HelpBox(lastMessage, MessageType.Info); return; }
            var state = session.GetState();
            EditorGUILayout.LabelField("第 " + state.day + " 天", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("世界", "纯净 " + state.purity.ToString("0.0") + " / 秩序 " + state.order.ToString("0.0"));
            EditorGUILayout.LabelField("信任 / 黑天鹅", state.trust + " / 剩余 " + state.blackSwanRemaining + " 次");
            EditorGUILayout.LabelField("队列", "待审批 " + state.npcs.Count(n => n.status == NpcStatus.Pending) + " / 收容 " + state.npcs.Count(n => n.status == NpcStatus.Sheltered));
            if (state.gameOver) EditorGUILayout.HelpBox(string.Join("\n", state.failureReasons), MessageType.Error);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var npcs = state.npcs.Where(n => n.status == NpcStatus.Pending || n.status == NpcStatus.Sheltered).ToList();
            if (npcs.Count > 0)
            {
                selectedIndex = Mathf.Clamp(selectedIndex, 0, npcs.Count - 1);
                selectedIndex = EditorGUILayout.Popup("选择NPC", selectedIndex, npcs.Select(n => n.id + " " + n.name + " [" + (n.status == NpcStatus.Sheltered ? "收容" : "待审批") + (n.mutated ? " / 变异" : "") + "]").ToArray());
                var n = npcs[selectedIndex];
                var profile = session.GetProfile(n.id);
                var doc = session.GetDocument(n.id);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("通关文书（申报）", EditorStyles.boldLabel);
                Text("身份", doc.name + " / " + doc.declaredOrigin);
                Text("目的地", doc.requestedDestination == Destination.Utopia ? "乌托邦" : "反乌托邦");
                Text("身体状况", doc.bodyText); Text("入境理由", doc.reasonText);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("人物档案（真实）", EditorStyles.boldLabel);
                Text("身份", profile.name + " / " + profile.realOrigin);
                Text("动机", profile.motive);
                Text("羁绊溯源", profile.bondTag.name + "：" + profile.bondTag.description);
                Text("驱力评估", profile.driveTag.name + "：" + profile.driveTag.description);
                Text("明细", profile.memoryText + "\n" + profile.relationshipText + "\n" + profile.desireText + "\n" + profile.willText);
                Text("真实身体", profile.bodyText);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("调试数值（不用于玩家展示）", EditorStyles.boldLabel);
                Text("四属性", "记忆 " + n.attributes.memory + " / 关系 " + n.attributes.relationship + " / 欲望 " + n.attributes.desire + " / 意志 " + n.attributes.will);
                Text("欺骗", n.deception + " / 偏差 " + n.deviation);
                Text("预计影响", "乌托邦 " + GameSession.CalculateImpact(n.attributes, Destination.Utopia).ToString("0.0") + " / 反乌托邦 " + GameSession.CalculateImpact(n.attributes, Destination.AntiUtopia).ToString("0.0"));
                using (new EditorGUI.DisabledScope(state.gameOver))
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("放行乌托邦")) Approve(n.id, Decision.ReleaseToUtopia);
                    if (GUILayout.Button("放行反乌托邦")) Approve(n.id, Decision.ReleaseToAntiUtopia);
                    if (GUILayout.Button("拒签入所")) Approve(n.id, Decision.Reject);
                    EditorGUILayout.EndHorizontal();
                }
            }
            using (new EditorGUI.DisabledScope(state.gameOver))
                if (GUILayout.Button("闭馆结算并进入次日"))
                {
                    var report = session.EndDay(state.day);
                    lastMessage = report.success ? "第" + report.settledDay + "天结算：合规 " + report.compliantCount + "，违规 " + report.violationCount +
                        "；信任变化 " + report.trustChange + "；放行纯净变化 " + report.approvalPurityChange.ToString("0.0") +
                        "，秩序变化 " + report.approvalOrderChange.ToString("0.0") +
                        "；恢复 " + report.purityRecovery.ToString("0.0") + "/" + report.orderRecovery.ToString("0.0") +
                        "；黑天鹅扣值 " + report.eventDamage.ToString("0.0") + "；新增变异 " + report.newlyMutatedIds.Count +
                        (report.blackSwanStarted ? "；黑天鹅启动，次日日终开始扣值。" : "") : report.error;
                    selectedIndex = 0;
                }
            EditorGUILayout.HelpBox(lastMessage, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        private static void Text(string label, string value)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(value ?? "", EditorStyles.wordWrappedLabel);
        }
        private void Approve(string id, Decision decision)
        {
            var result = session.SubmitDecision(id, decision);
            lastMessage = !result.success ? result.error : result.approval == null ? "已进入收容所。" :
                "已放行。" + (result.approval.compliant ? "合规" : "违规") + "，世界变化 " + result.approval.worldImpact.ToString("0.0") + "，闭馆时汇总。";
        }
    }
}
