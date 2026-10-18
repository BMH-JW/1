using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JU.GameData.Editor
{
    public class GameDataDebugWindow : EditorWindow
    {
        [SerializeField] private GameDataConfig config;
        [SerializeField] private int seed = 42;
        private GameSession session;
        private GameConfigData runningConfig;
        private GameDataConfig runningAsset;
        private int tab, selectedIndex, npcFilter, simulationDays = 30;
        private bool showSecrets;
        private Vector2 scroll;
        private string lastMessage = "检查运行条件后，用配置开始测试局。默认资产已包含本轮策划答复和明确标记的测试样本。";
        private List<string> issues = new List<string>();
        private DayEndReport lastReport;
        private List<SimulationReport> simulations = new List<SimulationReport>();
        [MenuItem("Tools/JU/Game Data/数据调试窗口")]
        public static void Open() { var w = GetWindow<GameDataDebugWindow>("彼方数据调试"); w.minSize = new Vector2(680, 500); }
        private void OnEnable() { if (config == null) config = AssetDatabase.LoadAssetAtPath<GameDataConfig>(GameDataMenus.DefaultAssetPath); }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("独立内存模拟，无需Play，不接管原场景。当天申请者只生成一次；关闭窗口或重编译后进度丢失。", MessageType.Info);
            config = (GameDataConfig)EditorGUILayout.ObjectField("下次开局配置", config, typeof(GameDataConfig), false);
            seed = EditorGUILayout.IntField("下次开局/模拟种子", seed);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(config == null))
            {
                if (GUILayout.Button("检查运行条件")) { issues = ConfigValidator.ValidateForPlay(config.data); lastMessage = issues.Count == 0 ? "运行检查通过；测试配置不等于正式发布确认。" : "存在" + issues.Count + "项运行问题。"; tab = 0; }
                if (GUILayout.Button("检查正式交付条件")) { issues = GameDataMenus.ReleaseIssues(config); lastMessage = issues.Count == 0 ? "正式检查通过。" : "正式交付仍有" + issues.Count + "项问题，见配置页。"; tab = 0; }
                if (GUILayout.Button("开始测试局")) StartSession();
                if (GUILayout.Button("定位配置")) { Selection.activeObject = config; EditorGUIUtility.PingObject(config); }
            }
            EditorGUILayout.EndHorizontal();
            if (session != null)
            {
                var s = session.GetState();
                EditorGUILayout.HelpBox((runningConfig.testConfiguration ? "测试配置局（非最终平衡）" : "配置局") + " / " + runningConfig.version + " / 本局种子" + s.seed + " / 第" + s.day + "天" +
                    "\n今日生成=" + s.todayApplicantCount + "；已处理=" + s.todayApprovals.Count + "；信任=" + s.trust +
                    "；" + session.GetWorldName(Destination.Utopia) + "=" + s.purity + "；" + session.GetWorldName(Destination.AntiUtopia) + "=" + s.order + "（实际值，日结才改变）", MessageType.Info);
            }
            Text("最近操作", lastMessage);
            tab = GUILayout.Toolbar(tab, new[] { "配置检查", "当日规则", "申请者/审批", "记录/日结", "次日反馈", "多日模拟" });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (tab == 0) DrawConfig();
            else if (tab == 5) DrawSimulation();
            else if (session == null) EditorGUILayout.HelpBox("请先开始测试局。", MessageType.Info);
            else if (tab == 1) Text("通讯机公告（与后台规则同源）", session.GetRuleAnnouncement());
            else if (tab == 2) DrawApplicants();
            else if (tab == 3) DrawSettlement();
            else DrawFeedback();
            EditorGUILayout.EndScrollView();
        }
        private void StartSession()
        {
            try
            {
                var next = GameSession.StartNewGame(config.data, seed);
                session = next; runningAsset = config; runningConfig = next.GetConfigSnapshot();
                session.Diagnostic += message => Debug.Log("[GameData规则] " + message);
                selectedIndex = 0; lastReport = null; issues.Clear(); showSecrets = false;
                lastMessage = "测试局已创建。人物类型/动机由样本指定；调查与扣留均未启用。修改资产后需重新开局。"; tab = 1;
            }
            catch (Exception ex) { lastMessage = "开局失败，原局保留：" + ex.Message; issues = ConfigValidator.ValidateForPlay(config.data); tab = 0; }
        }
        private void DrawConfig()
        {
            if (config == null) return;
            var c = runningConfig ?? config.data;
            Text(runningConfig == null ? "所选资产" : "当前局快照（更改输入不影响当前局）", c.version + "\n原表：" + c.sourceWorkbook + "\n答复：" + c.replyDocument + "\n答复SHA256：" + c.replySha256);
            foreach (var e in issues) EditorGUILayout.HelpBox(e, MessageType.Error);
            if (issues.Count > 0 && GUILayout.Button("复制检查问题")) EditorGUIUtility.systemCopyBuffer = string.Join("\n", issues);
            Text("本轮采用的实现约定", string.Join("\n", c.implementationDecisions));
            Text("仍待完善但不阻止测试的事项", string.Join("\n", c.pendingDecisions));
            Text("公式状态", string.Join("\n", c.formulas.Select(f => f.id + " [" + (f.enabled ? "开启" : "关闭") + (f.testOnly ? " / 测试" : "") + "] " + f.description)));
        }
        private void DrawApplicants()
        {
            var s = session.GetState();
            npcFilter = EditorGUILayout.Popup("筛选", npcFilter, new[] { "今日全部", "待处理", "已放行", "已拒签" });
            var ns = s.npcs.Where(n => npcFilter == 0 || (npcFilter == 1 && n.status == NpcStatus.Pending) || (npcFilter == 2 && n.status == NpcStatus.Released) || (npcFilter == 3 && n.status == NpcStatus.Rejected)).ToList();
            if (ns.Count == 0) { Text("列表", "当前筛选下没有申请者。"); return; }
            selectedIndex = Mathf.Clamp(selectedIndex, 0, ns.Count - 1);
            selectedIndex = EditorGUILayout.Popup("申请者", selectedIndex, ns.Select(n => n.name + " / " + n.id + " / " + n.status).ToArray());
            var n = ns[selectedIndex]; var doc = session.GetDocument(n.id); var p = session.GetPlayerProfile(n.id);
            Text("通关文书", doc == null ? "文书生成关闭。" : doc.name + "\n原籍：" + doc.declaredOrigin + "\n来源：" + doc.sourceDisplayName + "；申报：" + doc.destinationDisplayName + "\n" + doc.bodyText + "\n" + doc.reasonText);
            Text(runningConfig.bondTraceLabel, p.bondTraceSentence); Text(runningConfig.driveAssessmentLabel, p.driveAssessmentSentence);
            Text("本日相关维度", string.Join("、", p.relevantAttributes));
            using (new EditorGUI.DisabledScope(n.status != NpcStatus.Pending))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("按申报放行至" + session.GetWorldName(n.requestedDestination))) Approve(n.id, true);
                if (GUILayout.Button("拒签（结束本次申请）")) Approve(n.id, false);
                EditorGUILayout.EndHorizontal();
            }
            showSecrets = EditorGUILayout.Foldout(showSecrets, "开发者检查（包含真实属性与审批答案）", true);
            if (!showSecrets) return;
            Text("稳定人物身份与立绘", n.identityId + " / " + n.portraitKey + "\n相同人物跨日保持姓名、原籍和立绘Key；申请ID与属性可变化。");
            if (runningAsset != null)
            {
                try { using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("绑定立绘", runningAsset.ResolvePortrait(n.portraitKey), typeof(Sprite), false); }
                catch (InvalidOperationException ex) { EditorGUILayout.HelpBox(ex.Message + "。测试占位不会随机替代成其他图片。", MessageType.Warning); }
            }
            var hidden = session.GetProfile(n.id); var evaluation = session.PreviewRule(n.id);
            Text("当前四属性", "记忆=" + n.attributes.memory + "；关系=" + n.attributes.relationship + "；欲望=" + n.attributes.desire + "；意志=" + n.attributes.will);
            Text("样本显式信息", "申报类型=" + n.deception + "；真实动机=" + hidden.motive + "；F003未运行。");
            Text("规则判断", evaluation.success ? (evaluation.applicable ? "记忆目标=" + session.GetChoiceName(evaluation.target) + "；当前应执行=" + session.GetChoiceName(evaluation.requiredDecision) : "规则判定关闭") + "\n" + string.Join("\n", evaluation.traces) : evaluation.error);
            Text("身体真实状态", hidden.bodyText);
        }
        private void Approve(string id, bool approve)
        { var r = session.SubmitApplication(id, approve); lastMessage = r.success ? r.immediateFeedback + " 世界和信任等待日结。" : r.error; }
        private void DrawSettlement()
        {
            var s = session.GetState();
            Text("今天的记录（开发核对）", s.todayApprovals.Count == 0 ? "暂无审批记录。" : string.Join("\n", s.todayApprovals.Select(a => a.id + " / " + a.npcName + " / " + session.GetChoiceName(a.decision) + " / " + (!a.complianceEvaluated ? "未评定" : a.compliant ? "合规" : "违规") + " / 两界待结算=" + a.purityDelta + "/" + a.orderDelta)));
            if (GUILayout.Button("结算当前日并进入次日"))
            {
                lastReport = session.EndDay(s.day);
                lastMessage = lastReport.success ? "第" + lastReport.settledDay + "天已结算；次日申请者已生成并保存。" : lastReport.error;
                selectedIndex = 0;
            }
            if (lastReport == null) return;
            if (!lastReport.success) { Text("结算问题", lastReport.error); return; }
            Text("日结报告", lastReport.settlementId + "\n" + string.Join("\n", lastReport.log));
            Text("结算前 → 后", "灵虚：" + lastReport.before.purity + " → " + lastReport.after.purity + "\n废墟：" + lastReport.before.order + " → " + lastReport.after.order + "\n信任：" + lastReport.before.trust + " → " + lastReport.after.trust);
            if (GUILayout.Button("复制本次报告与判定原因")) EditorGUIUtility.systemCopyBuffer = runningConfig.version + " / 种子" + lastReport.before.seed + "\n" + string.Join("\n", lastReport.log) + "\n" + string.Join("\n", lastReport.approvals.Select(a => a.id + " / " + a.npcName + "\n" + a.ruleTrace));
            if (GUILayout.Button("检查重复日结拦截")) { var r = session.EndDay(lastReport.settledDay); lastMessage = r.success ? "异常：重复日结未被拦截。" : r.error; }
        }
        private void DrawFeedback()
        {
            var s = session.GetState(); Text("说明", "反馈仅在日结成功后入队，次日到期；正式叙事尚未补齐，当前为有真实审批来源的调试事实。按钮模拟播放完成确认。");
            if (s.feedbackQueue.Count == 0) Text("反馈", "暂无反馈，先审批并日结。");
            foreach (var f in s.feedbackQueue)
            {
                Text(f.id + " / " + f.templateId, "来源=" + f.sourceDecisionId + " / " + f.settlementId + "；到期日=" + f.availableDay + "；" + (f.consumed ? "已消费" : f.availableDay <= s.day ? "可播放" : "等待") + "\n" + f.text);
                using (new EditorGUI.DisabledScope(f.consumed || f.availableDay > s.day)) if (GUILayout.Button("模拟播放完成 " + f.id)) lastMessage = session.ConsumeFeedback(f.id) ? "已消费，不会重复播放。" : "不可消费。";
            }
        }
        private void DrawSimulation()
        {
            Text("独立多日测试", "比较按规则处理、全部放行、全部拒签三种策略。相同种子使用相同的每日申请者序列；不修改当前局或资产。最终信任/世界值与边界天数用于观察测试平衡。");
            simulationDays = Mathf.Clamp(EditorGUILayout.IntField("模拟天数（1～365）", simulationDays), 1, 365);
            using (new EditorGUI.DisabledScope(config == null && runningConfig == null)) if (GUILayout.Button("比较三种策略"))
            {
                try
                {
                    var c = runningConfig ?? config.data;
                    var reports = new List<SimulationReport>();
                    foreach (SimulationPolicy policy in Enum.GetValues(typeof(SimulationPolicy))) reports.Add(SimulationRunner.Run(c, seed, simulationDays, policy));
                    simulations = reports; lastMessage = "三种策略各" + simulationDays + "天模拟完成。";
                }
                catch (Exception ex) { lastMessage = "模拟失败：" + ex.Message; }
            }
            foreach (var report in simulations) Text(report.policy.ToString(), report.Summary());
            using (new EditorGUI.DisabledScope(simulations.Count == 0)) if (GUILayout.Button("导出多日对比CSV"))
            {
                string path = EditorUtility.SaveFilePanel("导出模拟报告", "", "GameDataSimulation.csv", "csv");
                if (!string.IsNullOrEmpty(path)) { File.WriteAllText(path, SimulationRunner.ToCsv(simulations), new UTF8Encoding(true)); lastMessage = "模拟报告已保存：" + path; }
            }
        }
        private static void Text(string label, string content)
        { EditorGUILayout.LabelField(label, EditorStyles.boldLabel); EditorGUILayout.LabelField(content ?? "", EditorStyles.wordWrappedLabel); EditorGUILayout.Space(4); }
    }
}
