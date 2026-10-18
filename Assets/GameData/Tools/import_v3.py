"""只读旧版Excel，更新来源快照。仅使用Python标准库；不修改Excel。
用法：python Assets/GameData/Tools/import_v3.py <xlsx路径>
本入口不再覆盖默认资产；当前规则请使用apply_planner_reply.py。
"""
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import zipfile

NS = {'m': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}


def read_tables(path):
    with zipfile.ZipFile(path) as z:
        strings = [''.join(x.itertext()) for x in ET.fromstring(z.read('xl/sharedStrings.xml')).findall('m:si', NS)] if 'xl/sharedStrings.xml' in z.namelist() else []
        rels = {r.attrib['Id']: r.attrib['Target'] for r in ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
        sheets = {}
        for sheet in ET.fromstring(z.read('xl/workbook.xml')).find('m:sheets', NS):
            target = rels[sheet.attrib['{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id']]
            target = target.lstrip('/') if target.startswith('/') else 'xl/' + target
            rows = {}
            for row in ET.fromstring(z.read(target)).findall('.//m:sheetData/m:row', NS):
                values = {}
                for c in row:
                    v = c.find('m:v', NS)
                    value = v.text if v is not None else ''
                    if c.attrib.get('t') == 's': value = strings[int(value)] if value else ''
                    elif c.attrib.get('t') == 'inlineStr': value = ''.join(c.find('m:is', NS).itertext())
                    if value: values[''.join(x for x in c.attrib['r'] if x.isalpha())] = value
                if values: rows[int(row.attrib['r'])] = values
            sheets[sheet.attrib['name']] = rows
        return sheets


def build(path, tables):
    data = dict(version='v3', sourceWorkbook=path.name, sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
        demonstrationOnly=False, worldMappingConfirmed=False, sourceWeightsConfirmed=False, investigationConfirmed=False, generationPolicyConfirmed=False,
        claimUtopiaWeight=-1, dailyInvestigationQuota=-1, investigationCost=-1, dailyRuleCount=1,
        worldValueMin=0, worldValueMax=100, trustMin=0, trustMax=-1,
        enableDailyRules=True, enableBondTrace=True, enableDriveAssessment=True, enableDocuments=True, enableWorldChanges=True, enableDelayedFeedback=True,
        enableShelterCorruption=False, enableMutation=False, enablePopulationFeedback=False, debugMode=False, logRuleResult=False,
        feedbackDelayDays=1, feedbackTemplate='第{day}天，{name}的{choice}处理已结算。本次记账影响{delta}；当日{utopia}实际净变化{purityDelta}，{anti_utopia}实际净变化{orderDelta}。', conflictPolicy=0,
        startDay=1, dailyApplicants=8, initialTrust=50, initialPurity=50.0, initialOrder=50.0,
        recoveryCeiling=50.0, dailyRecovery=1.0, blackSwanThreshold=30.0, worldFailureThreshold=20.0, blackSwanDays=3, blackSwanDamage=10.0,
        complianceReward=5, violationPenalty=10, dailyTrustBonus=5, memoryDecay=1, relationshipDecay=1, desireGrowth=1, willGrowth=1,
        blackSwanExtraDesire=2, blackSwanExtraWill=2, honestMaxDeviation=3, embellishedMaxDeviation=8,
        worlds=[dict(id=0, displayName='灵虚', sourceWeight=-1), dict(id=1, displayName='废墟', sourceWeight=-1)],
        bands=[], generationWeights=[], tags=[], details=[], semantics=[], motives=[], documents=[], items=[], rules=[], formulas=[], populationWeights=[],
        names=['林默', '苏禾', '陈舟', '许灯', '白榆', '顾川', '叶棠', '周岚'],
        origins=['第七工业区', '第九机械厂', '旧港区', '白塔城', '灰原镇', '北方矿区'],
        pendingDecisions=[
            '世界内部ID映射为占位，须策划确认；未确认禁止正式开局。',
            '来源世界分布、申报目的地比例、调查额度/消耗、信任上限未给正式值。',
            '记忆方向暂定高残留去废墟、低残留去灵虚；语义分值、锚点和等距处理待确认。',
            '关系/欲望/意志准入规则待确认，默认关闭。',
            '原表11条文书不能覆盖全部组合；补齐正式模板后才可正式开局。',
            'F003的偏差公式和性格修正未完整定义；须确认当前算法或另行实现。',
            'F004/F005按原表保留，待主题核验后启用；F006～F015按暂停机制关闭。',
            '极端动机并列按ID优先、身体沿用欲望等级为待确认策略。',
            '世界值影响后续NPC构成没有正式公式：默认关闭，禁止自动套用演示权重。',
            '反馈延迟1天与事实记录模板是实现占位；正式叙事及时间由策划确认。',
            '姓名与原籍池为项目既有示例；初始人数8、世界/信任50沿用截图要求的当前设计值。'])
    for row, r in tables['表1 属性等级映射表'].items():
        if row >= 4 and r.get('A', '').isdigit(): data['bands'].append(dict(level=int(r['A']), minValue=int(r['B']), maxValue=int(r['C']), description=r['D']))
    for row, r in tables['表6 NPC生成权重表'].items():
        if row >= 4 and 'D' in r:
            band = next(b for b in data['bands'] if b['minValue'] == int(r['B']) and b['maxValue'] == int(r['C']))
            data['generationWeights'].append(dict(level=band['level'], weight=float(r['D'])))
    for row, r in tables['表2 标签配置与表3 定性明细'].items():
        if r.get('A', '').isdigit() and 4 <= row <= 53:
            data['tags'].append(dict(id=int(r['A']), name=r['B'], axisX=r['C'], xLevel=int(r['D']), axisY=r['E'], yLevel=int(r['F']), description=r['G']))
        elif r.get('A', '').isdigit() and row >= 57:
            d = dict(id=int(r['A']), attribute=r['B'], level=int(r['C']), text=r['D'], description=r['E'])
            data['details'].append(d)
            if d['attribute'] != 'desire_body':
                data['semantics'].append(dict(attribute=d['attribute'], level=d['level'], meaning=d['text'], score=6-d['level'] if d['attribute']=='memory' else d['level'], confirmed=False))
    for row, r in tables['表4真实动机表'].items():
        if row >= 4 and r.get('A', '').isdigit(): data['motives'].append(dict(id=int(r['A']), attribute=r['B'], level=0 if r['B']=='any' else int(r['C']), name=r['D'], description=r['E']))
    for row, r in tables['表5 通关文书伪装文本表'].items():
        if row >= 4 and r.get('A', '').isdigit():
            text = r['F'].replace('反乌托邦', '{anti_utopia}').replace('乌托邦', '{utopia}')
            data['documents'].append(dict(id=int(r['A']), field=r['B'], motive=r['C'], destination=r['D'], triggerType=int(r['E']), text=text, source=f'Excel/表5/F{row}；世界名称转为配置占位符'))
    for row, r in tables['道具表'].items():
        if row >= 2 and r.get('A', '').startswith('I'):
            data['items'].append(dict(id=r['A'], name=r['B'], type=r['C'], costDescription=r['D'], target=r['E'], effectType=r['F'], originalEffect=r['G'], formulaEffect='', acquisition=r['H'], notes='未接入；与公式描述冲突不自动合并。', crystalCost=15 if r['A']=='I002' else 50 if r['A']=='I003' else -1))
    for row, r in tables['公式表'].items():
        if r.get('A', '').startswith('F0'):
            data['formulas'].append(dict(id=r['A'], description=r['C']+'：'+r['D'], source=f'Excel/公式表/第{row}行', enabled=r['A'] in ['F001','F002'], confirmed=r['A'] in ['F001','F002']))
    for row, r in tables['每日规则库草案'].items():
        if row >= 2 and 'A' in r:
            attr={'R_MEM_01':'memory','R_MEM_02':'memory','R_REL_01':'relationship','R_DES_01':'desire','R_WILL_01':'will','R_DOC_01':''}[r['A']]
            data['rules'].append(dict(id=r['A'], title=r['C'], attribute=attr, source='Excel/每日规则库草案：'+r['D']+'；'+r.get('I',''), enabled=False, confirmed=False,
                firstDay=1, lastDay=0, repeatEveryDays=1, priority=0, lowAnchor=0.0, highAnchor=0.0, lowDecision=0, highDecision=1, tieBreak=0, unmatchedReject=False))
    assert [len(data[k]) for k in ['bands','generationWeights','tags','details','semantics','motives','documents','items','formulas','rules']] == [5,5,50,25,20,9,11,5,15,6], '源表结构改变，请检查导入范围'
    return data


def yaml_value(value):
    if isinstance(value, bool): return '1' if value else '0'
    if isinstance(value, (int, float)): return str(value)
    return json.dumps(value, ensure_ascii=False)


def yaml_lines(data, indent):
    lines=[]
    for key, value in data.items():
        prefix=' '*indent+key+':'
        if isinstance(value, list):
            if not value: lines.append(prefix+' []'); continue
            lines.append(prefix)
            for item in value:
                if isinstance(item, dict):
                    children=yaml_lines(item, indent+4)
                    lines.append(' '*(indent+2)+'- '+children[0].lstrip())
                    lines.extend(children[1:])
                else: lines.append(' '*(indent+2)+'- '+yaml_value(item))
        else: lines.append(prefix+' '+yaml_value(value))
    return lines


def main():
    path=Path(sys.argv[1]).resolve()
    root=Path(__file__).resolve().parents[1]
    tables=read_tables(path)
    data=build(path,tables)
    (root/'Editor/SourceWorkbook.json').write_text(json.dumps(dict(file=path.name,sha256=data['sourceSha256'],sheets=tables),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Source workbook snapshot updated only. Use apply_planner_reply.py with both sources to rebuild current test configuration.')


if __name__=='__main__': main()
