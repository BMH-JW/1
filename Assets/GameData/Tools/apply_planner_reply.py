"""按指定V3工作簿和策划答复生成本轮测试配置；不会改动输入文件。
用法：python Assets/GameData/Tools/apply_planner_reply.py <原V3.xlsx> <策划答复.docx>
覆盖默认资产/JSON基线及来源快照，先备份自定义配置。固定实现约定见README。
仅支持当前文档结构，明确核对关键行后生成；不是执行任意文档指令的工具。
"""
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import zipfile

sys.dont_write_bytecode = True
from import_v3 import read_tables, build as build_old, yaml_lines

W = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}


def read_reply(path):
    with zipfile.ZipFile(path) as z:
        body = ET.fromstring(z.read('word/document.xml')).find('w:body', W)
    tables = []
    paragraphs = []
    for el in body:
        if el.tag.endswith('}tbl'):
            rows = []
            for row in el.findall('w:tr', W):
                rows.append([''.join(t.text or '' for t in c.findall('.//w:t', W)) for c in row.findall('w:tc', W)])
            tables.append(rows)
        elif el.tag.endswith('}p'):
            text = ''.join(t.text or '' for t in el.findall('.//w:t', W))
            if text: paragraphs.append(text)
    return dict(file=path.name, sha256=hashlib.sha256(path.read_bytes()).hexdigest(), paragraphs=paragraphs, tables=tables)


