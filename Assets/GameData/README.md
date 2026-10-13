# 游戏数据模块使用说明

本模块独立于现有 UI、时钟与 NPC 点击脚本，只新增 `Assets/GameData` 内容。无需修改场景即可使用数据调试窗口。道具暂时只有设定，没有使用、购买或奖励逻辑；不包含存档、任务、贿赂和沙漏撤销。

## 1. 先测试完整流程

1. 用项目原版本 Unity **2022.3.62f3c1** 打开项目，等待新增脚本编译和资产导入。
2. 菜单选择 **Tools → JU → Game Data → 数据调试窗口**。
3. 窗口默认选择 `Config/DefaultGameDataConfig.asset`。种子默认 **42**，点击 **开始新局**。
4. 从下拉框选择 NPC，查看申报文书和真实档案。窗口下方的真实数值只供开发调试，不是玩家界面。
5. 点击 **放行乌托邦**、**放行反乌托邦** 或 **拒签入所**。放行影响先记录，闭馆时才计入世界数值。
6. 点击 **闭馆结算并进入次日**。查看两界、信任、事件与结算报告；收容者保留，可以再次选择和审批。

此窗口是编辑器模拟，无需点击 Play。关闭窗口、脚本重载或 Unity 重启后不保留进度；固定配置和种子可重新生成相同流程。修改配置后必须重新点击“开始新局”才生效。窗口不推动已有时钟，也不改变旧 NPC UI。

## 2. 如何修改设定

在 Project 面板中选择 `Assets/GameData/Config/DefaultGameDataConfig.asset`，Inspector 可编辑初值、权重、文案与事件参数。建议先 **Ctrl+D 复制配置资产**，或使用 **Tools → JU → Game Data → 创建策划默认配置副本** 创建另一份，再拖到调试窗口。

- **属性区间与权重**：每个区间有 `level`、`minValue`、`maxValue`、`weight` 和描述。覆盖 −10～10，区间不能重叠或遗漏，权重总和为 1。先按权重选区间，再在区间内等概率取整数。
- **二维标签**：`axisX / xLevel / axisY / yLevel` 是查表键，`id` 是唯一编号，`name / description` 是展示内容。两组各有25个组合。
- **一维明细**：`attribute / level` 是查表键，`text` 为展示内容，`description` 为补充设定。`desire_body` 使用欲望等级。
- **动机**：四属性各有1级、5级对应的极端动机，`any / level=0` 是普通生活。多个极端动机匹配时随机选一个，没有则选普通生活。
- **文书**：`field` 使用 `body_state` 或 `entry_reason`；`destination` 使用 `utopia`、`anti_utopia` 或 `any`；`triggerType` 的 0/1/2 分别对应诚实/粉饰/欺骗；`motive` 使用动机名称或 `any`。
- **道具**：`originalEffect` 保留原道具表描述，`formulaEffect` 保留冲突的公式表描述，`notes` 说明待定内容。`crystalCost=-1` 表示未定义购买价格，不表示免费。改描述不会产生道具效果。
- **姓名/原籍池**：这些是新增示例，可以替换为正式设定。诚实使用真实原籍，粉饰和欺骗会从其他原籍中选择申报原籍。

编辑后点击 **检查配置完整性**，在 Console 查看结果。无效配置不能开局。

文书匹配优先级：具体动机优先，再优先具体目的地，最后使用通用模板；同等优先级按 `id` 从小到大选择。重复的完整匹配键会报错。已有11条文书按原文保存，另增6条通用入境理由并标明 `source`。诚实身体状态直接使用生成时真实状态，不显示原表中“真实呈现当前状态”占位文字。

支持文字占位符：`{motive}`、`{motive_desc}`、`{destination}`、`{name}`、`{origin}`。文书中的文字仅作为数据，不执行任何指令。

`Editor/Defaults.json` 是生成配置副本时使用的策划基线，游戏不读取它。正常调整玩法只需编辑 `.asset`；修改现有资产不会同步改写 JSON。原始 Excel 保持不变，没有自动导入工作流。

## 3. 已确定的玩法规则

初始第1天，两界与信任均为50，每日8名申请者。四属性始终位于−10～10。记忆数值越高，展示的记忆残留越低，保留策划中的反向含义。

申请目的地两界各50%。欺骗偏差为相悖属性的幅度之和：

```text
乌托邦：max(0,-记忆) + max(0,-关系) + max(0,欲望) + max(0,意志)
反乌托邦：max(0,-记忆) + max(0,关系) + max(0,欲望) + max(0,意志)
```

默认偏差≤3诚实，4～8粉饰，≥9欺骗；无性格修正。动机和文书生成后保留，收容属性变化时只刷新真实档案。生成时欲望和意志已同时达到10的NPC也会标记变异。

