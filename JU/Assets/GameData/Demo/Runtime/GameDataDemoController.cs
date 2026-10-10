using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace JU.GameData.Demo
{
    public class GameDataDemoController : MonoBehaviour
    {
        [Header("独立Demo配置，不连接原场景时钟")]
        public GameDataConfig configuration;
        public int randomSeed = 42;
        [Tooltip("可选：指定支持中文的字体。未指定时使用系统微软雅黑/黑体。")]
        public Font fontOverride;
        [Header("五个板块的UI引用")]
        public Text dayLabel, stateText, queueCountText;
        public Dropdown npcDropdown;
        public Text documentText, profileText, debugText, feedbackText;
        public Button utopiaButton, antiUtopiaButton, rejectButton, closeDayButton;
        public Text closeDayButtonLabel;

        private GameSession session;
        private List<string> selectableIds = new List<string>();
        private string selectedNpcId;
        private Font generatedFont;
        private bool refreshing;
        public GameState CurrentState { get { return session == null ? null : session.GetState(); } }
        public string SelectedNpcId { get { return selectedNpcId; } }

        private void Awake()
        {
            ApplyFont();
            npcDropdown.onValueChanged.AddListener(SelectNpc);
            utopiaButton.onClick.AddListener(() => Approve(Decision.ReleaseToUtopia));
            antiUtopiaButton.onClick.AddListener(() => Approve(Decision.ReleaseToAntiUtopia));
            rejectButton.onClick.AddListener(() => Approve(Decision.Reject));
            closeDayButton.onClick.AddListener(CloseDayOrRestart);
            StartNewSession();
        }
        public void StartNewSession()
        {
            try
            {
                if (configuration == null) throw new ArgumentException("请给 Demo Controller 指定 GameDataConfig 配置资产。");
                session = GameSession.StartNewGame(configuration.data, randomSeed);
                selectedNpcId = null;
                feedbackText.text = "新局已开始。请选择申请者，核对文书与档案后审批；闭馆时统一结算。";
                Refresh();
            }
            catch (Exception e)
            {
                session = null;
                dayLabel.text = "配置错误";
                stateText.text = "无法开始新局";
                feedbackText.text = e.Message;
                documentText.text = profileText.text = debugText.text = "请检查配置资产与UI引用。";
                npcDropdown.interactable = false;
                utopiaButton.interactable = antiUtopiaButton.interactable = rejectButton.interactable = false;
                closeDayButton.interactable = true;
                closeDayButtonLabel.text = "重试开局";
            }
        }
        public void Approve(Decision decision)
        {
            if (session == null || string.IsNullOrEmpty(selectedNpcId)) return;
            var selected = session.GetNpc(selectedNpcId);
            var result = session.SubmitDecision(selectedNpcId, decision);
            if (!result.success) { feedbackText.text = result.error; return; }
            if (result.approval == null)
                feedbackText.text = selected.name + " 已进入收容所。可以从人员列表再次选择；当日日终开始腐化。";
            else
                feedbackText.text = selected.name + " 已放行至" + (decision == Decision.ReleaseToUtopia ? "乌托邦" : "反乌托邦") +
                    " · " + (result.approval.compliant ? "合规" : "违规") + " · 世界影响 " + Signed(result.approval.worldImpact) + "（闭馆时计入）";
            // 审批后优先推进到下一个新申请者，避免把刚拒签的人立即重复审批。
            selectedNpcId = session.GetState().npcs.FirstOrDefault(n => n.status == NpcStatus.Pending)?.id;
            Refresh();
        }
        public void CloseDayOrRestart()
        {
            if (session == null || session.GetState().gameOver) { StartNewSession(); return; }
            int day = session.GetState().day;
            var report = session.EndDay(day);
            if (!report.success) { feedbackText.text = report.error; return; }
            selectedNpcId = null;
            feedbackText.text = "第 " + day + " 天结算：合规 " + report.compliantCount + " / 违规 " + report.violationCount +
                "；信任 " + Signed(report.trustChange) + "；纯净 " + Signed(report.approvalPurityChange) +
                " / 秩序 " + Signed(report.approvalOrderChange) + "；恢复 " + report.purityRecovery.ToString("0.0") +
                "/" + report.orderRecovery.ToString("0.0") + "；事件扣值 " + report.eventDamage.ToString("0.0") +
                "；新增变异 " + report.newlyMutatedIds.Count + (report.blackSwanStarted ? "。黑天鹅启动，次日日终开始。" : "。");
            if (report.after.gameOver) feedbackText.text += "\n游戏结束：" + string.Join(" / ", report.after.failureReasons);
            Refresh();
        }
        private void SelectNpc(int index)
        {
            if (refreshing || index < 0 || index >= selectableIds.Count) return;
            selectedNpcId = selectableIds[index];
            RefreshDetails();
        }
        private void Refresh()
        {
            var state = session.GetState();
            dayLabel.text = "第 " + state.day.ToString("D2") + " 天";
            stateText.text = "纯净 " + state.purity.ToString("0.0") + "     秩序 " + state.order.ToString("0.0") +
                "     信任 " + state.trust + "     " + (state.gameOver ? "游戏结束" : state.blackSwanRemaining > 0 ? "黑天鹅 · 剩余 " + state.blackSwanRemaining + " 次" : "两界正常");
            var candidates = state.npcs.Where(n => n.status == NpcStatus.Pending || n.status == NpcStatus.Sheltered)
                .OrderBy(n => n.status == NpcStatus.Pending ? 0 : 1).ThenBy(n => n.id).ToList();
            int pending = candidates.Count(n => n.status == NpcStatus.Pending);
            queueCountText.text = "待审批 " + pending + "    收容 " + (candidates.Count - pending) + "    已放行 " + state.todayApprovals.Count;
            selectableIds = candidates.Select(n => n.id).ToList();
            if (!selectableIds.Contains(selectedNpcId)) selectedNpcId = selectableIds.FirstOrDefault();
            refreshing = true;
            npcDropdown.ClearOptions();
            npcDropdown.AddOptions(candidates.Count == 0 ? new List<string> { "今日申请已处理完毕" } :
                candidates.Select(n => (n.status == NpcStatus.Pending ? "待审批" : "收容") + " · " + n.name + " · " + n.id + (n.mutated ? " [变异]" : "")).ToList());
            npcDropdown.SetValueWithoutNotify(Math.Max(0, selectableIds.IndexOf(selectedNpcId)));
            npcDropdown.RefreshShownValue();
            refreshing = false;
            npcDropdown.interactable = candidates.Count > 0 && !state.gameOver;
            closeDayButtonLabel.text = state.gameOver ? "重新开局" : "闭馆结算 / 次日";
            closeDayButton.interactable = true;
            RefreshDetails();
        }
        private void RefreshDetails()
        {
            var state = session.GetState();
            var npc = session.GetNpc(selectedNpcId);
            bool canApprove = npc != null && !state.gameOver;
            utopiaButton.interactable = antiUtopiaButton.interactable = canApprove && !npc.mutated;
            rejectButton.interactable = canApprove && npc.status == NpcStatus.Pending;
            if (npc == null)
            {
                documentText.text = "今日没有待审批人员。\n\n请点击闭馆结算，进入下一天。";
                profileText.text = "暂无人物档案。\n\n收容人员会在日终后保留。";
                debugText.text = "暂无当前NPC。\n\n世界变化在闭馆时结算。";
                return;
            }
            var p = session.GetProfile(npc.id);
            var d = session.GetDocument(npc.id);
            documentText.text = "申请编号   " + d.id + "\n\n姓名   " + d.name + "\n原籍   " + d.declaredOrigin +
                "\n申请目的地   " + (d.requestedDestination == Destination.Utopia ? "乌托邦" : "反乌托邦") +
                "\n\n【身体状况】\n" + d.bodyText + "\n\n【入境理由】\n" + d.reasonText;
            profileText.text = "真实身份   " + p.name + " / " + p.realOrigin + "\n真实动机   " + p.motive +
                "\n\n【羁绊溯源】 " + p.bondTag.name + "\n" + p.bondTag.description +
                "\n\n【驱力评估】 " + p.driveTag.name + "\n" + p.driveTag.description +
                "\n\n" + p.memoryText + "\n" + p.relationshipText + "\n" + p.desireText + "\n" + p.willText +
                "\n\n" + p.bodyText + (p.mutated ? "\n变异：禁止正常放行" : "");
            debugText.text = "记忆    " + Signed(npc.attributes.memory) + "\n关系    " + Signed(npc.attributes.relationship) +
                "\n欲望    " + Signed(npc.attributes.desire) + "\n意志    " + Signed(npc.attributes.will) +
                "\n\n偏差    " + npc.deviation + "\n伪装    " + (npc.deception == Deception.Honest ? "诚实" : npc.deception == Deception.Embellished ? "粉饰" : "欺骗") +
                "\n状态    " + (npc.mutated ? "变异" : npc.status == NpcStatus.Sheltered ? "收容" : "待审批") +
                "\n\n【预计放行影响】\n乌托邦    " + Signed(GameSession.CalculateImpact(npc.attributes, Destination.Utopia)) +
                "\n反乌托邦    " + Signed(GameSession.CalculateImpact(npc.attributes, Destination.AntiUtopia)) +
                "\n\n影响 ≥ 0：合规\n影响 < 0：违规";
        }
        private static string Signed(float value) { return value.ToString("+0.0;-0.0;0.0"); }
        private void ApplyFont()
        {
            Font font = fontOverride;
            if (font == null)
            {
                generatedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" }, 20);
                font = generatedFont;
            }
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var text in GetComponentsInChildren<Text>(true)) text.font = font;
        }
        private void OnDestroy()
        {
            if (generatedFont != null) Destroy(generatedFont);
        }
    }
}