def build_config(workbook, reply):
    tables = read_tables(workbook)
    old = build_old(workbook, tables)
    snapshot = read_reply(reply)
    candidates = [t for t in snapshot['tables'] if t and t[0] == ['申报类型', '目的地', '完整通用入境理由']]
    if len(candidates) != 1 or len(candidates[0]) != 7:
        raise ValueError('未找到唯一的六类通用入境理由表，未写入配置。')
    text = json.dumps(snapshot, ensure_ascii=False)
    for required in ['随机 4–8', 'F003', 'Δ=M÷5', 'Δ=−M÷5', '来源世界状态 −1', '3–5级对应灵虚']:
        if required not in text:
            raise ValueError('策划答复关键约定已变化，请人工核对：' + required)
    c = dict(version='v3-reply-test-2026-10-18', sourceWorkbook=workbook.name, sourceSha256=old['sourceSha256'],
        replyDocument=reply.name, replySha256=snapshot['sha256'], testConfiguration=True,
        startDay=1, minDailyApplicants=4, maxDailyApplicants=8, initialTrust=50, trustMin=0, trustMax=100,
        initialPurity=50.0, initialOrder=50.0, worldValueMin=0.0, worldValueMax=100.0,
        complianceReward=3, violationPenalty=5, impactDivisor=5, rejectionSourcePenalty=1,
        enableDailyRules=True, enableBondTrace=True, enableDriveAssessment=True, enableDocuments=True,
        enableWorldChanges=True, enableDelayedFeedback=True, showImmediateCompliance=False,
        enablePopulationFeedback=False, enableThresholdEvents=False, debugMode=False, logRuleResult=False,
        feedbackDelayDays=1, dailyRuleCount=1, bondTraceLabel='羁绊牵挂', driveAssessmentLabel='驱力等级', conflictPolicy=0,
        worlds=[dict(id=0, displayName='灵虚'), dict(id=1, displayName='废墟')],
        lightFeedbackThresholds=[45.0,55.0], formalEventThresholds=[40.0,60.0],
        identities=[], portraitPools=[], feedbackTemplates=[], implementationDecisions=[
            '依据2026-10-18用户授权采用分析建议；这些实现约定不是最终平衡确认。',
            '来源逐人等概率抽取，不设当天人数配额；申报固定为来源另一界。',
            '只对申报放行或拒签，不允许改派。申报与记忆目标一致时放行合规，否则拒签合规。',
            '信任暂用合规处理+3、违规处理-5；拒签纳入统计；取消每日固定+5。',
            '拒签结束当次申请，不进入收容，不改记忆；扣留未启用。',
            'F003关闭；申报类型和真实动机由人物样本明确指定，不按属性推断，也不新增优先级系统。',
            '当前公式下合规放行影响非负；合规拒签仍可能降低来源世界状态。',
            '世界真实值仅在日结变动，UI刷新结算后的实际值；审批后的默认提示仅为处理已记录。',
            '分别累计两界精确变化，日终对旧值加总变化四舍五入，再限制在0～100。',
            '人物身份与当次申请分开；首次按原籍池选定立绘，同一局跨日复现保持姓名、原籍、立绘一致。'],
        pendingDecisions=[
            '信任+3/-5与记忆世界公式是可执行测试标准，需多日模拟后确认最终平衡。',
            '8名人物及申报类型/普通动机属于显式测试样本；正式姓名、原籍和立绘资源仍需策划/美术补充。',
            '诚实/粉饰/欺骗由样本配置，正式文案与人物真实情况的一致性仍需内容审核；不自动推断欺骗类型。',
            '留声机只有按真实结果生成的调试事实模板；正式叙事、条件及日结展示内容待补。',
            '0/100的失败或事件处理未确认，当前只记录边界，不自动结束游戏。',
            '45/55与40/60仅保留阈值资料；触发方向、冷却、持续时间和事件内容未完整定义，事件关闭。',
            '低于40、权重乘2及两界对应等级仍未确认；生成权重反馈关闭，基础表6权重保持不变。',
            '本局保存是内存保存，未提供退出游戏后的存档/读档。'])
    for key in ['bands','generationWeights','tags','details','semantics','motives','documents','items','rules','formulas']:
        c[key] = old[key]
    for s in c['semantics']:
        s['confirmed'] = True
    for r in c['rules']:
        if r['id'] == 'R_MEM_01':
            r.update(enabled=True, confirmed=True, firstDay=1, lastDay=0, repeatEveryDays=1, priority=10,
                lowAnchor=1.0, highAnchor=5.0, lowDecision=0, highDecision=1, tieBreak=2,
                source='策划逐项答复：记忆1～2级去废墟，3～5级去灵虚；中点明确归灵虚。')
    for row in candidates[0][1:]:
        kind={'诚实':0,'粉饰':1,'欺骗':2}[row[0]]
        destination={'灵虚':'utopia','废墟':'anti_utopia'}[row[1]]
        c['documents'].append(dict(id=9001+kind*2+(destination=='anti_utopia'), field='entry_reason', motive='any', destination=destination,
            triggerType=kind, text=row[2].replace('灵虚','{utopia}').replace('废墟','{anti_utopia}'), source=reply.name+'／事项5六类通用入境理由'))
    for f in c['formulas']:
        f['enabled']=f['id'] in ['F001','F002','F004','F005','F013']
        f['confirmed']=f['enabled']
        f['testOnly']=f['id'] in ['F004','F005','F013']
        f['source']=reply.name+'；用户授权按分析建议执行测试约定。'
        f['description']={
            'F003':'本版关闭；类型由人物样本显式指定，旧偏差和性格算法不参与。',
            'F004':'测试：放行灵虚，纯净变化=当前记忆原始值/impactDivisor。',
            'F005':'测试：放行废墟，秩序变化=-当前记忆原始值/impactDivisor。',
            'F013':'测试：全部合规处理+complianceReward，违规处理-violationPenalty；无每日固定加分。',
            'F014':'关闭；信任边界只记录，失败规则待确认。',
            'F015':'关闭；世界0/100只记录，失败规则待确认。'}.get(f['id'],f['description'])
    for i, origin in enumerate(old['origins']):
        c['portraitPools'].append(dict(origin=origin,portraitKeys=[f'sample:origin-{i+1:02d}-a',f'sample:origin-{i+1:02d}-b']))
    for i,name in enumerate(old['names']):
        c['identities'].append(dict(id=f'PERSON-{i+1:03d}',name=name,origin=old['origins'][i%len(old['origins'])],portraitKey='',source='项目既有姓名/原籍组成的测试样本，非正式人物设定。',
            sampleOnly=True,useFixedAttributes=False,fixedAttributes=dict(memory=0,relationship=0,desire=0,will=0),declarationType=i%3,motiveId=9))
    for action in [0,1,2]:
        c['feedbackTemplates'].append(dict(id='TEST_FEEDBACK_'+str(action),decision=action,compliance=-1,testOnly=True,
            text='【调试事实】第{day}天，{name}的{choice}已结算。该次{utopia}记账{purityDelta}，{anti_utopia}记账{orderDelta}；当日实际净变化分别为{actualPurityDelta}、{actualOrderDelta}。',
            source='实现占位，按真实审批/日结生成；正式叙事待补。'))
    return c, snapshot, tables


def main():
    workbook,reply=map(lambda x:Path(x).resolve(),sys.argv[1:3])
    root=Path(__file__).resolve().parents[1]
    data,snapshot,tables=build_config(workbook,reply)
    asset=root/'Config/DefaultGameDataConfig.asset'
    header='\n'.join(line.rstrip() for line in asset.read_text(encoding='utf-8-sig').split('  data:')[0].splitlines())+'\n'
    (root/'Editor/Defaults.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (root/'Editor/PlannerReply.json').write_text(json.dumps(snapshot,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (root/'Editor/SourceWorkbook.json').write_text(json.dumps(dict(file=workbook.name,sha256=data['sourceSha256'],sheets=tables),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    asset.write_text(header+'  data:\n'+'\n'.join(yaml_lines(data,4))+'\n  portraitBindings: []\n',encoding='utf-8')
    print('Updated reply test baseline: 4-8 applicants, F003 off, six planner templates, identity roster, +3/-5 test trust. Asset GUID unchanged.')


if __name__=='__main__':main()