放行乌托邦影响 `(记忆+关系-欲望-意志)/10`，放行反乌托邦影响 `(记忆-关系-欲望-意志)/10`，保留小数。对实际放行世界的影响≥0为合规，<0为违规；申请目的地不影响合规。拒签不计入放行统计，收容者可再次放行，变异者禁止放行。

日终依次执行：

1. 汇总当天放行影响。
2. 收容腐化：记忆−1、关系−1、欲望+1、意志+1，新入所者当天也变化。
3. 已生效黑天鹅：两界各−10，收容者额外欲望+2、意志+2，剩余次数−1。
4. 限制属性范围，判定欲望与意志均为10者变异。
5. 低于50的世界数值恢复1，恢复不超过50；世界数值本身不做上下限截断。
6. 信任变化为 `合规数×5-违规数×10+5`。
7. 信任≤0，或任一世界≤20则失败，停止推进。失败检查在恢复之后。
8. 未失败、日初无活动事件，且任一世界≤30则启动黑天鹅；次日日终开始，持续3次。活动期间不叠加、不重置，结束当天不重触发，此后的日终可以再次触发。
9. 进入次日、生成新申请者。未审批者离开，不自动拒签、不罚信任。

没有收容容量限制。当前局只保留收容者、当天申请者和当天放行状态；离开的NPC在次日移除，不提供完整历史档案。结算报告可由调用方保留。

## 4. 后续如何接入 UI

由一个调用方持有整局 `GameSession`，不要每次点击都重新创建，也不要让每个面板创建一局。

```csharp
using JU.GameData;
using System.Linq;
using UnityEngine;

// 在你之后新增的控制器中使用，不需要修改数据模块。
public class DataControllerExample : MonoBehaviour
{
    public GameDataConfig config; // Inspector 拖入配置资产
    private GameSession session;

    private void Start()
    {
        session = GameSession.StartNewGame(config.data, 42);
        var applicant = session.GetState().npcs.First(n => n.status == NpcStatus.Pending);
        NpcProfile profile = session.GetProfile(applicant.id); // 真实档案，定性文字
        TravelDocument document = session.GetDocument(applicant.id); // 申报文书
        Debug.Log(profile.bondTag.name + " / " + document.reasonText);
    }

    public void ReleaseToUtopia(string npcId)
    {
        DecisionResult result = session.SubmitDecision(npcId, Decision.ReleaseToUtopia);
        if (!result.success) Debug.LogWarning(result.error);
        // 成功后重新查询状态并刷新UI。
    }

    public void CloseDay(int expectedDay)
    {
        DayEndReport report = session.EndDay(expectedDay);
        if (!report.success) Debug.LogWarning(report.error);
        // report.after 为最新状态；gameOver 时展示 failureReasons。
    }
}
```

接口说明：

| 接口 | 用途 |
| --- | --- |
| `StartNewGame(config.data, seed)` | 检查配置、复制设定并生成首日申请者；无效配置抛出 `ArgumentException` |
| `GetState()` | 获取世界、天数、事件、NPC和当天审批记录快照 |
| `GetNpc(id)` | 获取真实数值与完整NPC数据副本，不存在则返回null |
| `GetProfile(id)` | 获取最新真实档案，不存在则返回null |
| `GetDocument(id)` | 获取生成时的申报文书，不存在则返回null |
| `SubmitDecision(id, decision)` | 返回成功/失败、原因及放行记录；失败不改变状态 |
| `EndDay(expectedDay)` | 结算指定当天；旧天数/错误天数/游戏结束时返回失败，防止重复结算 |

先读取并保留要结算的当天编号，再调用 `EndDay(day)`。同一闭馆事件的重复回调应传同一个编号，不要每次重新读取最新天数，否则会把下一天也推进。只应有一个日终调用方。

所有公开查询均返回副本，修改副本不会改变游戏。不要通过修改 `GetNpc()` 的结果实现道具；之后应新增明确的数据操作接口。玩家UI应使用 `NpcProfile` 和 `TravelDocument`，原始属性仅用于开发调试。

当前没有连接旧 `GameTimeManager` 或 `NPCDailyManager`。将来接入时，需要统一每日人数、天数及收容再审批是否计入旧点击次数；旧脚本里的每日8次计数不会自动成为本模块的数据队列。

## 5. 测试

打开 **Window → General → Test Runner → EditMode**，运行 `JU.GameData.Tests`。测试验证默认配置资产、随机生成、档案/文书、审批与日终、收容与变异、黑天鹅时序、失败条件以及配置/状态副本隔离。

测试基于原始默认参数。修改默认资产后，依赖基线设定的测试可能失败；建议保留默认资产作为参考，用副本调试自己的平衡参数。

本次验证：使用 Unity 2022.3.62f3c1 在隔离项目中运行20项 EditMode 测试，全部通过，包含默认资产与策划基线JSON的一致性检查。另通过19项独立C#逻辑场景，并使用本机Unity程序集检查新增运行时、编辑器和测试代码编译。未打开原项目执行测试，未验证旧UI与新数据的集成。
