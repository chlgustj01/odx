from pathlib import Path
import zipfile,re,json,difflib
R=Path(r'D:\UnityProject\CraneSignalAI');O=R/'Phase2A';z=zipfile.ZipFile(O/'Phase2_Start_Baseline/working-tree-overlay.zip')
a=z.read('Assets/Scenes/Main.unity').decode('utf-8-sig').replace('\r\n','\n');b=(R/'Assets/Scenes/Main.unity').read_text(encoding='utf-8-sig')
def blocks(s):return {m.group(2):dict(type=m.group(1),text=m.group(0)) for m in re.finditer(r'^--- !u!(\d+) &(\d+).*?\n(?:(?!^--- !u!).|\n)*',s,re.M)}
aa,bb=blocks(a),blocks(b);changed=[]
for key in aa.keys()&bb.keys():
 if aa[key]['text']!=bb[key]['text']:
  old,new=aa[key]['text'],bb[key]['text'];changed.append(dict(id=key,type=aa[key]['type'],diff=''.join(difflib.unified_diff(old.splitlines(True),new.splitlines(True),fromfile='before',tofile='after'))))
data=dict(changedExisting=changed,added=[dict(id=k,type=v['type']) for k,v in bb.items() if k not in aa],removed=[k for k in aa if k not in bb]);(O/'SceneObjectDiff.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(data,ensure_ascii=False,indent=2)[:12000])
