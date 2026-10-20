"""Read-only XLSX -> runtime JSON. Run with bundled Python + openpyxl.
Usage: python preprocess_accidents.py SOURCE.xlsx --out DATA_DIR --reports REPORT_DIR
No inference from narratives, no workbook save, no risk/probability model.
"""
import argparse, collections, datetime, hashlib, json, pathlib, re
import openpyxl
from openpyxl.utils import get_column_letter

FIELDS = {
    'accidentType':'CM-A01','caseType':'CM-A07','craneRelated':'CM-A05',
    'workType':'CM-A06','shipyardArea':'CM-P05','spaceType':'CM-P06',
    'workStage':'CM-K01','craneType':'CM-E01','workerRole':'CM-H04',
    'workerLocation':'CM-H03','dropMechanism':'DR-X02','overturnCause':'OT-X04',
    'sourceDatabase':'CM-S01','evidenceGrade':'CM-Q01'
}
DIMENSIONS=['workType','shipyardArea','spaceType','workStage','craneRelated','craneType','workerRole']
UNKNOWN={'unknown','미상','정보 없음','정보없음','notavailable','n/a','없음'}
def raw(v):
    if v is None:return None
    if isinstance(v,float) and v.is_integer():return str(int(v))
    return str(v).strip() or None
def unknown_label(v):return v is None or v.strip().lower() in UNKNOWN
def dump(p,obj):p.write_text(json.dumps(obj,ensure_ascii=False,indent=2,default=str),encoding='utf-8')

