using System;
using System.Collections.Generic;
using System.Linq;

namespace JU.GameData
{
    public static class ConfigValidator
    {
        private static readonly string[] Axes = { "memory", "relationship", "desire", "will" };
        public static List<string> Validate(GameConfigData c)
        {
            var errors = new List<string>();
            if (c == null) { errors.Add("配置为空。"); return errors; }
            if (c.bands == null || c.tags == null || c.details == null || c.motives == null ||
                c.documents == null || c.items == null || c.names == null || c.origins == null)
            { errors.Add("配置列表不能为 null。"); return errors; }
            if (c.bands.Any(x => x == null) || c.tags.Any(x => x == null) || c.details.Any(x => x == null) ||
                c.motives.Any(x => x == null) || c.documents.Any(x => x == null) || c.items.Any(x => x == null))
            { errors.Add("配置条目不能为 null。"); return errors; }
            if (c.startDay < 1 || c.dailyApplicants < 1 || c.dailyApplicants > 1000)
                errors.Add("起始天数须≥1，每日人数须为1～1000。");
            if (c.initialTrust <= 0) errors.Add("初始信任必须大于0。");
            float[] numbers = { c.initialPurity, c.initialOrder, c.recoveryCeiling, c.dailyRecovery,
                c.blackSwanThreshold, c.worldFailureThreshold, c.blackSwanDamage };
            if (numbers.Any(x => float.IsNaN(x) || float.IsInfinity(x))) errors.Add("数值必须为有限数字。");
            if (c.initialPurity <= c.worldFailureThreshold || c.initialOrder <= c.worldFailureThreshold)
                errors.Add("初始世界数值必须高于失败阈值。");
            if (c.worldFailureThreshold >= c.blackSwanThreshold || c.blackSwanThreshold >= c.recoveryCeiling)
                errors.Add("需满足：失败阈值 < 黑天鹅阈值 < 恢复上限。");
            if (c.blackSwanDays < 1 || c.blackSwanDamage < 0 || c.dailyRecovery < 0)
                errors.Add("事件天数必须≥1，扣值与恢复值不能为负。");
            int[] positiveParameters = { c.complianceReward, c.violationPenalty, c.dailyTrustBonus,
                c.memoryDecay, c.relationshipDecay, c.desireGrowth, c.willGrowth,
                c.blackSwanExtraDesire, c.blackSwanExtraWill };
            if (positiveParameters.Any(x => x < 0 || x > 100000)) errors.Add("变化幅度须为0～100000。");
            if (c.honestMaxDeviation < 0 || c.embellishedMaxDeviation <= c.honestMaxDeviation)
                errors.Add("欺骗分段阈值必须递增且非负。");
            if (c.bands.Count != 5 || c.bands.Select(x => x.level).Distinct().Count() != 5 ||
                c.bands.Any(x => x.level < 1 || x.level > 5)) errors.Add("需有唯一的1～5级属性区间。");
            foreach (var b in c.bands)
                if (b.minValue < -10 || b.maxValue > 10 || b.minValue > b.maxValue ||
                    b.weight <= 0 || float.IsNaN(b.weight) || float.IsInfinity(b.weight)) errors.Add("属性区间或权重无效。");
            for (int value = -10; value <= 10; value++)
                if (c.bands.Count(x => value >= x.minValue && value <= x.maxValue) != 1)
                    errors.Add("属性数值 " + value + " 必须恰好属于一个区间。");
            if (Math.Abs(c.bands.Sum(x => (double)x.weight) - 1) > 0.0001) errors.Add("生成权重总和必须为1。");
            UniqueIds(c.tags.Select(x => x.id), "标签", errors);
            UniqueIds(c.details.Select(x => x.id), "明细", errors);
            UniqueIds(c.motives.Select(x => x.id), "动机", errors);
            UniqueIds(c.documents.Select(x => x.id), "文书", errors);
            if (c.items.Select(x => x.id).Distinct().Count() != c.items.Count || c.items.Any(x => string.IsNullOrWhiteSpace(x.id)))
                errors.Add("道具编号为空或重复。");
            foreach (var pair in new[] { new[] { "memory", "relationship" }, new[] { "desire", "will" } })
                for (int x = 1; x <= 5; x++) for (int y = 1; y <= 5; y++)
                    if (c.tags.Count(t => t.axisX == pair[0] && t.axisY == pair[1] && t.xLevel == x && t.yLevel == y) != 1)
                        errors.Add("二维标签缺失或重复：" + pair[0] + "/" + x + "/" + y);
            if (c.tags.Count != 50 || c.tags.Any(x => string.IsNullOrWhiteSpace(x.name))) errors.Add("应有50个非空标签。");
            foreach (var axis in Axes.Concat(new[] { "desire_body" })) for (int level = 1; level <= 5; level++)
                if (c.details.Count(x => x.attribute == axis && x.level == level) != 1)
                    errors.Add("明细缺失或重复：" + axis + "/" + level);
            if (c.details.Count != 25 || c.details.Any(x => string.IsNullOrWhiteSpace(x.text))) errors.Add("应有25条非空明细。");
            foreach (var axis in Axes) foreach (int level in new[] { 1, 5 })
                if (c.motives.Count(x => x.attribute == axis && x.level == level) != 1) errors.Add("极端动机缺失或重复。");
            if (c.motives.Count(x => x.attribute == "any") != 1 || c.motives.Count != 9 ||
                c.motives.Any(x => string.IsNullOrWhiteSpace(x.name)) || c.motives.Select(x => x.name).Distinct().Count() != c.motives.Count)
                errors.Add("需8个极端动机和1个普通动机，名称必须唯一且非空。");
            var docKeys = c.documents.Select(x => x.field + "/" + x.motive + "/" + x.destination + "/" + x.triggerType);
            if (docKeys.Distinct().Count() != c.documents.Count) errors.Add("文书匹配键重复。");
            if (c.documents.Any(x => (x.field != "body_state" && x.field != "entry_reason") ||
                (x.destination != "any" && x.destination != "utopia" && x.destination != "anti_utopia") ||
                (x.motive != "any" && !c.motives.Any(m => m.name == x.motive)) ||
                x.triggerType < 0 || x.triggerType > 2 || string.IsNullOrWhiteSpace(x.text))) errors.Add("文书字段、条件或内容无效。");
            foreach (var motive in c.motives) foreach (var destination in new[] { "utopia", "anti_utopia" })
                for (int type = 0; type <= 2; type++) foreach (var field in new[] { "body_state", "entry_reason" })
                    if (!c.documents.Any(x => x.field == field && x.triggerType == type &&
                        (x.motive == "any" || x.motive == motive.name) && (x.destination == "any" || x.destination == destination)))
                        errors.Add("文书未覆盖：" + field + "/" + motive.name + "/" + destination + "/" + type);
            if (!c.names.Any() || c.names.Any(string.IsNullOrWhiteSpace)) errors.Add("姓名池不能为空或包含空文字。");
            if (c.origins.Distinct().Count() < 2 || c.origins.Any(string.IsNullOrWhiteSpace)) errors.Add("需至少两个不同的非空原籍。");
            return errors.Distinct().ToList();
        }
        private static void UniqueIds(IEnumerable<int> ids, string label, List<string> errors)
        {
            var list = ids.ToList();
            if (list.Any(x => x <= 0) || list.Distinct().Count() != list.Count) errors.Add(label + "编号须为唯一正整数。");
        }
    }
}
