using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace JU.GameData
{
    /// <summary>
    /// 每日事件配置 —— 在 Unity Inspector 里按秩序值/纯净值的区间各填一个事件列表，
    /// 每天随机从命中的区间里挑一条显示。列表为空则该区间不触发事件。
    /// 用法：Create → GameData → Daily Event Config → 填文本 → 拖给 GameSessionBridge.dailyEventConfig。
    /// </summary>
    [CreateAssetMenu(fileName = "DailyEventConfig", menuName = "GameData/Daily Event Config")]
    public class DailyEventConfig : ScriptableObject
    {
        [Header("秩序值事件 —— 按日结后的 order 值命中区间，每天从对应列表随机挑一条")]

        [Tooltip("order < 20（危险），列表里每条是一条独立事件文本")]
        public List<string> orderDanger = new List<string>();

        [Tooltip("order 20~30（临界低）")]
        public List<string> orderCriticalLow = new List<string>();

        [Tooltip("order 31~50（偏低）")]
        public List<string> orderLow = new List<string>();

        [Tooltip("order 51~80（正常）—— 留空列表表示不触发事件")]
        public List<string> orderNormal = new List<string>();

        [Tooltip("order > 80（高值）")]
        public List<string> orderHigh = new List<string>();

        [Header("纯净值事件 —— 按日结后的 purity 值命中区间，每天从对应列表随机挑一条")]

        [Tooltip("purity < 20（危险）")]
        public List<string> purityDanger = new List<string>();

        [Tooltip("purity 20~30（临界低）")]
        public List<string> purityCriticalLow = new List<string>();

        [Tooltip("purity 31~50（偏低）")]
        public List<string> purityLow = new List<string>();

        [Tooltip("purity 51~80（正常）—— 留空列表表示不触发事件")]
        public List<string> purityNormal = new List<string>();

        [Tooltip("purity > 80（高值）")]
        public List<string> purityHigh = new List<string>();

        // ---------------------------------------------------------------------------
        // 解析：命中区间 → 从对应 List 里随机挑一条返回；列表为空则返回 null
        // ---------------------------------------------------------------------------

        private static readonly System.Random _rng = new System.Random();

        private static string RandomPick(IList<string> list)
        {
            if (list == null || list.Count == 0) return null;
            // 跳过空白条目
            var valid = list.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (valid.Count == 0) return null;
            return valid[_rng.Next(valid.Count)];
        }

        /// <summary>根据秩序值命中区间，从该区间的事件列表里随机挑一条。无事件则返回 null。</summary>
        public string ResolveOrderEvent(float orderAfter)
        {
            float v = Mathf.Clamp(orderAfter, 0f, 100f);
            List<string> tier;
            if (v < 20f) tier = orderDanger;
            else if (v < 31f) tier = orderCriticalLow;
            else if (v < 51f) tier = orderLow;
            else if (v < 81f) tier = orderNormal;
            else tier = orderHigh;
            return RandomPick(tier);
        }

        /// <summary>根据纯净值命中区间，从该区间的事件列表里随机挑一条。无事件则返回 null。</summary>
        public string ResolvePurityEvent(float purityAfter)
        {
            float v = Mathf.Clamp(purityAfter, 0f, 100f);
            List<string> tier;
            if (v < 20f) tier = purityDanger;
            else if (v < 31f) tier = purityCriticalLow;
            else if (v < 51f) tier = purityLow;
            else if (v < 81f) tier = purityNormal;
            else tier = purityHigh;
            return RandomPick(tier);
        }

        /// <summary>拼接完整事件文本 —— 秩序事件 + 换行 + 纯净事件，任一为 null 则跳过。全无时返回"无新闻报道"。</summary>
        public string BuildDailyEventText(float orderAfter, float purityAfter)
        {
            var parts = new List<string>();
            string o = ResolveOrderEvent(orderAfter);
            string p = ResolvePurityEvent(purityAfter);
            if (!string.IsNullOrWhiteSpace(o)) parts.Add(o);
            if (!string.IsNullOrWhiteSpace(p)) parts.Add(p);
            return parts.Count == 0 ? "无新闻报道" : string.Join("\n\n", parts);
        }

        /// <summary>标题行：第 N 日 · 秩序 XX · 纯净 XX</summary>
        public static string BuildTitle(int day, float orderAfter, float purityAfter)
            => $"第 {day} 日   秩序 {Mathf.RoundToInt(orderAfter)}   纯净 {Mathf.RoundToInt(purityAfter)}";
    }
}
