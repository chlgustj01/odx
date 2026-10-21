from pathlib import Path
import json,hashlib,re,difflib,collections,shutil,zipfile
R=Path(r'D:\UnityProject\CraneSignalAI'); O=R/'Phase2A'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
base=read(O/'Phase2_Start_Baseline/Baseline.json')['hashes'];changes=[];same=[];removed=[]
for p,h in base.items():
 q=R/p
 if not q.exists():removed.append(p)
 elif sha(q)!=h:changes.append(dict(path=p,before=h,after=sha(q)))
 else:same.append(p)
new=[dict(path=p.relative_to(R).as_posix(),after=sha(p)) for p in (R/'Assets').rglob('*') if p.is_file() and p.relative_to(R).as_posix() not in base]
tests=[p for p in base if p.endswith('.cs') and ('Test' in p or 'Checks' in p or 'Runner' in p)]
testChanges=[x for x in changes if x['path'] in tests]
old=(O/'SceneRelative.original.cs.txt').read_text(encoding='utf-8-sig');current=(R/'Assets/Editor/Prediction/LiveLoadSceneRelativeChecks.cs').read_text(encoding='utf-8-sig')
prefix=current.startswith(old);raw=zipfile.ZipFile(O/'Phase2_Start_Baseline/working-tree-overlay.zip').read('Assets/Editor/Prediction/LiveLoadSceneRelativeChecks.cs');rawPrefix=(R/'Assets/Editor/Prediction/LiveLoadSceneRelativeChecks.cs').read_bytes().startswith(raw)
audit=dict(changedExisting=changes,newFiles=new,removed=removed,testFileCount=len(tests),testChanges=testChanges,originalSceneRelativeByteTextPrefixPreserved=prefix and rawPrefix,originalSceneRelativePreservedByteCount=len(raw),unchangedTests=[dict(path=p,sha256=base[p]) for p in tests if p in same],unchangedCore=[dict(path=p,sha256=base[p]) for p in same if p.endswith('.cs') and any(s in p for s in ['/Entrance/','CommandReceiver','TowerCraneController','Prediction/','Verification','FaultInjector','WorkerRoute','ExplorerHazard'])])
write(O/'FileAudit.json',audit)
before=(O/'Explorer.before.cs.txt').read_text(encoding='utf-8-sig');after=(R/'Assets/Scripts/Shipyard/Explorer/ShipyardExploreCameraController.cs').read_text(encoding='utf-8-sig')
(O/'Explorer.diff').write_text(''.join(difflib.unified_diff(before.splitlines(True),after.splitlines(True),fromfile='baseline',tofile='Phase2A')),encoding='utf-8')
def classify(path):
 c=read(path);counts=collections.defaultdict(lambda:dict(Error=0,Warning=0,Log=0));rows=[]
 for x in c['rows']:
  m=x['message'];mode=int(x['mode']);level='Warning' if mode&(4|128|512|4096) else 'Error' if mode&(1|2|16|64|256|2048|8192|131072|1048576|2097152|4194304) else 'Log'
  source='C# compiler' if re.search(r'\b(?:error|warning) CS\d+',m) else ('Project Editor' if re.search(r'Assets[\\/]Editor',m) else 'Project Runtime') if re.search(r'Assets[\\/](Scripts|Editor)',m) else 'MCP package' if re.search(r'IvanMurzak|McpPlugin|HubConnection|Server timeout',m,re.I) else 'Unity native/system'
  counts[source][level]+=1;rows.append(dict(level=level,source=source,index=x['index'],message=m))
 return dict(errors=c['errors'],warnings=c['warnings'],counts=dict(counts),rows=rows)
