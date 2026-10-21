from pathlib import Path
import zipfile,re,json,hashlib,difflib
root=Path(r'D:\UnityProject\CraneSignalAI');out=root/'Phase2A';z=zipfile.ZipFile(out/'Phase2_Start_Baseline/working-tree-overlay.zip');base=json.loads((out/'Phase2_Start_Baseline/Baseline.json').read_text())['hashes']
def sec(s,name,end):return s.split('  '+name+':',1)[1].split('  '+end+':',1)[0]
def blocks(s):return {int(m.group(1)):m.group(0) for m in re.finditer(r'(?m)^  - m_Index: (\d+)\n(?:(?!  - m_Index:).|\n)*?(?=^  - m_Index:|\Z)',sec(s,'m_GlyphTable','m_CharacterTable'),re.M)}
def normal(s):
 s=re.sub(r'(?m)^  _typelessdata:.*$','  _typelessdata: [atlas pixels]',s)
 for start,end in [('m_GlyphTable','m_CharacterTable'),('m_CharacterTable','m_AtlasTextures'),('m_UsedGlyphRects','m_FreeGlyphRects'),('m_FreeGlyphRects','m_FontFeatureTable')]:
  if '  '+start+':' in s and '  '+end+':' in s:s=s.replace(sec(s,start,end),' [glyph data]\n')
 return s
phrases=json.loads((out/'WorkerUIUsedPhrases.json').read_text(encoding='utf-8-sig'));used=set(ord(c) for p in phrases for c in p['text']);rows=[]
names=['Assets/UI/Fonts/CraneDashboard SDF.asset','Assets/UI/Fonts/OperatorCommand SDF.asset','Assets/UI/Fonts/OperatorCommand Korean SDF.asset']
for name in names:
 old=z.read(name).decode('utf-8-sig').replace('\r\n','\n');new=(root/name).read_text(encoding='utf-8-sig');a,b=blocks(old),blocks(new);ca=set(map(int,re.findall(r'm_Unicode:\s*(\d+)',sec(old,'m_CharacterTable','m_AtlasTextures'))));cb=set(map(int,re.findall(r'm_Unicode:\s*(\d+)',sec(new,'m_CharacterTable','m_AtlasTextures'))))
 changed=[k for k in a.keys()&b.keys() if a[k]!=b[k]];diff=''.join(difflib.unified_diff(normal(old).splitlines(True),normal(new).splitlines(True),fromfile='before',tofile='after'));(out/(Path(name).stem.replace(' ','_')+'_nonGlyph.diff')).write_text(diff,encoding='utf-8');row=dict(path=name,before=base[name],after=hashlib.sha256((root/name).read_bytes()).hexdigest(),addedGlyphCount=len(b.keys()-a.keys()),addedCharacters=[dict(character=chr(c),unicode=f'U+{c:04X}') for c in sorted(cb-ca)],nonWorkerCharacters=[f'U+{c:04X}' for c in sorted((cb-ca)-used)],oldGlyphChanges=changed,removedCharacters=sorted(ca-cb),settingsUnchanged=not diff);rows.append(row)
other=[dict(path=p,before=h,after=hashlib.sha256((root/p).read_bytes()).hexdigest(),unchanged=h==hashlib.sha256((root/p).read_bytes()).hexdigest()) for p,h in base.items() if (p.endswith('.asset') and ('SDF' in p or '/Fonts/' in p)) and p not in names]
(out/'FontAudit.json').write_text(json.dumps(dict(allowedFonts=rows,otherFonts=other,passed=all(not r['nonWorkerCharacters'] and not r['oldGlyphChanges'] and not r['removedCharacters'] and r['settingsUnchanged'] for r in rows) and all(r['unchanged'] for r in other)),ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(dict(fonts=[{k:r[k] for k in ['path','addedGlyphCount','nonWorkerCharacters','oldGlyphChanges','settingsUnchanged']} for r in rows],otherFonts=other),ensure_ascii=True,indent=2))