def run(source,out,reports):
    before=hashlib.sha256(source.read_bytes()).hexdigest()
    w=openpyxl.load_workbook(source,read_only=True,data_only=True)
    values=list(w['사고DB'].values);headers=list(values[0]);names=list(values[1])
    assert len(headers)==len(set(headers)), 'Duplicate column headers'
    definitions={r[0]:r for r in w['변수사전'].iter_rows(min_row=5,values_only=True) if r[0]}
    maps={};active=None
    for r in w['코드표'].values:
        if r[0]:
            m=re.match(r'((?:CM|DR|OT)-[A-Z]\d+)',str(r[0]));active=m.group(1) if m else None
            if active:maps.setdefault(active,{})
        if active and isinstance(r[1],(int,float)) and r[2] is not None:
            code,label=raw(r[1]),str(r[2]).strip()
            if code in maps[active]:assert maps[active][code]==label, (active,code)
            maps[active][code]=label
    # Binary flags use the explicitly documented 1/0/9 vocabulary.
    binary={}
    for field,d in definitions.items():
        if d[3]=='1/0/9' and field not in maps:
            parsed={}
            for part in str(d[4]).split(' / '):
                m=re.match(r'(\d+)\s+(.+)',part)
                if m:parsed[m.group(1)]=m.group(2)
            assert set(parsed)=={'0','1','9'},field
            maps[field]=parsed;binary[field]=parsed
    def decoded(row,field):
        v=raw(row.get(field))
        if v is None or v.lower() in UNKNOWN:return None,None
        label=maps[field].get(v)
        if label is None:raise ValueError(f'Unmapped code {field}={v}')
        return (None,None) if unknown_label(label) else (v,label)
    records=[];missing=collections.Counter();errors=[]
    for rownum,rv in enumerate(values[2:],3):
        if not any(v is not None for v in rv):continue
        r=dict(zip(headers,rv));aid=raw(r.get('사고ID'))
        if not aid:errors.append(rownum);continue
        a={'accidentId':aid,'sourceRow':rownum,'originalId':raw(r.get('원자료ID')),
           'title':raw(r.get('사고명')),'description':raw(r.get('사고경위')),
           'causeSummary':raw(r.get('원인요약')),'sourceUrl':raw(r.get('출처URL')),
           'evidenceLevel':raw(r.get('근거수준')),'reviewStatus':raw(r.get('검수')),
           'notes':raw(r.get('비고'))}
        for key,col in FIELDS.items():
            code,label=decoded(r,col);a[key+'Code']=code;a[key]=label
        a['evidenceGrade']=(a['evidenceGrade'] or '').split(' ')[0] or None
        a['dropMechanisms']=[];a['overturnMechanisms']=[];a['controlFailureFlags']=[];a['factorMentions']=[]
        a['mechanismStates']=[]
        for col,d in definitions.items():
            if not (col.startswith(('DR-M','OT-L','OT-S','OT-D','CM-F'))):continue
            applies=not(col.startswith('DR-') and r.get('CM-A02')!=1 or col.startswith('OT-') and r.get('CM-A01')!=11)
            v=raw(r.get(col));state='NOT_APPLICABLE' if not applies else 'UNKNOWN' if v in (None,'9') else 'IDENTIFIED' if v=='1' else 'ABSENT' if v=='0' else 'UNKNOWN'
            a['mechanismStates'].append({'column':col,'label':d[1],'state':state})
            if state=='IDENTIFIED':
                key='dropMechanisms' if col.startswith('DR-') else 'overturnMechanisms' if col.startswith(('OT-L','OT-S')) else 'controlFailureFlags' if col.startswith('OT-D') else 'factorMentions'
                a[key].append({'column':col,'label':d[1]})
        # Retain categorical main mechanisms as explicit source facts, without switching binary flags.
        a['dropIndicator']=r.get('CM-A02');a['craneCapacityT']=r.get('CM-E03');a['loadWeightT']=r.get('CM-K02');a['windMps']=r.get('CM-W03')
        a['knownContextFields']=sum(a[k+'Code'] is not None for k in DIMENSIONS)
        for k in FIELDS:
            if a[k+'Code'] is None:missing[k]+=1
        records.append(a)
    counts=collections.Counter(a['accidentId'] for a in records)
    duplicates=[k for k,v in counts.items() if v>1]
    assert not errors and not duplicates,(errors,duplicates)
    assert len(records)==sum(any(v is not None for v in r) for r in values[2:])
    out.mkdir(parents=True,exist_ok=True);reports.mkdir(parents=True,exist_ok=True)
    root={'schemaVersion':'1.0','sourceWorkbook':source.name,'sourceSheet':'사고DB','sourceSha256':before,
          'generatedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'recordCount':len(records),'records':records}
    dump(out/'accidents_normalized.json',root)
    cb={'fields':[{'column':c,'displayName':definitions[c][1],
          'options':[{'code':k,'label':v,'unknown':unknown_label(v)} for k,v in choices.items()]} for c,choices in maps.items()]}
    dump(out/'AccidentDBCodebook.json',cb)
    lines=['# Accident DB 실제 Schema 조사','',f'원본: `{source}`',f'SHA-256: `{before}`','',
           '## Sheet 목록','| Sheet | 물리 행 | 열 |','|---|---:|---:|']
    lines += [f'| {s.title} | {s.max_row} | {s.max_column} |' for s in w]
    lines += ['',f'데이터 Sheet: 사고DB. 1행 코드, 2행 표시명, 3행부터 실제 사고 {len(records)}건. 총 {len(headers)}개 열.',
              '사용법·정의·셀 내용은 데이터로 해석했으며 실행 지시로 사용하지 않았다. 원본 미수정.','',
              '## 실제 열 정의 및 관측 값 형식','| Excel 열 | 코드 | 표시명 | 실제 Python 형식 | 예시(최대 70자) |','|---|---|---|---|---|']
    for i,c in enumerate(headers):
        present=[r[i] for r in values[2:] if r[i] is not None]
        types=', '.join(sorted(set(type(v).__name__ for v in present))) or 'empty'
        examples='; '.join(dict.fromkeys(str(v).replace('\n',' ')[:70] for v in present[:3])).replace('|','/')
        lines.append(f'| {get_column_letter(i+1)} | {c} | {names[i]} | {types} | {examples} |')
    lines += ['', '## Runtime mapping','| Runtime | Workbook | 표시명 |','|---|---|---|']
    lines += [f'| {k}, {k}Code | {c} | {definitions[c][1]} |' for k,c in FIELDS.items()]
    lines += ['| description | 사고경위 | 사고 경위 |','| causeSummary | 원인요약 | 원인 요약 |','| sourceUrl | 출처URL | 출처 URL |',
              '| originalId / sourceRow | 원자료ID / 실제 Excel 행 번호 | 추적 정보 |',
              '', '## 결측·적용범위·해석',
              '- null을 내부 결측 표현으로 사용한다. 코드표 label이 미상/정보 없음인 코드만 결측으로 바꾼다.',
              '- 9는 공통 미상 코드가 아니다. CM-A06=9 점검·검사, CM-P05=9 야드 도로, DR-X02=9 운전·신호 오류는 유효 값이다.',
              '- CM-A05=0 무관도 유효한 값이며 UNKNOWN으로 바꾸지 않는다.',
              '- DR flag는 CM-A02=1, OT flag는 CM-A01=11에만 적용한다. 범위 밖은 NOT_APPLICABLE, 빈칸/9는 UNKNOWN.',
              '- CM-F는 요인 언급이다. 확인된 메커니즘/확정 원인으로 바꾸지 않는다.',
              '- CM-Q01의 A/B/C와 근거수준 원문, 검수 상태를 각각 유지한다. 등급을 재평가하지 않았다.',
              '- 정격용량·인양하중 단위 t, 풍속 m/s. 원문 없는 값/날짜/좌표/예방조치를 만들어 넣지 않는다.',
              '- CSI 확장 자료는 항만·해상 유사 작업이며 전체 802건이 모두 조선소 사고인 것은 아니다.',
              '- 분석 Sheet의 캐시 없는 수식 결과를 이용하지 않고 사고DB 실제 행을 읽었다.']
    (reports/'AccidentDB_SchemaReport.md').write_text('\n'.join(lines),encoding='utf-8')
    report=['# Accident DB Normalization Report','',f'Source Workbook: `{source}`','Source Sheet: 사고DB',
            f'Source Rows (actual records): {len(records)}',f'Normalized Records: {len(records)}',
            f'Duplicate IDs: {len(duplicates)}',f'Missing IDs: {len(errors)}',f'Source SHA-256: {before}',
            '', '## Unknown Field Counts','| Field | null 건수 |','|---|---:|']
    report += [f'| {k} | {missing[k]} |' for k in FIELDS]
    report += ['', '## 자료 상태',
        f'- 검수 상태: {dict(collections.Counter(a["reviewStatus"] for a in records))}',
        f'- 사례 종류: {dict(collections.Counter(a["caseType"] for a in records))}',
        '- near miss 코드가 존재하지만 실제 near miss 기록은 0건이다. 필터 결과 없음은 안전 판정이 아니다.',
        '- Excel 자체의 근거 등급과 요인 코딩을 보존했다. 외부 source URL의 사실관계 재검증은 수행하지 않았다.',
        '- Runtime identified mechanism은 원본 flag=1일 때만 제공한다. categorical 주 메커니즘은 별도로 표시한다.']
    assert hashlib.sha256(source.read_bytes()).hexdigest()==before,'Source changed during read'
    (reports/'AccidentDB_NormalizationReport.md').write_text('\n'.join(report),encoding='utf-8')
    dump(reports/'AccidentDB_NormalizationChecks.json',{'sourceRows':len(records),'normalizedRecords':len(records),
         'duplicateIds':duplicates,'missingIdRows':errors,'unknownFields':dict(missing),'sourceUnchanged':True,'sourceSha256':before})
    print(json.dumps({'records':len(records),'codebookFields':len(maps),'sourceUnchanged':True},ensure_ascii=False))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('source',type=pathlib.Path);p.add_argument('--out',type=pathlib.Path,required=True);p.add_argument('--reports',type=pathlib.Path,required=True);a=p.parse_args();run(a.source,a.out,a.reports)