if (O/'Final/Console.json').exists():
 a=classify(O/'BaselineConsole/Console.json');b=classify(O/'Final/Console.json');cats=set(a['counts'])|set(b['counts']);delta={k:{lev:b['counts'].get(k,{}).get(lev,0)-a['counts'].get(k,{}).get(lev,0) for lev in ['Error','Warning','Log']} for k in sorted(cats)}
 known=collections.Counter((r['level'],r['source'],r['message']) for r in a['rows']);fresh=[]
 for row in b['rows']:
  key=(row['level'],row['source'],row['message'])
  if known[key]:known[key]-=1
  else:fresh.append(row)
 result=dict(before={k:v for k,v in a.items() if k!='rows'},after={k:v for k,v in b.items() if k!='rows'},delta=delta,newMessages=fresh,newProjectProblems=[r for r in fresh if r['source'] in ['Project Runtime','Project Editor','C# compiler'] and r['level'] in ['Error','Warning']])
 if (O/'PostFixBaselineConsole/Console.json').exists():
  fix=classify(O/'PostFixBaselineConsole/Console.json');fk=collections.Counter((r['level'],r['source'],r['message']) for r in fix['rows']);problems=[]
  for row in b['rows']:
   key=(row['level'],row['source'],row['message'])
   if fk[key]:fk[key]-=1
   elif row['source'] in ['Project Runtime','Project Editor','C# compiler'] and row['level'] in ['Error','Warning']:problems.append(row)
  result['postFixBefore']={k:v for k,v in fix.items() if k!='rows'};result['postFixDelta']={k:{lev:b['counts'].get(k,{}).get(lev,0)-fix['counts'].get(k,{}).get(lev,0) for lev in ['Error','Warning','Log']} for k in set(fix['counts'])|set(b['counts'])};result['postFixProblems']=problems
  result['historyNote']='FirstFinal captured +1 Project Runtime MissingReferenceException before correction. Shutdown guard fixed it; active Explore Play-stop reproduction and full final Gate follow. Console buffer reset during restored-default automatic recompile lifecycle; original snapshots and Editor.log evidence retained. No manual Clear or log filter used. Negative whole-task deltas are not evidence of absence of earlier errors.'
 write(O/'ConsoleFinalClassification.json',result)
gate=O/'PostGate';suites=[]
for d in sorted(gate.glob('[0-9][0-9]_*')):
 files=[]
 for p in sorted(d.rglob('*tests.json')):
  v=read(p);files.append(dict(path=p.relative_to(R).as_posix(),passed=v.get('passed'),count=v.get('count',len(v.get('rows',v.get('results',[])))),error=v.get('error')))
 record=dict(suite=d.name,files=files,harnessErrors=[str(p) for p in d.glob('*Error*')])
 repair=O/'HmiRepairGate'/('00_HMI13' if d.name=='07_HMI13' else '01_Operator18')
 if d.name in ['07_HMI13','06_Operator18'] and repair.exists():
  final=[]
  for p in repair.glob('*tests.json'):
   v=read(p);final.append(dict(path=p.relative_to(R).as_posix(),passed=v.get('passed'),count=len(v.get('rows',v.get('results',[]))),error=v.get('error')))
  if final:record.update(previousAttemptFiles=files,files=final,repairReason='RemainingText existing English TARGET REACHED truncation; only Scene TMP auto-size minimum adjusted; no test/expected/font asset settings changed')
 suites.append(record)
write(O/'GateSummary.json',suites)
expected={'00_Original103':103,'01_Confidence17':17,'02_Entrance44':44,'03_SceneRelative8':8,'04_CoverageGaps':8,'05_Timeline11':11,'06_Operator18':18,'07_HMI13':13,'09_Workers':27,'10_WorkerWalking44':44}
validation=[]
for name,count in expected.items():
 s=next((s for s in suites if s['suite']==name),None);validation.append(dict(suite=name,expected=count,actual=sum(f['count'] for f in s['files']) if s else 0,passed=bool(s and not s['harnessErrors'] and s['files'] and all(f['passed'] for f in s['files']) and sum(f['count'] for f in s['files'])==count)))
write(O/'GateValidation.json',dict(passed=all(r['passed'] for r in validation),rows=validation))
print(json.dumps(dict(existingChanged=len(changes),newFiles=len(new),removed=removed,testChanges=testChanges,sceneRelativePrefix=prefix,gate=suites),ensure_ascii=False,indent=2))
